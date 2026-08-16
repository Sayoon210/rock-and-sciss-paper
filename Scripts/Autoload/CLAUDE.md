# Scripts/Autoload — Global Singletons

Autoload scripts are the project's global services: registered once in `project.godot`, alive for the whole session, reachable from any scene. That reach is exactly why they turn into dumping grounds if left unchecked — these rules exist to keep each one narrow enough to reason about.

## What belongs here

- One clear, single responsibility per singleton — if you can't name it in a few words without "and", it's two singletons.
- State or services genuinely shared across multiple scenes. Anything a single scene owns stays in that scene's own script.
- This is not a closed list; new Autoloads are fine when they meet the bar above.

## Autoload vs. game logic

The rules of the game do not live here. **Autoloads own and relay; the plain C# classes they own know the rules.**

- `GameState` (Autoload) — owns the current match object, resets it between matches, receives network calls, emits signals for the UI.
- `MatchSession`, `RoundResolver`, etc. (plain C# in the separate [`GameLogic`](../../GameLogic/CLAUDE.md) project) — round resolution order, scoring, deck/hand/vanish operations. No `Node`, no RPC, no scene tree.

The test for whether the split is right: **a full match should be runnable in a plain console harness with no Godot and no network.** If it isn't, logic has leaked into the Autoload.

Why this matters for this project specifically:
- Host-authoritative means the rules only ever execute on one side, but an Autoload exists on *both* host and client. A singleton that *is* the rules ends up half-real on clients — a reliable source of confusion and desync. A singleton that merely *holds* an authoritative session on the host and a received view on the client stays honest about which side it's on.
- Round resolution has real branching (Joker > Reset > other specials > normal; ties; vanish vs. return-to-deck-bottom). Verifying that by launching two Godot instances is slow. Verifying it as a plain function call is not.

## Registration

- Register in `project.godot` under `[autoload]`, pointing at the `.cs` file.
- Registration order is initialization order — Autoloads enter the tree top to bottom. If one must exist before another (e.g. `CardDatabase` loaded before anything can query cards), it goes earlier in the list.
- Extend `Node`, not `Node2D`/`Control`, unless the singleton actually renders something. A plain `Node` carries no transform or canvas overhead.

## Access pattern

- Expose a static `Instance` property set in `_EnterTree()`, rather than making callers write `GetNode<T>("/root/Name")`:

  ```csharp
  public partial class NetworkManager : Node
  {
      public static NetworkManager Instance { get; private set; }

      public override void _EnterTree()
      {
          Instance = this;
      }
  }
  ```

- Why: the string path is unchecked — a typo or a rename silently fails at runtime, while `Instance` is caught by the compiler. Setting it in `_EnterTree()` (not `_Ready()`) means it's available before any other node's `_Ready()` runs.

## Structure

- Keep the Autoload thin. It coordinates and holds references; the actual rules live in plain C# classes in the `GameLogic` project that it delegates to.
- Why: a `Node`-derived singleton is awkward to test and drags the scene tree into logic that doesn't need it. Pushing logic into plain classes keeps it testable and reusable, and keeps the singleton small enough to read in one sitting.
- Public surface should be intent-shaped (`RequestCardPlay`, `StartMatch`), not a bag of public fields other code mutates directly.

## Communication

- Outbound: emit signals. Let interested scenes connect to them.
- Inbound: accept method calls.
- Autoloads must not hold direct references to each other — that's how four small singletons become one tangled god-object with an unclear startup order. Route cross-cutting notifications through `EventBus` instead.
- Scenes connecting to Autoload signals must disconnect on exit (or use `CONNECT_ONE_SHOT` where it fits). A freed node still connected to a session-lifetime signal is a crash waiting for the next emit.

## Lifecycle & reset

- Autoloads outlive scene changes, so anything match-scoped must be explicitly cleared — `GameState` needs a reset function called at the start of each match.
- Why: leftover state from a finished match silently leaking into the next one is the failure mode here, and it surfaces as a bug that only reproduces on the second match.
- Distinguish session-lifetime data (loaded card definitions, connection settings) from match-lifetime data (scores, hands, turn order). Only the latter resets.

## Multiplayer

**Autoloads load on every instance of the game — there is no such thing as a host-only Autoload.** They're registered in `project.godot` and instantiated at startup, long before anyone clicks "Host" or "Join", so conditional registration isn't possible even in principle. Authority is a runtime branch, not a loading decision.

- Let the type system carry the asymmetry. `GameState` owns a `MatchSession` on the host and leaves it `null` on clients, holding only a received view:

  ```csharp
  private MatchSession _session;   // host: the authoritative match. client: null, always.
  public MatchView View { get; private set; } = new();   // both sides, display only
  ```

- Why: a null reference on a client that tried to run the rules fails loudly and immediately. The alternative — both sides holding a "real" session and quietly diverging — surfaces as a desync several rounds later with no obvious cause.
- `NetworkManager` owns the peer and the multiplayer lifecycle; nothing else creates or replaces `Multiplayer.MultiplayerPeer`.
- Route every player action through one entry point regardless of side, so the host isn't a special case in the caller. On the host that call resolves locally; on a client it's an RPC to peer 1. The caller shouldn't care which.
- Split broadcasts by visibility. Public outcomes (cards revealed, scores, hand *counts*) go to everyone; anything the opponent must not learn (what you drew, what's in your hand) goes out as a targeted RPC to that peer alone. Getting this wrong leaks the whole hand — see the root [CLAUDE.md](../../CLAUDE.md).

## Naming

- Name by role: `...Manager` for lifecycle/connection services, `...Database` for read-only data lookup, `...State` for mutable state holders, `EventBus` for the signal relay.
- The file name matches the class name, per the root convention.
