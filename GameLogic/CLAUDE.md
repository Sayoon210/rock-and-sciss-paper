# GameLogic — Pure Game Logic

`RockAndScissPaper.GameLogic` is a separate .NET project (`Microsoft.NET.Sdk`, no Godot reference). The rules of the match live here: round resolution, scoring, deck/hand operations, ability card effects. This is the half of the architecture that `Scripts/Autoload/` owns but does not implement — see [Scripts/Autoload/CLAUDE.md](../Scripts/Autoload/CLAUDE.md) for the other side of the boundary.

Named `GameLogic` rather than `Core` on purpose — `Scripts/Cards/` also holds card-related files (the `CardData` Resource/`.tres` side), and a generic name like `Core` made the two easy to confuse. This folder is specifically the rules; `Scripts/Cards/` is specifically the presentation data.

## The boundary

**No Godot types in this project.** No `Node`, no `Resource`, no `Signal`, no `GD.Print`, no RPC attributes.

This isn't a convention you have to remember — it's enforced by the build. `GameLogic` references nothing, so `using Godot;` fails with `error CS0246` before the code can run. Tests reference `GameLogic` alone, so they never need Godot either.

- The test: a full match should run in a plain console harness with no Godot and no network. If a class here can't, something leaked.
- Why it's worth the discipline: round resolution has real branching (Joker > Reset > other abilities > normal; ties; vanish vs. return-to-deck-bottom), and this is the code most likely to be wrong. Verifying it by launching two Godot instances and clicking through rounds is slow enough that it won't happen often. Verifying it as a plain function call is fast enough that it will.
- `Resource` counts as a Godot type even though it needs no scene tree — it still drags in the Godot runtime and breaks the harness test.

### Card identity vs. card presentation

Because `CardData : Resource` can't cross the boundary, game logic deals in plain identity values, not `CardData`:

- Here: `ECardName` (Rock / Paper / Scissors / Blank / Joker / Reset / Swap / Transform / Draw). A deck is a list of those. Its member order is serialized — `Data/Cards/*.tres` store a card as this enum's integer value — so taking a name out or inserting one renumbers the rest and those files have to be renumbered with it. `ECardType` (Normal / Blank / Joker / Ability) is derived from a `ECardName` via `GetCardType()` and is what `RoundResolver` actually dispatches on.
- Outside: `CardDatabase` maps identity → `CardData` for display name, art, and description at the presentation layer.

Resolution only needs to know *"this is a Joker"* — never what it looks like. Keeping art and flavor text out of the rules is what makes the rules testable.

## What lives here

- `WinLossRules.Judge` (normal-card matchup resolution) in `WinLossRules.cs`.
- `ECardName`, `ECardType`, and the `CardNameExtensions` mapping between them (plus `ToNormalCard()`, the bridge into `WinLossRules`) in `ECardName.cs`.
- `Deck` (deck-top/deck-bottom operations, seeded Fisher-Yates shuffle) in `Deck.cs`.
- `Hand` (add, remove, contains) in `Hand.cs`.
- `DeckAndHand` (draw, return-to-deck-bottom, vanish — the operations spanning both) in `DeckAndHand.cs`.
- `RoundResult` and `ECardFate` (the outcome of one round: revealed cards, each card's fate, win/loss or none, and both players' full post-round hand and deck count) in `RoundResult.cs`. It carries whole hands rather than "what you drew" because Reset replaces both hands outright and Draw adds two extra cards — one drawn card can't describe those rounds. Hands are copied on construction, since `Hand.Cards` is a live view.
- `MatchSession` and `ESide` in `MatchSession.cs` — the authoritative match, host-only. Takes both assembled decks (composition is the caller's job; `CardDatabase` is Godot-side), shuffles, deals the mulligan (`MULLIGAN_HAND_SIZE`). `SubmitCard(side, card)` returns `null` while waiting on the other side and a `RoundReveal` once both are in, since rounds are simultaneous. If that reveal says someone owes a choice, the round waits in `ERoundPhase.AwaitingChoices` for `SubmitChoice` or `DeclineChoice` from every side that does; otherwise the reveal already carries the finished `RoundResult`. Tracks scores, round number, and `Winner` (10 wins). Illegal submissions — match already over, same side twice, card not in hand — throw; `GameState` is expected to catch and drop those rather than pass a malformed client request through. Takes an optional `Action<string>` trace sink (null by default, so tests stay silent); the Godot side passes `GD.Print`, which this project can't call itself.
- `CardChoice` in `CardChoice.cs` — what a player picked so 교체 or 변화 can run. Built through `Transforming` or `Swapping`. It deliberately does **not** name the card it belongs to: the host already knows what it prompted for, and a choice that carried a card would let a client pick which effect runs by shaping its payload.
- `RoundReveal` in `RoundReveal.cs` — both played cards made public, plus who still owes a choice. `Result` is non-null exactly when nobody does.
- `RoundInProgress` and `EChoiceStatus` in `RoundInProgress.cs` — a round that has been revealed but not finished. One object rather than a fistful of nullable fields on `MatchSession`, so ending a round is a single `_round = null` instead of remembering to clear eleven things.
- `ICardEffect` in `Effects/ICardEffect.cs` — `RequiresChoice`, then `Validate(choice, self)` and `Apply(choice, self, opponent, rng)`. Note what an effect is **not** given: the card that was played. It is looked up by card so it already knows which one it is, and withholding it means an effect cannot ask whether its own card is still in hand. It never is, and a bug came from `SwapEffect.Validate` assuming otherwise — taking the card away makes that mistake unrepresentable rather than merely guarded against. Every ability card is implemented in `Effects/`: `ResetEffect`, `SwapEffect`, `TransformEffect`, `DrawEffect`. (`RefillEffect` was one of these until 보충 was taken out of the game; it sits in `Deprecated/` now.) `opponent` is unused by everything except Reset. `Validate` exists so a bad play is rejected before anything mutates.
- `RoundResolver.Reveal` then `RoundResolver.Finish` — one round, two phases, because 교체 and 변화 are chosen after both cards are revealed.
  - `Reveal`: figure out each side's `ECardFate`/win-loss, remove both played cards from their hands (`ApplyFate`), then run every effect that needs **no** choice — 리셋 included. Removing the played card first matters: Reset's own card must not still be "in hand" when Reset asks what's in the hand, or it would get shuffled back into the deck instead of vanishing.
  - `Finish`: apply the choices that came back — always Player 1 then Player 2, **never** the order they arrived in, since 교체 and 리셋 both draw from the shared `Random` — then draw for both sides.
  - **Why choiceless effects run before anyone is prompted:** 리셋 is the only effect that touches the opponent, and it needs no choice. Running it first means nothing can change a hand between the moment its owner is shown it and the moment their choice is applied, so a validated choice can never go stale. Choosing before the reveal let 리셋 invalidate an already-validated choice, which threw partway through resolution and wedged the match.
  - Priority: Joker present → both vanish, no effects run, no win/loss, and **the blocked player is never asked to choose**. Otherwise: Reset runs first if either side played it (twice, Player 1 first, if both did), then any other choiceless Ability, Player 1 before Player 2.
  - `ValidateSubmission` rejects a card not in hand; `ValidateChoice` rejects a choice the effect can't carry out. Neither mutates anything, and `MatchSession` runs each before recording, so a rejected request can't leave a side unable to try again.
  - `RequiresChoice` asks the effect rather than testing card names, so a sixth ability card slots in without editing the resolver.

## Tests

Every rule here should be reachable from `Tests/` (xUnit). Prefer `[Theory]` + `[InlineData]` for the combination matrices this game is full of — matchups, Joker against each ability, priority collisions — one test method covers the whole table. Run with `dotnet test`.

## Sides, not peers

This code knows about `Player1` and `Player2`. It does not know about peer IDs, connections, or who is hosting.

- Translation from peer ID to player side happens in `GameState`, at the boundary.
- Why: peer IDs are a networking concern with networking lifetimes. Letting them in here means the rules can no longer be exercised without a network, which defeats the whole split.

## Determinism

- Shuffles and random draws take an injected RNG (seeded), never a global or ambient one.
- Why: a failing round should be reproducible from its seed. Without that, a resolution bug found in playtesting is a bug you get to see exactly once.
- No `DateTime.Now`, no ambient statics, no reading config at call time. Everything a computation depends on arrives through its parameters or the session it belongs to.

## Results, not side effects

- Resolution returns a `RoundResult`; it doesn't emit signals, update UI, or send anything over the network. `GameState` takes that result and decides what to broadcast, what to send privately, and what the UI hears.
- Why: this is what makes the hidden-information split enforceable. If resolution announced its own outcome, there'd be no single place to decide "this part is public, this part goes only to the player who drew it."
- Validation belongs here too: `MatchSession` decides whether a play is legal (is that card in that player's hand, is the round awaiting their submission). `GameState` calls it and trusts the answer; it doesn't re-implement rules of its own.
