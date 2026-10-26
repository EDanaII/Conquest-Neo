# Conquest Project Plan

Revised 2026-10-26. The two product phases below replace the earlier numbered scaffold/refactoring phases.

## Phase 1 — Conquest Classic

Faithfully reproduce original C behavior and gameplay in C# with a CLI. `OriginalSource` is the primary rules reference. Modern architecture is encouraged when it preserves gameplay. Investigate unclear rules rather than invent replacements. Maintain .NET 8 compatibility and add regression tests for fixes and meaningful functionality.

### 1. Capture and baseline

- Present: original C sources, Core/CLI/test projects, Git ignore/style files, documentation, and Ubuntu .NET 8 CI.
- Reconnaissance baseline (2026-10-26): restore and Release build pass; 22 tests pass. This does not establish gameplay fidelity.
- Pending: reproducible legacy build, documented original rules/loop, and decisions on RNG/output fidelity and apparent C defects.

### 2. Stabilize the existing CLI first

- Address invalid-command crashes, EOF handling, zero-coordinate/index defects, and disconnected visibility/rendering.
- Investigate possible nonterminating generation on small/dense boards.
- Correct occupancy and snapshot restoration with explicit decisions on supported semantics.
- Add regression cases for real rendering, malformed commands, zero axes, overlaps, restored games, and generation boundaries.

See [DevelopmentBacklog.md](DevelopmentBacklog.md) for evidence and priorities. Complete this stabilization pass before adding major gameplay systems.

### 3. Resolve architecture incrementally

Discuss significant decisions before implementation: authoritative state/indexing, occupancy, visibility, shell/engine boundaries, generator consolidation, and save scope. Keep changes reversible and game rules independent of presentation.

Existing: board/stars/RNG, partial visibility, fleet placement/course/tick operations, and limited snapshots. Original movement and exploration behavior remain incomplete.

### 4. Restore original gameplay systems

Proposed dependency order, to refine through C-source investigation:

1. Original initialization/configuration and planet/colony/player state.
2. Fleet lifecycle, splitting/joining, distance/range/velocity movement, exploration, and arrivals.
3. Landing, ownership, population, investment/production, and research.
4. Fleet/planet combat, bombardment, withdrawal, conquest/revolt, and enemy decisions.
5. Full turn orchestration, termination, scoring, and victory/defeat.

Trace rules to original source and test them. Distinguish original behavior, conversion defects, and enhancements. Current Manhattan travel and manually placed free scouts are scaffold behavior, not accepted Classic rules.

### 5. Classic acceptance

- Exercise initialization, exploration, colonies, economy, research, combat, AI, and termination in representative end-to-end scenarios.
- Validate documented original rules and boundary cases; resolve required RNG/output fidelity and legacy-defect policy before declaring completion.
- Verify .NET 8 build/tests and keep documentation aligned with implemented commands and limitations.

Existing scripted I/O supports scenario testing. Density/spacing bands and 20–50-turn economy scenarios may supplement rule tests where justified by the C source; subjective feel is not a substitute for fidelity. Reference transcripts remain useful; exact transcript testing requirements are unresolved.

## Phase 2 — Conquest Modernized

Begin after a working, tested Classic baseline. Potential work includes usability, diagnostics, gameplay extensions, graphical front ends, and rendering/input/audio abstractions. No graphics framework is selected.

Additional analyzer/style tooling, a command reference, reference playthroughs, and a script runner remain possible improvements. CI and scripted test I/O already exist; an interactive `script run demo1` command does not.

## Quick start checklist

- Restore/build/test with commands in the [README](../README.md).
- Run the CLI; try `seed 1`, `genstars`, `map`, `place 1`, `move 1 2`, `tick`, and `tf list`.
- Follow [AGENTS.md](../AGENTS.md); Git history/branch mutations require explicit permission.

## Revision history

- **2025-09-01:** Earlier plan emphasized playability bands and dropped strict C parity.
- **2025-09-03/04:** Snapshots recorded scaffold and CLI/Core work.
- **2026-10-26:** Established Classic-first behavioral fidelity followed by modernization; corrected implementation status and prioritized CLI stabilization. Exact RNG/transcript parity remains a design decision.
