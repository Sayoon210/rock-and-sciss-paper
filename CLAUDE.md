# RockAndScissPaper — Technical Guide

Godot 4.7 (Mono/C#) card game. 1:1 host-client multiplayer.
Design doc will be added separately later — this document covers tech stack / architecture rules only.

## Rules

- C# only, with one exception: shaders. `Shaders/*.gdshader` is Godot Shading Language,
  which has no C# equivalent — a per-pixel effect cannot be expressed any other way.
  Keep them to how something is drawn; no game rule ever lives in a shader.
- All game rule validation (legal plays, shuffling, win/loss) happens on the host only.
- Hidden information (opponent's hand, deck order) is sent only via targeted RPC to the peer allowed to see it.
- Card/game data is defined as `Resource`-derived classes (e.g. `CardData : Resource`), one `.tres` file per instance.
- Spell every name out in full. No initialisms (`Rps`, `Mgr`, `Cfg`, `Btn`).
- Name a type after what it holds or does, not a vague category (`WinLossRules`, not `Helper`).
- Use DESIGN.md's vocabulary verbatim — normal card, dummy, joker, special, vanish, deck bottom.
- Godot script names reveal role via suffix: `...Manager`, `...Controller`, `...Data`, `...View`/`...UI`, `...Effect`, `I...`. File name matches class name.
- Name `const` members in `SCREAMING_SNAKE_CASE` (`MULLIGAN_HAND_SIZE`, `MAX_CLIENTS`) — a deliberate departure from the C#/Godot PascalCase convention, so a constant is distinguishable from a property at the call site. Don't "correct" these back.
- Write the plain form of a C# construct, not the compressed one: full `{ }`/`return` bodies, not expression-bodied `=>` members or switch expressions.

## Avoid

- GDScript.
- Trusting client-sent values without host-side re-validation.
- Full-syncing hidden information through `MultiplayerSynchronizer`.
- Subclass trees for card variants (`ResetCard : SpecialCard : Card`) — cards are identified by `CardName`, special behavior composed via `ICardEffect`.
- Hardcoding card stats/text in scripts.
- Adding abstractions/scaffolding "in case it's needed later."

## Project Structure

Three .NET projects in one solution (`RockAndScissPaper.sln`), net8.0:

```
RockAndScissPaper.csproj      Godot.NET.Sdk — scenes, nodes, UI, networking
  └─ references ─→ GameLogic/ Microsoft.NET.Sdk — pure game rules, references nothing
Tests/                        xUnit — references GameLogic only
```

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
- Don't hardcode the special-card count or roster into deck-assembly logic — read the special-card set from `CardDatabase` (deckbuilding/pool expansion planned, see [DESIGN.md](DESIGN.md)).

## AI-Assisted Development

- Verify Godot API members against the actual 4.7 C# API before using them.
- Test multiplayer RPC/authority behavior with two running instances, not from memory.
- `godot-mcp` MCP server is configured (local, stdio, `GODOT_PATH` → local Godot 4.7 Mono install) for direct editor control.

## Git Commits

- Commit via the global `commit` skill (`~/.claude/skills/commit/`, `/commit`) — Conventional Commits prefix, detailed body, mandatory "why".
