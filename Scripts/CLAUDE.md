# Scripts — The Godot Layer

Everything that needs Godot lives here: nodes, scenes, `Resource` definitions, RPC, input, UI. The rules of the game do not — those are in [`GameLogic`](../GameLogic/CLAUDE.md), which cannot reference Godot at all.

Subfolders: `Autoload/` (global services, see [its own CLAUDE.md](Autoload/CLAUDE.md)), `Cards/` (card nodes and `CardData` resources), `Network/`, `UI/`.

## What this layer is for

- Translating player input into an intent, and handing it to `GameState`.
- Translating a `GameLogic` result into something the screen and the network can carry.
- Deciding what is public and what is private, and sending each accordingly.

It does not decide whether a play is legal, who won a round, or what a card does. If a script here is answering one of those, the logic belongs in `GameLogic` instead.

## Input

- Input handling lives in the node that owns it — `CardController` for a card, the relevant `Control` for a button. There is no central `InputManager`; Godot's `InputMap` and node-tree input routing already do that job.
- A node that receives input does not judge it. `CardController` detects the click and calls `GameState.Instance.RequestCardPlay(cardName)`; it does not check whether the card is playable.
- Greying out an unplayable card is fine as a UI affordance, but it is not validation. The host re-validates everything.
- Input code must not branch on host vs. client. That branch belongs in `GameState` alone.

## Reading match state

**Read `GameState.View`, never `GameState._session` — on the host too.**

The host's process holds both players' real hands in memory, so nothing but this rule stops a host-side UI script from reading the opponent's hand. On a client the network makes that impossible; on the host only discipline does.

`View` is shaped as "me / opponent", not "Player1 / Player2":

```
View
├─ MyHand : List<CardName>       ← always my real hand
├─ OpponentHandCount : int       ← count only, never contents
├─ MyDeckCount / OpponentDeckCount
└─ this round's revealed cards, scores
```

Both sides fill `View` differently — the host copies from its session in-process, a client fills it from RPC — but the shape and the UI code reading it are identical.

## Card presentation

- `CardName` is the only card type that crosses from `GameLogic`. Resolve it to a `CardData` through `CardDatabase` at the point of display.
- Hand order in `GameLogic` is meaningless; the rules never assign a card a slot. Screen-side slot stability is this layer's business — rebuilding the whole row on every change throws away which node the player was looking at.
- Playing a card is always a single click, 교체 and 변화 included. Their choice is asked for **after** both cards are revealed, through `GameState.RequestChoice`, and only ever on the screen of the player who owes it. A card node therefore does not need to know whether the card it shows needs a choice.

## Debug tracing into GameLogic

`GameLogic` cannot call `GD.Print`. Types there that support tracing take an injected sink instead, so pass `GD.Print` in from this side:

```csharp
_session = new MatchSession(player1Deck, player2Deck, rng, GD.Print);
```

Leave the argument off to disable tracing. Don't add `Console.WriteLine` inside `GameLogic` to work around this — it makes that project emit output on its own, which is the thing the sink exists to avoid.
