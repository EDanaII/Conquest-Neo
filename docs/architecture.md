# Conquest Architecture

## Overview

Conquest reimplements a legacy C text strategy game with modular C#. Phase 1, **Conquest Classic**, preserves original gameplay with a CLI. Phase 2, **Conquest Modernized**, permits improvements after a working, tested Classic baseline. See [AGENTS.md](../AGENTS.md).

The intended boundary is UI-independent game logic in Core, with input and presentation in front ends. The current implementation only partially realizes this boundary.

## Modules

### Conquest.Core

- `Game`: engine facade holding RNG/state; generates stars, finds stars by coordinates, and advances fleet movement and the turn counter.
- `GameState`: configuration, stars, two players, board sectors, visibility state, and lazily constructed `FleetSystem`.
- `Player`, `TaskForce`, `Star`, `Planet`, `Sector`, `BoardPoint`: current state models. Stars own planet collections; planets retain their parent star, ownership, population, IU, defenses, and intelligence fields. `Ownership` matches C's enemy/player/none values. Player stores fleets, technology levels, population growth, and research progress; colony, economy, and research operations remain absent.
- `Game.InitializeClassicPlayers()`: explicit Core initialization after successful `Game.GenerateStars()`. Requires fresh player/fleet state, no exploration or colonization, and no elapsed turns; rejects repeat initialization even through another `Game` facade. Establishes original attributes, configured starting transports, and the two enemy-advantage draws. Does not place fleets, grant enemy warships, make research decisions, spend player funds, or advance turns. The CLI does not invoke it automatically. A failed initialization attempt after RNG consumption requires a new state.
- `FleetSystem`: placement, courses, Manhattan ETA/movement, and occupancy updates. No original range/velocity rules, combat, encounters, or arrival exploration.
- `GameConfig` and `Constants`: configuration parsing/validation and selected original constants. Some settings have no implemented gameplay consumer.
- `CRandRng`: deterministic LCG implementing `IRng`, `ISeedableRng`, and `ICRandRng`. Original sequence equivalence is not established.
- `SaveSystem`: JSON snapshot prototype for selected configuration, fog, and star fields. It omits substantial game state and does not rebuild board star links on load.
- `CommandRouter` and `GenStarsHandler`: separate command infrastructure not wired into `GameShell`. The handler prints coordinates without updating active game state.
- Star placement exists in `Game.GenerateStars`, `GameState.PlaceStars`, and the separate spacing-aware `StarGenerator`. The interactive shell uses `Game.GenerateStars`, enforcing uniqueness but not spacing and generating planets immediately after each star using the original C draw order. The other generation paths do not initialize planets or satisfy the Classic initialization prerequisite. Both state-mutating placement APIs reject regeneration once Classic player initialization starts, before changing state or consuming random draws; this guard belongs to the shared `GameState`.
- `MapRenderer` and `SectorView`: additional presentation adapters with behavior differing from the active CLI renderer.

There are no implemented `Parser`, `CombatSystem`, or `MovementSystem` classes. Earlier documentation described intended systems as existing code.

### Conquest.CLI

`Program` parses configuration and constructs `CRandRng`, `GameState`, CLI `MapRenderer`, `Game`, and `GameShell`. It then runs the shell without automatically generating stars.

`GameShell` parses commands using a switch, calls engine operations, directly changes visibility state, and accesses `State.FleetsSys`. It also embeds a separate `tf map` renderer. It currently has more responsibilities than a minimal command adapter.

`ConsoleIO` implements `IGameIO`. `SystemRng` is an alternative adapter, not selected by `Program`.

### Conquest.Tests

xUnit tests reference Core and CLI. `DummyIO` scripts commands/captures output; `SpyRenderer` checks renderer invocation. Tests cover configuration, RNG bounds, star structure, router dispatch, shell smoke sequences, fog flags, basic fleets, and selected snapshot fields.

There are no comprehensive Classic acceptance tests. Renderer-call tests do not verify real rendering, and snapshot tests do not establish restored-game usability.

### OriginalSource

Primary gameplay reference. The original loop performs player input, enemy decisions, movement, combat, periodic investment, annual updates, and game-over checks. A reproducible legacy build remains an investigation task; the .NET solution does not compile C.

### Graphics (future)

No graphics project or `IRenderer`/`IInput`/`IAudio` abstraction set is implemented. MonoGame, Godot, and Avalonia remain Phase 2 possibilities, not selected dependencies. Preserve UI-independent rules during Classic work.

## Key interfaces

- `IGameIO`: `ReadLine()`, `Write(string)`, `WriteLine(string = "")`.
- `IRng`: `Next(minInclusive, maxExclusive)` and `Reseed(seed)`.
- `ISeedableRng`: extends `IRng` with `Seed(int)`.
- `ICRandRng`: separate `Seed(uint)` and `NextInt(maxExclusive)` contract.
- `IMapRenderer`: `Render(IGameIO, GameState)`.

The shell checks for null input, but `IGameIO.ReadLine` is non-nullable and `ConsoleIO` converts EOF to an empty string. That contract mismatch needs correction.

## Known inconsistencies and boundaries to resolve

- Sectors duplicate indices and object references. Board/stars are zero-based; fleet slots are 1-based. Some adapters still assume old conventions.
- CLI rendering consumes `ExploredBy`/`FogEnabled`; Core rendering consumes `Visible`/`RevealStars`. Discovery/reveal commands update the latter model and do not affect the active CLI map.
- Per-cell occupancy holds only one fleet per team, although fleets can share coordinates.
- Mutable configuration and public collections allow invalidation of allocation and cross-references.
- RNG and state are supplied separately to shell/engine, allowing inconsistent wiring.

These are current limitations, not approved redesigns. Discuss significant state, occupancy, command-routing, visibility, or persistence decisions before implementation. See the [backlog](DevelopmentBacklog.md).
