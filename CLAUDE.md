# RockAndScissPaper — Technical Guide

Godot 4.7 (Mono/C#) card game. 1:1 host-client multiplayer.
Design doc will be added separately later — this document covers tech stack / architecture rules only.

## Rules

- C# only.
- All game rule validation (legal plays, shuffling, win/loss) happens on the host only.
- Hidden information (opponent's hand, deck order) is sent only via targeted RPC to the peer allowed to see it.
- Card/game data is defined as `Resource`-derived classes (e.g. `CardData : Resource`), one `.tres` file per instance.
- Spell every name out in full. No initialisms or shortened forms (`Rps`, `Mgr`, `Cfg`, `Btn`) — a name has to be readable by someone who has never seen this codebase.
- Name a type after what it actually holds or does, not a vague category. `WinLossResult`, not `Outcome`; `WinLossRules`, not `Helper`.
- Use DESIGN.md's own vocabulary verbatim — normal card, dummy, joker, special, vanish, deck bottom. If the design doc and the code call the same thing different names, one of them is wrong.
- Godot script names must reveal their role via suffix: `...Manager` (Autoload service), `...Controller` (drives a node/scene), `...Data` (Resource definition), `...View`/`...UI` (presentation only), `...Effect` (composable behavior), `I...` (interface). File name matches class name.

## Avoid

- GDScript.
- Trusting client-sent values without host-side re-validation.
- Full-syncing hidden information through `MultiplayerSynchronizer` — the default Godot multiplayer pattern, but it exposes everything to every peer.
- Subclass trees for variants (`FireCard : AttackCard : Card`) — use composition (`ICardEffect`) instead.
- Hardcoding card stats/text in scripts.
- Adding abstractions/scaffolding "in case it's needed later."

## Project Structure

Three .NET projects in one solution (`RockAndScissPaper.sln`), net8.0:

```
RockAndScissPaper.csproj      Godot.NET.Sdk — scenes, nodes, UI, networking
  └─ references ─→ GameLogic/ Microsoft.NET.Sdk — pure game rules, references nothing
Tests/                        xUnit — references GameLogic only
```

- Pure game logic goes in `GameLogic/`. Anything touching `Node`, `Resource`, RPC, or the scene tree goes under `Scripts/`.
- `GameLogic` references nothing, so Godot types can't be used there — `using Godot;` fails the build with `error CS0246`. That's the boundary, and it's enforced by the compiler rather than by discipline.
- The Godot `.csproj` excludes `GameLogic/**` and `Tests/**` from its compile glob; without that it would swallow those files and the boundary would silently vanish.
- `dotnet build` builds all three. `dotnet test` runs the suite.

## Multiplayer

- `ENetMultiplayerPeer` + Godot's High-Level Multiplayer API (`[Rpc]`, `MultiplayerSynchronizer`).
- No custom GDExtension/C++ netcode — a turn-based card game doesn't need it.
- Host-authoritative flow: clients send "I want to do X" requests, the host validates against game rules and broadcasts the result.
- This also enforces information asymmetry naturally, since the host decides what each client is told.

## Class Hierarchy & Composition

- Favor interfaces + composition over deep inheritance — define behavior contracts as C# interfaces (`ICardEffect`, `IInteractable`) rather than a new base class per variant.
- Keep Godot node inheritance shallow (at most one custom class between a script and its Godot node base, e.g. `Card : Node2D`).
- Keep pure game logic (plain C# classes, no node dependency) separate from node/scene-bound scripts where practical, for testability and reuse.

## Data Management (Godot Resources)

- `CardDatabase` (Autoload) loads and indexes `.tres` `CardData` resources — it does not define card stats itself.
- Each card is one `.tres` file, giving type-safe, editor-visible data instead of a `Dictionary`/JSON blob.
- The special-card roster is currently fixed (all 6 designed special cards go in every deck), but a future deckbuilding step where players pick a subset from a larger pool is planned (see [DESIGN.md](DESIGN.md)).
- Don't hardcode "there are exactly 6 special cards" into deck-assembly logic — read the special-card set from `CardDatabase` rather than assuming a fixed count/list.

## AI-Assisted Development

- Verify Godot API members against the actual 4.7 C# API before using them — training data mixes GDScript examples and older Godot versions, so a hallucinated or outdated method/property name is a real risk here.
- Test multiplayer RPC/authority behavior with two running instances rather than assuming it from memory of docs.
- This project has the `godot-mcp` MCP server configured (local, stdio, via `npx @coding-solo/godot-mcp`) with `GODOT_PATH` pointing at the local Godot 4.7 Mono install.
- It gives Claude direct control over the Godot editor — launching the editor/project, capturing debug output, creating nodes/scenes.

## Git Commits

- Commits in this project are made via the global `commit` skill (`~/.claude/skills/commit/`, invoked as `/commit`).
- It enforces Conventional Commits prefix, a detailed body, and a mandatory "why".
- Use it rather than writing a generic commit message by hand.
