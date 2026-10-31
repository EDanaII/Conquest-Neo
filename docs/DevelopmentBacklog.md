# Conquest Development Backlog

Current milestone: [Classic v1.0 completion audit](ClassicV1CompletionAudit.md). All major gameplay systems and normal endgame/scoring are implemented. The audit fixes the directly reproduced dense-board startup hang and recommends release-candidate validation with 775 passing Release tests. The approved wander correction and all other original undefined-state guards remain intact. Startup presentation and exact intra-movement pause screens are documented compatibility differences. Color enhancements, save/load, expanded galaxies and improved AI are deferred modernization, not missing Classic gameplay. Historical scaffold/debug-adapter findings below are not a current list of Classic release blockers; use the completion report for that classification.

### Completion-audit reliability finding

**Resolved P0, direct regression:** `Game.GenerateStars`, seed 1, 4x4 board, 16 stars exceeded 100,000 RNG draws. Correlated low LCG bits made some coordinate pairs unreachable. `CRandRng` now rotates higher state bits into the bounded output before extracting its 31-bit value; the state transition, draw count per call and source sampling rules are retained. Six dense-board cases now complete and repeat deterministically. Old seeded maps change. Exact platform C/C# RNG sequence identity is explicitly not required for this audit. See the completion report for reproducer, simulations and remaining guarded legacy states.

Baseline: reconnaissance on 2026-10-26. Restore and Release build passed; all 22 tests passed. These results do not establish Classic completeness or cover the problems below.

Evidence labels: **Confirmed — runtime** means reproduced through the CLI; **Confirmed — inspection** means established from code without a separate runtime reproducer; **Suspected** requires investigation. Missing original functionality is distinct from a conversion defect. Modernization items are proposals, not approved Classic changes.

Priorities: **P0** reliability/blocking investigation; **P1** scaffold correctness; **P2** missing Classic gameplay after stabilization; **P3** modernization after the Classic baseline. Add regression tests for fixes and meaningful additions.

## 1. Verified defects and reliability problems

| Priority | Evidence | Problem and next verification |
|---|---|---|
| P0 | Resolved — regression tested | Invalid fleet/star identifiers are rejected at the CLI boundary; `move`/`course` tests preserve fleets and continue accepting commands. Letter/numeric IDs are interchangeable. |
| P0 | Confirmed — inspection | `ConsoleIO` converts EOF to an empty string; the shell keeps reading. Test actual exhausted input; `DummyIO` returns `quit` and masks this path. |
| P1 | Confirmed — runtime | With fog enabled, `reveal on`, `discover`, and `reveal all` do not reveal stars in the active CLI map. Commands and renderer consume different visibility state. |
| P1 | Confirmed — runtime and inspection | `tf map` omits star zero and zero-axis positions due to one-based loops/bounds. Verify all edges and align displayed/command coordinates. |
| P1 | Confirmed — inspection | Move guard treats valid `(0,0)` as unplaced. Establish explicit placement semantics. |
| P1 | Partially resolved — regression tested | Movement rebuilds references after all moves, preserving crossings/colocation; debug placement clears only its own prior reference. A cell still holds one fleet per team and debug placement can replace that reference. General occupancy representation remains deferred. |
| P1 | Confirmed — inspection | `SaveSystem.Load` does not reconstruct board star links/indices. Snapshots omit fleets, turn, RNG state, visibility grid, reveal mode, and enemy exploration. Define scope before calling this full persistence. |
| P1 | Confirmed — inspection | `SectorView.Star` excludes index zero; `TaskForceForViewer` can index `-1` for an empty sector. Test empty sectors and both teams. |
| P1 | Confirmed — inspection | `GameState.PlaceStars` clears object references but leaves stale legacy star indices. Test board invariants across repeated placements. |
| P1 | Confirmed — runtime; documentation corrected | Old README startup flags `--w`, `--h`, `--count`, and `--space` were ignored. README now uses actual options; parsing/unknown-option regression coverage is still absent. |

### Historical reliability risks

The generation-hang suspicion below is now directly reproduced and resolved as described above. Other entries retain their original scaffold scope and need a concrete current reproducer before being classified as Classic blockers.

- **P0 — Potential generation hang:** `Game.GenerateStars` and `GameState.PlaceStars` retry without a bound. A bounded simulation of the actual RNG algorithm yielded only four coordinate pairs on a 4×4 board in 100,000 samples. A bounded direct API reproduction is still needed; the hang is not runtime-confirmed. Investigate termination consistent with original generation rules.
- **P1 — Mutable-state invariants:** configuration and collections can change after allocation. Assess supported mutations and concrete failure cases before choosing encapsulation changes.
- **P1 — Regeneration during play:** no defined reset policy exists for fleets, courses, or exploration. Investigate original behavior and decide whether this is a debug operation or a supported game action.

## 2. Architectural inconsistencies and technical debt

These structures are confirmed by inspection; significant redesign choices require discussion.

- **P1:** Unify visibility semantics and rendering responsibilities. Core/CLI map renderers and an embedded fleet renderer differ in visibility, orientation, and indexing.
- **P1:** Define authoritative state/occupancy. Sectors duplicate indices and references; board/stars are zero-based while fleet slots are 1-based. Different conventions are not inherently defects; inconsistent assumptions are.
- **P1:** Define shell/engine boundaries. `GameShell` directly mutates state/accesses fleets; the separate router/handler path is not integrated.
- **P1:** Consolidate generation after confirming original rules. Three implementations differ; standalone spacing tests do not exercise interactive generation.
- **P1:** Define persistence ownership, validation, reconstruction, and supported data. Turn lives in `Game`; snapshots accept only `GameState`.
- **P1:** Clarify RNG contracts/ownership. Three related interfaces coexist; shell and engine can receive different RNG instances. Original sequence fidelity is unresolved.
- **P1:** Expand tests for real rendering, EOF, malformed/startup input, zero axes, overlaps, save usability, small/dense boards, both teams, and regeneration. No numeric coverage baseline was collected.
- **P1:** Preserve .NET 8 compatibility. Core/CLI use `LangVersion=latest` without SDK pinning; discuss whether SDK/language selection should be pinned. CI already uses .NET 8.
- **P1:** Review obsolete commented implementations, parity claims, unused adapters, and duplicate test helpers incrementally. Keep documentation aligned with actual commands.

## 3. Missing original Conquest functionality

Historical investigation inventory: startup/assets/planet assignment, research, enemy planning and player command gameplay listed below are now implemented. The separate Classic interface supersedes the debug shell as the primary game. Detailed current coverage is in the completion audit and linked subsystem documents; the original configuration-file and startup presentation are not reproduced verbatim.

**P2 — Classic requirements:** investigate original rules before implementing them and preserve behavior in tests. Source pointers are starting points, not complete specifications.

| Area | Missing or incomplete behavior | Primary C references |
|---|---|---|
| Initialization/configuration | Original startup, initial spending, player/enemy setup, configuration-file behavior, planet assignment. | `init.c`, `config.c`, `structs.h` |
| Planets/colonies | L, fourth-year growth/investment, B bombardment, P? capture and guard/revolt are implemented. Fleet/enemy combat now resolves in original order; enemy expansion now follows ClassicEnemyAI.md; wandering index correction is implemented; normal endgame integration now follows ClassicEndgame.md. | `structs.h`, `init.c`, `command.c`, `update.c`, `combat.c` |
| Fleets/travel | Player orders, distance/range/velocity travel, interpolation, exploration losses, arrival intelligence and Classic J/N joining/splitting implemented; see [Classic movement](ClassicMovement.md) and [fleet commands](ClassicFleetCommands.md). The original J Y-comparison defect is explicitly preserved as X-only joining. Combat withdrawal and encounters now resolve; enemy order generation now includes wandering with its explicit index correction. | `taskforce.c`, `movement.c`, `input.c` |
| Economy/landing | L and full player inv_player spending are implemented with original local IU budgets, construction, mobilization/abandonment, research and bundle/max actions. Player attacks precede growth and follow spending. Enemy-owned colonies now invest automatically; unsafe legacy AI paths stop explicitly. See [economy](ClassicEconomy.md). | `command.c`, `update.c` |
| Research | Weapon, range, and velocity progression. | `research.c`, `main.c` |
| Combat | B/P? planetary combat, B? fleet battles, W/enemy combat withdrawal and immediate enemy planetary responses implemented. Shared starts resolve before movement. Slot exhaustion and unreachable enemy withdrawals report original-C unsafe boundaries without rerolling losses. Enemy expansion/production now follows ClassicEnemyAI.md. See [audit](ClassicCombat.md) and [encounter fix](ClassicEncounters.md). | `combat.c`, `command.c`, `enemy.c`, `display.c` |
| Enemy AI | Planning, scouting, transport deployment, investment, attacks, retreats. | `enemy.c` |
| Turns/outcome | Classic G runs original enemy orders, movement/revolt, combat, both owners' growth/spending and annual update, followed by original elimination/overwhelm/time-limit checks and the final tally. Final-turn investment is skipped. Source-undefined states and full legacy comparison remain explicit boundaries. Debug `Tick` only moves fleets/increments the shared completed counter. | `main.c`, `update.c` |
| Player interaction | Original gameplay commands and reports are not fully represented by the debug-oriented shell. Required output fidelity is undecided. | `command.c`, `display.c`, `input.c`, `misc.c` |

Establish a reproducible legacy build and evidence-backed behavior checklist. Apparent original C defects require investigation and a recorded decision rather than silent copying or repair.

## 4. Future modernization opportunities

**P3 — proposals only, after a working, tested Classic baseline:**

- Improve command usability, diagnostics, discoverability, scripting, and accessibility.
- Evaluate graphical front ends (MonoGame, Godot, Avalonia, or alternatives); none is selected or implemented.
- Extend gameplay/rebalance explicitly as modernization while preserving a defined Classic baseline.
- Consider richer persistence, replay/transcript tools, performance work, analyzers, and developer tooling as needs emerge. Decide full-save scope separately from correcting the snapshot prototype.

## Decisions requiring discussion

1. Exact C/C# RNG-sequence identity is not required by the completion audit. Physical-console acceptance of documented presentation differences remains.
2. How should apparent C defects be handled when observed behavior conflicts with likely intent?
3. What canonical state, visibility, occupancy, and command/engine boundaries should replace parallel representations?
4. Save/load is explicitly deferred modernization. Define its scope before extending the development snapshot prototype.
5. Should regeneration during play be supported, and should SDK/language selection be pinned?

These are open decisions, not approval to implement a redesign. Stabilize the existing CLI before adding major gameplay systems. Follow the [project plan](ProjectPlan.md) and [standing instructions](../AGENTS.md).
