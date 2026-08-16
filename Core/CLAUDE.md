# Core — Pure Game Logic

`RockAndScissPaper.Core` is a separate .NET project (`Microsoft.NET.Sdk`, no Godot reference). The rules of the match live here: round resolution, scoring, deck/hand operations, special card effects. This is the half of the architecture that `Scripts/Autoload/` owns but does not implement — see [Scripts/Autoload/CLAUDE.md](../Scripts/Autoload/CLAUDE.md) for the other side of the boundary.

## The boundary

**No Godot types in this project.** No `Node`, no `Resource`, no `Signal`, no `GD.Print`, no RPC attributes.

This isn't a convention you have to remember — it's enforced by the build. `Core` references nothing, so `using Godot;` fails with `error CS0246` before the code can run. Tests reference `Core` alone, so they never need Godot either.

- The test: a full match should run in a plain console harness with no Godot and no network. If a class here can't, something leaked.
- Why it's worth the discipline: round resolution has real branching (Joker > Reset > other specials > normal; ties; vanish vs. return-to-deck-bottom), and this is the code most likely to be wrong. Verifying it by launching two Godot instances and clicking through rounds is slow enough that it won't happen often. Verifying it as a plain function call is fast enough that it will.
- `Resource` counts as a Godot type even though it needs no scene tree — it still drags in the Godot runtime and breaks the harness test.

### Card identity vs. card presentation

Because `CardData : Resource` can't cross the boundary, game logic deals in plain identity values, not `CardData`:

- Here: `CardKind` (Rock / Paper / Scissors / Dummy / Joker / Special) and, for specials, which special it is. A deck is a list of those.
- Outside: `CardDatabase` maps identity → `CardData` for display name, art, and description at the presentation layer.

Resolution only needs to know *"this is a Joker"* — never what it looks like. Keeping art and flavor text out of the rules is what makes the rules testable.

## What lives here

- `MatchSession` — the authoritative match: both players' zones, scores, round number, win condition (5 wins). Instantiated on the host only.
- `RoundResolver` — takes both submitted plays, returns what happened. No mutation of its inputs.
- `PlayerZone` — one player's deck and hand: draw, return-to-deck-bottom, vanish, shuffle.
- `RoundResult` — a plain value object describing the outcome: both cards revealed, each card's fate, score delta, what each player drew.
- `ICardEffect` implementations — one per special card (Reset, Swap, Change, Refill, Foresight, Draw), composed rather than subclassed, per the root convention.

Don't split a class out until it's carrying its weight. `PlayerZone` may well start as two fields on `MatchSession` and only become its own type once that gets unwieldy.

Currently implemented: `WinLossRules.Judge` (normal-card matchup resolution) in `WinLossRules.cs`.

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
