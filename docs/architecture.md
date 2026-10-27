# Conquest Architecture

## Overview

Conquest reimplements a legacy C text strategy game with modular C#. Phase 1, **Conquest Classic**, preserves original gameplay with a CLI. Phase 2, **Conquest Modernized**, permits improvements after a working, tested Classic baseline. See [AGENTS.md](../AGENTS.md).

The intended boundary is UI-independent game logic in Core, with input and presentation in front ends. The current implementation only partially realizes this boundary.

## Modules

### Conquest.Core

- `Game`: engine facade holding RNG/state; generates stars, finds stars by coordinates, and advances fleet movement and the turn counter.
- `GameState`: configuration, stars, two players, board sectors, visibility state, and lazily constructed `FleetSystem`.
- `Player`, `TaskForce`, `Star`, `Planet`, `Sector`, `BoardPoint`: current state models. Stars own planet collections; planets retain their parent star, ownership, population, IU, defenses, and intelligence fields. `Ownership` matches C's enemy/player/none values. Player stores fleets, technology levels, population growth, and research progress; colony, economy, and general research operations remain absent.
- `Game.InitializeClassicPlayers()`: explicit Core initialization after successful `Game.GenerateStars()`. Requires fresh player/fleet state, no exploration or colonization, and no elapsed turns; rejects repeat initialization even through another `Game` facade. Establishes original attributes, configured starting transports, and the two enemy-advantage draws. Does not place fleets, grant enemy warships, make research decisions, spend player funds, or advance turns. The CLI does not invoke it automatically. A failed initialization attempt after RNG consumption requires a new state.
- `Game.InitializeClassicEnemyResearch()`: explicit next stage requiring successful completion of player initialization, not merely its started flag. `ResearchRules` applies the two-unit velocity investment without a breakthrough and selects the enemy's next `ResearchField` using the original strict comparisons, integer division, and conditional RNG draws. `GameState.EnemyResearchField` starts unset. Repeat calls are blocked by shared state across facades, including after a research RNG failure. This operation does not grant warships, select a starting star, run economy logic, or interact with the CLI. Only startup velocities 1..3 and zero enemy research progress are supported.
- `Game.InitializeClassicEnemy()`: coordinates the remaining original `initmach()` sequence after successful player initialization: assign fleet 1 one cruiser and two scouts, invoke startup research once, then score and select its starting star. Rejects empty galaxies, existing fleet/location activity, invalid board links, elapsed turns, independent research, and repeat calls before mutation or RNG consumption. Shared started/completed flags distinguish failures from successful placement; failures require a new state. `Tick()` records that turns have advanced in shared state so a new facade cannot bypass this prerequisite; existing turn counting and movement remain unchanged. `EnemyStartingStarRules` counts all stars (including self) within inclusive Euclidean range, adds one 1..5 draw per candidate in generated order, and retains the first tied maximum. The CLI does not invoke this operation automatically.
- `Game.BeginClassicPlayerStartup()`: Core-only `init_player()` session after successful enemy initialization. `GameState` owns its stage, selected star, spending balance, and pending shared-start encounter. Capacity zero requires `SelectClassicPlayerStartingStar(index)`; any generated star is allowed. Positive capacity uses `PlayerStartupRules` to repeatedly draw stars in the original order and lower the threshold by 10 after the post-increment retry test (`N+2` samples), without modifying configuration. Sampling failures leave a terminal failed session; a facade cannot restart it.
- `PurchaseClassicStartupShips` and `InvestClassicStartupResearch` operate only at the selected stationary location. Ships cost 16/6/70 for cruiser/scout/battlestar. Unaffordable requests return false without partial effects; malformed quantities, enum values, and signed-overflow cases throw before mutation. Player-only research uses all original requirement tables, progress resets, maxima, and multiple breakthroughs, without RNG or enemy decisions. The full affordable research amount is charged even beyond the cap. Remaining money is shared across facades. `FinalizeClassicPlayerStartup()` requires zero balance (including an initially zero budget without a purchase), then marks player exploration and fleet marker 1; selection alone records team-specific occupancy without revealing the star. Enemy occupancy and all planets are preserved.
- Shared starts remain valid. Finalization records `PendingSharedStartEncounter`; no combat or clearing API is implemented. Both tick entry points reject active/failed startup sessions and pending encounters before mutation or marking advancement, including through new facades. A completed player session with a pending encounter is not ready for gameplay. The CLI/debug commands are unchanged and do not activate these operations; existing movement mechanics remain a scaffold.
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
- Classic enemy startup retains the winning zero-based star index in `TaskForce.Destination` with ETA/original ETA zero, matching C's stationary `dest` convention; index zero is valid. Debug placement and arrival retain their existing null-destination behavior. Startup records the enemy in `Sector.TaskForces[0]`, marks only enemy exploration, and leaves `Sector.Tf` unchanged. This represents the initial single stationary fleet without implementing C's general `tf_stars` counts, colocation, or fleet lifecycle; those limitations remain deferred.
- Mutable configuration and public collections allow invalidation of allocation and cross-references.
- RNG and state are supplied separately to shell/engine, allowing inconsistent wiring.
- Turn ownership remains split: `Game.Turn` is facade-local, while `GameState.HasAdvancedTurns` is a shared, irreversible lifecycle marker. After the player-startup/encounter gate permits advancement, both `Game.Tick()` (including the CLI tick command) and public `FleetSystem.Tick()` set the marker before advancement, including attempts that later throw. A new facade starts its displayed counter at zero but cannot bypass Classic initialization prerequisites. Direct fleet ticks do not increment any facade's turn counter. Consolidating turn ownership and persisting this state are deferred architectural work; this milestone changes neither turn counting nor movement rules.

These are current limitations, not approved redesigns. Discuss significant state, occupancy, command-routing, visibility, or persistence decisions before implementation. See the [backlog](DevelopmentBacklog.md).

## Deferred original research behavior

Milestone 4 implements enemy startup research; Milestone 6 adds player-only startup investments and breakthroughs. Future general enemy research must preserve these `OriginalSource/research.c` quirks: `research()` runs at least once even with zero investment and selects a new enemy field after each iteration, including when the amount has been exhausted. Enemy weapons/range breakthroughs select a field inside `research_limited()` and again in the outer `research()`; velocity breakthroughs do not make the inner selection. The inner assignment to its field parameter is local. If no amount is consumed, the remaining investment is discarded after field selection. Range is the unconditional fallback even at its maximum level. General economy integration and enemy breakthroughs remain deferred; snapshots do not preserve research selection, player-startup sessions, starting money, or initialization lifecycle state.

## Classic player startup boundaries

`GameConfig.StartingMoney` defaults to 30; transports remain independent. Core configuration supports nonnegative money, transports, and capacity for this session. Existing `GameConfig.Validate()` still exposes a clamped-zero `Capacity` for negative requests outside this path; it now retains the requested value internally so Classic player startup can reject it instead of silently treating it as manual selection. Explicitly assigning a supported capacity replaces that request. This legacy/configuration mismatch remains for later CLI integration. No new CLI options, investment-text parser, raw-key input, report commands, or 25-character line handling are introduced. A future adapter must preserve purchase ordering, permit only end-of-spending finalization, and use actual battlestar cost 70 despite the C startup help's conflicting 75. Negative budgets/capacities and overflowing quantities are deliberately unsupported instead of copying undefined C behavior or clamping. Direct mutation of public model collections remains a general invariant risk; startup operations validate lifecycle, board links, and starting fleet locations but do not redesign model ownership.
