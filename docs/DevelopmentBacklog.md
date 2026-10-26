# Conquest Development Backlog

Baseline: reconnaissance on 2026-10-26. Restore and Release build passed; all 22 tests passed. These results do not establish Classic completeness or cover the problems below.

Evidence labels: **Confirmed — runtime** means reproduced through the CLI; **Confirmed — inspection** means established from code without a separate runtime reproducer; **Suspected** requires investigation. Missing original functionality is distinct from a conversion defect. Modernization items are proposals, not approved Classic changes.

Priorities: **P0** reliability/blocking investigation; **P1** scaffold correctness; **P2** missing Classic gameplay after stabilization; **P3** modernization after the Classic baseline. Add regression tests for fixes and meaningful additions.

## 1. Verified defects and reliability problems

| Priority | Evidence | Problem and next verification |
|---|---|---|
| P0 | Confirmed — runtime | `move 99 0` crashes in `GameShell.Course`, indexing before fleet-slot validation. Audit invalid fleet/star IDs and command error handling. |
| P0 | Confirmed — inspection | `ConsoleIO` converts EOF to an empty string; the shell keeps reading. Test actual exhausted input; `DummyIO` returns `quit` and masks this path. |
| P1 | Confirmed — runtime | With fog enabled, `reveal on`, `discover`, and `reveal all` do not reveal stars in the active CLI map. Commands and renderer consume different visibility state. |
| P1 | Confirmed — runtime and inspection | `tf map` omits star zero and zero-axis positions due to one-based loops/bounds. Verify all edges and align displayed/command coordinates. |
| P1 | Confirmed — inspection | Move guard treats valid `(0,0)` as unplaced. Establish explicit placement semantics. |
| P1 | Confirmed — inspection | Fleet occupancy overwrites same-team references and clears old references without checking identity. Test colocation, crossing, departure, and re-placement. |
| P1 | Confirmed — inspection | `SaveSystem.Load` does not reconstruct board star links/indices. Snapshots omit fleets, turn, RNG state, visibility grid, reveal mode, and enemy exploration. Define scope before calling this full persistence. |
| P1 | Confirmed — inspection | `SectorView.Star` excludes index zero; `TaskForceForViewer` can index `-1` for an empty sector. Test empty sectors and both teams. |
| P1 | Confirmed — inspection | `GameState.PlaceStars` clears object references but leaves stale legacy star indices. Test board invariants across repeated placements. |
| P1 | Confirmed — runtime; documentation corrected | Old README startup flags `--w`, `--h`, `--count`, and `--space` were ignored. README now uses actual options; parsing/unknown-option regression coverage is still absent. |

### Suspected reliability risks

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

**P2 — Classic requirements:** investigate original rules before implementing them and preserve behavior in tests. Source pointers are starting points, not complete specifications.

| Area | Missing or incomplete behavior | Primary C references |
|---|---|---|
| Initialization/configuration | Original startup, initial spending, player/enemy setup, configuration-file behavior, planet assignment. | `init.c`, `config.c`, `structs.h` |
| Planets/colonies | Planet model, ownership, population/capacity, infrastructure, planetary defenses. | `structs.h`, `init.c`, `command.c`, `update.c` |
| Fleets/travel | Splitting/joining, ship lifecycle, original distance/range/velocity travel, exploration losses, arrival effects. Current Manhattan travel is a scaffold. | `taskforce.c`, `movement.c`, `input.c` |
| Economy/landing | Landing/transports, population growth, production/investment, conquest, revolt. | `command.c`, `update.c` |
| Research | Weapon, range, and velocity progression. | `research.c`, `main.c` |
| Combat | Fleet battles, planetary attacks, bombardment, salvos, withdrawal. | `combat.c`, `command.c` |
| Enemy AI | Planning, scouting, transport deployment, investment, attacks, retreats. | `enemy.c` |
| Turns/outcome | Original turn sequence, production cycle, elimination, scoring, victory/defeat, turn-limit enforcement. `Tick` only moves fleets/increments a counter. | `main.c`, `update.c` |
| Player interaction | Original gameplay commands and reports are not fully represented by the debug-oriented shell. Required output fidelity is undecided. | `command.c`, `display.c`, `input.c`, `misc.c` |

Establish a reproducible legacy build and evidence-backed behavior checklist. Apparent original C defects require investigation and a recorded decision rather than silent copying or repair.

## 4. Future modernization opportunities

**P3 — proposals only, after a working, tested Classic baseline:**

- Improve command usability, diagnostics, discoverability, scripting, and accessibility.
- Evaluate graphical front ends (MonoGame, Godot, Avalonia, or alternatives); none is selected or implemented.
- Extend gameplay/rebalance explicitly as modernization while preserving a defined Classic baseline.
- Consider richer persistence, replay/transcript tools, performance work, analyzers, and developer tooling as needs emerge. Decide full-save scope separately from correcting the snapshot prototype.

## Decisions requiring discussion

1. What RNG-sequence and output/transcript fidelity is required for Classic acceptance?
2. How should apparent C defects be handled when observed behavior conflicts with likely intent?
3. What canonical state, visibility, occupancy, and command/engine boundaries should replace parallel representations?
4. Is persistence a Classic deliverable or development convenience, and what constitutes a complete save?
5. Should regeneration during play be supported, and should SDK/language selection be pinned?

These are open decisions, not approval to implement a redesign. Stabilize the existing CLI before adding major gameplay systems. Follow the [project plan](ProjectPlan.md) and [standing instructions](../AGENTS.md).
