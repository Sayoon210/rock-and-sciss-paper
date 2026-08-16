# GameLogic — Pure Game Logic

`RockAndScissPaper.GameLogic` is a separate .NET project (`Microsoft.NET.Sdk`, no Godot reference). The rules of the match live here: round resolution, scoring, deck/hand operations, special card effects. This is the half of the architecture that `Scripts/Autoload/` owns but does not implement — see [Scripts/Autoload/CLAUDE.md](../Scripts/Autoload/CLAUDE.md) for the other side of the boundary.

Named `GameLogic` rather than `Core` on purpose — `Scripts/Cards/` also holds card-related files (the `CardData` Resource/`.tres` side), and a generic name like `Core` made the two easy to confuse. This folder is specifically the rules; `Scripts/Cards/` is specifically the presentation data.

## The boundary

**No Godot types in this project.** No `Node`, no `Resource`, no `Signal`, no `GD.Print`, no RPC attributes.

This isn't a convention you have to remember — it's enforced by the build. `GameLogic` references nothing, so `using Godot;` fails with `error CS0246` before the code can run. Tests reference `GameLogic` alone, so they never need Godot either.

- The test: a full match should run in a plain console harness with no Godot and no network. If a class here can't, something leaked.
- Why it's worth the discipline: round resolution has real branching (Joker > Reset > other specials > normal; ties; vanish vs. return-to-deck-bottom), and this is the code most likely to be wrong. Verifying it by launching two Godot instances and clicking through rounds is slow enough that it won't happen often. Verifying it as a plain function call is fast enough that it will.
- `Resource` counts as a Godot type even though it needs no scene tree — it still drags in the Godot runtime and breaks the harness test.

### Card identity vs. card presentation

Because `CardData : Resource` can't cross the boundary, game logic deals in plain identity values, not `CardData`:

- Here: `CardName` (Rock / Paper / Scissors / Dummy / Joker / Reset / Swap / Transform / Refill / Foresight / Draw). A deck is a list of those. `CardType` (Normal / Dummy / Joker / Special) is derived from a `CardName` via `GetCardType()` and is what `RoundResolver` actually dispatches on.
- Outside: `CardDatabase` maps identity → `CardData` for display name, art, and description at the presentation layer.

Resolution only needs to know *"this is a Joker"* — never what it looks like. Keeping art and flavor text out of the rules is what makes the rules testable.

## What lives here

- `MatchSession` — the authoritative match: both players' `DeckAndHand`, scores, round number, win condition (5 wins). Instantiated on the host only.
- `ICardEffect` implementations for Transform, Foresight, and Swap — these need a player-chosen parameter (which card, which of 3, which cards to discard) whose delivery path isn't decided yet; see [DevLogDoc/2026-08-17-multiplayer-round-flow-design.md](../DevLogDoc/2026-08-17-multiplayer-round-flow-design.md).

Currently implemented:
- `WinLossRules.Judge` (normal-card matchup resolution) in `WinLossRules.cs`.
- `CardName`, `CardType`, and the `CardNameExtensions` mapping between them (plus `ToNormalCard()`, the bridge into `WinLossRules`) in `CardName.cs`.
- `Deck` (deck-top/deck-bottom operations, shuffle, peek/insert for effects like Foresight) in `Deck.cs`.
- `Hand` (add, remove, contains) in `Hand.cs`.
- `DeckAndHand` (draw, return-to-deck-bottom, vanish — the operations spanning both) in `DeckAndHand.cs`.
- `RoundResult` and `CardFate` (the outcome of one round: revealed cards, each card's fate, win/loss or none, what each player drew) in `RoundResult.cs`.
- `RoundResolver.Resolve` — takes both submitted plays and each player's `DeckAndHand`, applies the outcome (fate, draw) to them directly, and returns the `RoundResult`. Normal/Dummy/Joker only so far; throws `NotImplementedException` for a Special card. No priority queue yet — with no real Special cards wired into it, "Joker present → both vanish, no win/loss; otherwise each card follows its own default fate" covers every case. The real priority ordering (Joker > Reset > other specials > rest) waits for the rest of `ICardEffect`.
- `ICardEffect` (`self`, `opponent`, seeded `rng` — `opponent` unused except by Reset) in `Effects/ICardEffect.cs`, with three of the six special cards implemented: `ResetEffect`, `RefillEffect`, `DrawEffect` in `Effects/`. These three needed no extra input beyond the caster's and (for Reset) the opponent's `DeckAndHand`.

## Tests

Every rule here should be reachable from `Tests/` (xUnit). Prefer `[Theory]` + `[InlineData]` for the combination matrices this game is full of — matchups, Joker against each special, priority collisions — one test method covers the whole table. Run with `dotnet test`.

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
