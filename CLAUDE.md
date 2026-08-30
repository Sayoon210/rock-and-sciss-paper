# RockAndScissPaper — Technical Guide

Godot 4.7 (Mono/C#) card game. 1:1 host-client multiplayer.

**This file is rules only — things that stay true while the game changes under them.**
Nothing here should need editing because a card left the deck, a class was renamed, or a
threshold was retuned. If a statement would go stale on a normal day's work, it belongs in
one of these instead:

| | |
|---|---|
| What currently exists and how it connects | [ARCHITECTURE.md](ARCHITECTURE.md) |
| The game's rules and content | [DESIGN.md](DESIGN.md) |
| Not-yet-decided extensions | [IDEAS.md](IDEAS.md) |
| Decisions as they were made | `DevLogDoc/` |

The split matters because this file is loaded as instructions on every task. A stale rule here
does not sit quietly the way a stale paragraph in a design doc does — it gets followed.

## Rules

- C# only, with one exception: shaders. `Shaders/*.gdshader` is Godot Shading Language,
  which has no C# equivalent — a per-pixel effect cannot be expressed any other way.
  Keep them to how something is drawn; no game rule ever lives in a shader.
- All game rule validation (legal plays, shuffling, win/loss) happens on the host only.
- Hidden information (opponent's hand, deck order) is sent only via targeted RPC to the peer allowed to see it.
- Card/game data is defined as `Resource`-derived classes (e.g. `CardData : Resource`), one `.tres` file per instance.
- **Anything the player reads is a symbol in source, never a sentence** — `text = "TITLE_PLAY"`, `DisplayName = "CARD_JOKER_NAME"`, `Tr("MATCH_ROUND")`. Both languages live in `Data/Translations/strings.csv`. See [Scripts/CLAUDE.md](Scripts/CLAUDE.md) for the naming scheme and how to add one.
- That is about the *product's* language, not the codebase's. Identifiers, comments and [DESIGN.md](DESIGN.md) keep the Korean game vocabulary (교체, 리셋, 패, 소멸) — the rule below is unchanged.
- Spell every name out in full. No initialisms (`Rps`, `Mgr`, `Cfg`, `Btn`).
- Name a type after what it holds or does, not a vague category (`WinLossRules`, not `Helper`).
- Use [DESIGN.md](DESIGN.md)'s vocabulary verbatim. If the design doc calls it 소멸, the code does not call it discard.
- Godot script names reveal role via suffix: `...Manager`, `...Controller`, `...Data`, `...View`/`...UI`, `...Effect`, `I...`. File name matches class name.
- A plain `enum` is prefixed `E...` (`ECardName`, `ESide`, `ERoundOutcome`) — the same reasoning as `I...` for interfaces: the name alone says the type carries no behavior, so a reader isn't left checking whether "CardName" is a value type or a class with logic on it. Applies to the enum only, never to a property or field of that type — `CardData.CardName` stays `CardName` (it's what `Data/Cards/*.tres` key their value on), even though its type is `ECardName`.
- Name `const` members in `SCREAMING_SNAKE_CASE` (`MULLIGAN_HAND_SIZE`, `MAX_CLIENTS`) — a deliberate departure from the C#/Godot PascalCase convention, so a constant is distinguishable from a property at the call site. Don't "correct" these back.
- Write the plain form of a C# construct, not the compressed one: full `{ }`/`return` bodies, not expression-bodied `=>` members or switch expressions.

## Avoid

- GDScript.
- Trusting client-sent values without host-side re-validation.
- Full-syncing hidden information through `MultiplayerSynchronizer`.
- Subclass trees for card variants (`ResetCard : AbilityCard : Card`) — cards are identified by `ECardName`, ability behavior composed via `ICardEffect`.
- Hardcoding card stats/text in scripts.
- Committing a third-party asset without a row in [ATTRIBUTIONS.md](ATTRIBUTIONS.md) — where it came from is unrecoverable later.
- Adding abstractions/scaffolding "in case it's needed later."

## Project Structure

Three .NET projects in one solution (`RockAndScissPaper.sln`), net8.0:

```
RockAndScissPaper.csproj      Godot.NET.Sdk — scenes, nodes, UI, networking
  └─ references ─→ GameLogic/ Microsoft.NET.Sdk — pure game rules, references nothing
Tests/                        xUnit — references GameLogic only
```

`Deprecated/` is outside every compile glob — nothing in it builds, and it is excluded from
reviews unless asked for by name. See its own README for what is in there and why.

- Pure game logic goes in `GameLogic/`; anything touching `Node`, `Resource`, RPC, or the scene tree goes under `Scripts/`.
- `GameLogic` references nothing — `using Godot;` fails to compile there (`error CS0246`).
- The Godot `.csproj` excludes `GameLogic/**` and `Tests/**` from its compile glob.
- `dotnet build` builds all three. `dotnet test` runs the suite.

## Multiplayer

- `ENetMultiplayerPeer` + Godot's High-Level Multiplayer API (`[Rpc]`, `MultiplayerSynchronizer`).
- No custom GDExtension/C++ netcode.
- Host-authoritative flow: clients send "I want to do X" requests, the host validates and broadcasts the result.

## Class Hierarchy & Composition

- Favor interfaces + composition over deep inheritance (`ICardEffect`, `IInteractable`).
- Keep Godot node inheritance shallow — at most one custom class between a script and its node base (e.g. `Card : Node2D`).
- Keep pure game logic (plain C#, no node dependency) separate from node/scene-bound scripts.

## Data Management (Godot Resources)

- `CardDatabase` (Autoload) loads and indexes `.tres` `CardData` resources; it does not define card stats itself.
- One `.tres` file per card.
- Don't hardcode a card roster into deck-assembly logic — read the set from `CardDatabase`, so growing the pool needs no edit there (deckbuilding is planned, see [DESIGN.md](DESIGN.md)).
- **One source of truth for what is in the deck.** Anything that needs to know which cards exist in play asks `DeckAssembler`, not `CardDatabase` — the database holds every card that has ever been designed, which is a larger set.

## AI-Assisted Development

- Verify Godot API members against the actual 4.7 C# API before using them.
- Test multiplayer RPC/authority behavior with two running instances, not from memory.
- `godot-mcp` MCP server is configured (local, stdio, `GODOT_PATH` → local Godot 4.7 Mono install) for direct editor control.

## Git Commits

- Commit via the global `commit` skill (`~/.claude/skills/commit/`, `/commit`) — Conventional Commits prefix, detailed body, mandatory "why".
