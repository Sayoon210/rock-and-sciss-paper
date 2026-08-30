# Scripts — The Godot Layer

Everything that needs Godot lives here: nodes, scenes, `Resource` definitions, RPC, input, UI. The rules of the game do not — those are in [`GameLogic`](../GameLogic/CLAUDE.md), which cannot reference Godot at all.

Subfolders: `Autoload/` (global services, see [its own CLAUDE.md](Autoload/CLAUDE.md)), `Cards/`, `Match3D/`, `Network/`, `UI/`. What is in each and how they reference one another is [ARCHITECTURE.md](../ARCHITECTURE.md); this file is the rules they follow.

## What this layer is for

- Translating player input into an intent, and handing it to `GameState`.
- Translating a `GameLogic` result into something the screen and the network can carry.
- Deciding what is public and what is private, and sending each accordingly.

It does not decide whether a play is legal, who won a round, or what a card does. If a script here is answering one of those, the logic belongs in `GameLogic` instead.

## Text the player reads

**No source file contains a sentence a player reads.** Source carries a symbol —
`text = "TITLE_PLAY"`, `DisplayName = "CARD_JOKER_NAME"` — and `Data/Translations/strings.csv`
carries every language, `keys,en,ko`. English is a translation like any other; it has no
privileged position in the files.

Godot picks the locale from the player's OS, and `internationalization/locale/fallback="en"`
catches everything else — a French player gets the English column, not a screen full of
`TITLE_PLAY`. There is no language switcher and none is needed.

### The symbols

`AREA_THING`, SCREAMING_SNAKE_CASE, words spelled out (`_DESCRIPTION`, never `_DESC`). Five
areas: `CARD_` (a card's own name and rules text), `CARD_TYPE_` (the 카드 종류 badge), `TITLE_`,
`CONNECT_`, `MATCH_`. The `MATCH_` group subdivides — `MATCH_PROMPT_`, `MATCH_OUTCOME_`,
`MATCH_ACTION_`, `MATCH_END_`, `MATCH_LOG_`.

The two halves cost very different amounts to change, which is the point of doing it this way:

- **The English or Korean wording** is one cell in the CSV. Nothing recompiles. Reword freely.
- **A symbol** is a rename across source. Worth getting right when it is added.

Adding or changing a string means three things, and skipping the third is silent:

1. Put the symbol in the source file — the `.tscn`, the card's `.tres`, or the literal.
2. Add a row to the CSV with both languages.
3. Re-import, so Godot regenerates the `.translation` files beside the CSV:
   `godot --headless --path . --import`. Both are committed; `project.godot` names both.

### Where the translation actually happens

`Node.AutoTranslateMode` is `Always` on the root and inherited everywhere below, so a `Label` or
`Button` translates its own text — whether the text was authored in the scene or assigned from
code. Two rules follow:

- **A plain string: just assign it.** `_promptLabel.Text = "Choice sent...";` is already
  translated. Wrapping it in `Tr()` does nothing except make it look like the ones that need it.
- **A string with a value in it: translate the template, then fill it.**
  `string.Format(Tr("Round {0}"), n)`. The composed string is not a key, so leaving it to
  auto-translation means it is never looked up. In a `static` method use
  `TranslationServer.Translate(...)`, which is what `Tr()`'s own docs point to.

### When a symbol is missing

A symbol with no CSV row shows up on screen as the symbol itself — `MATCH_PROMPT_SWAPP` in
capitals, impossible to miss. That is the whole reason this is better than keying on the English
text, where a typo produced a screen that looked perfectly fine in English and silently stopped
translating.

Placeholder text in a scene is the one exception to "no sentences in source" — a `Label` that
reads `Me` or `Round` in the .tscn, which code overwrites before a player ever sees it. It is
there so the Godot editor shows a laid-out screen instead of a column of capitals, and it is
deliberately not a symbol and not in the CSV.

## Input

- Input handling lives in the node that owns it — the card node for a card, the relevant `Control` for a button. There is no central `InputManager`; Godot's `InputMap` and node-tree input routing already do that job.
- A node that receives input does not judge it. It recognises the gesture and passes an intent to `GameState`; it never checks whether the play is legal.
- 3D picking is off by default. `Viewport.PhysicsObjectPicking` has to be true or **no** `Area3D` mouse signal fires at all. Set it once on the scene root — it is one switch for the whole viewport, so no individual pickable should be the one deciding it for every other.
- Greying out an unplayable card is fine as a UI affordance, but it is not validation. The host re-validates everything.
- Input code must not branch on host vs. client. That branch belongs in `GameState` alone.

## Reading match state

**Read `GameState.View`, never `GameState._session` — on the host too.**

The host's process holds both players' real hands in memory, so nothing but this rule stops a host-side UI script from reading the opponent's hand. On a client the network makes that impossible; on the host only discipline does.

`View` is shaped as "me / opponent", not "Player1 / Player2":

```
View
├─ MyHand : List<ECardName>   ← always my real hand
├─ OpponentHandCount : int    ← count only, never contents
└─ …everything else public: counts, healths, this round's revealed cards
```

The asymmetry in those first two lines is the whole point of the type: my own side is spelled out, the opponent's is reduced to what I am allowed to know. A field that gives the opponent's *contents* does not belong in it.

Both sides fill `View` differently — the host copies from its session in-process, a client fills it from RPC — but the shape and the UI code reading it are identical.

## Card presentation

- `ECardName` is the only card type that crosses from `GameLogic`. Resolve it to a `CardData` through `CardDatabase` at the point of display.
- Hand order in `GameLogic` is meaningless; the rules never assign a card a slot. Screen-side slot stability is this layer's business — rebuilding the whole row on every change throws away which node the player was looking at.
- `Control`-only affordances do not exist in 3D — `_MakeCustomTooltip`/`_GetTooltip` among them. When one is wanted, it is a design job rather than a port; do not reach for the `Control` contract.
- A choice a card asks for (교체, 변화) is asked **after** both cards are revealed, through `GameState.RequestChoice`, and only ever on the screen of the player who owes it. A card node therefore never needs to know whether the card it shows needs a choice.
- Where the gesture thresholds and the current state of that flow are is [ARCHITECTURE.md](../ARCHITECTURE.md).

## Debug tracing into GameLogic

`GameLogic` cannot call `GD.Print`. Types there that support tracing take an injected sink instead, so pass `GD.Print` in from this side:

```csharp
_session = new MatchSession(player1Deck, player2Deck, rng, GD.Print);
```

Leave the argument off to disable tracing. Don't add `Console.WriteLine` inside `GameLogic` to work around this — it makes that project emit output on its own, which is the thing the sink exists to avoid.
