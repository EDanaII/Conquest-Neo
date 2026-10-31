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
- Dense-board generation hang reproduced and corrected during the [completion audit](ClassicV1CompletionAudit.md); seeded RNG output mapping changes, preserving deterministic replay and original sampling rules.
- Correct occupancy and snapshot restoration with explicit decisions on supported semantics.
- Add regression cases for real rendering, malformed commands, zero axes, overlaps, restored games, and generation boundaries.

See [DevelopmentBacklog.md](DevelopmentBacklog.md) for evidence and priorities. Complete this stabilization pass before adding major gameplay systems.

### 3. Resolve architecture incrementally

Discuss significant decisions before implementation: authoritative state/indexing, occupancy, visibility, shell/engine boundaries, generator consolidation, and save scope. Keep changes reversible and game rules independent of presentation.

Existing: board/stars/RNG, partial visibility, Classic player course/range/velocity/interpolation and exploration operations, and limited snapshots. See [Classic movement](ClassicMovement.md) for implemented rules and remaining boundaries.

### Current milestone: Classic release-candidate validation

The [RC validation report](ClassicV1ReleaseCandidate.md) records the full repository review, 775 passing Release tests, normal Release build, paired executable victory/defeat/turn-limit games, terminal review and human-playtest checklist. Only completed Classic implementation/tests/documentation are included. The user authorizes committing and pushing the reviewed Classic work if checks pass; no v1.0 tag or completion declaration is authorized. Physical-console and varied human-play acceptance remain before a final release decision. No gameplay changes or refactoring are part of RC preparation.

### Previous milestone: Classic gameplay completion audit

The [v1.0 completion audit](ClassicV1CompletionAudit.md) reviews all implemented systems, fixes a confirmed dense-board generation hang, and adds paired ordinary seeded games through legitimate endings plus capture-to-victory and generation regressions. Its recommendation was release-candidate validation, not a declaration of v1.0 completion. The audit itself authorized no commit, push or tag; the current RC instructions above supersede that version-control restriction. Save/load, color enhancements, expanded galaxies and improved AI are explicitly deferred modernization; exact C/C# RNG sequence identity is not required. Correct rules, probabilities, state transitions and deterministic C# replay remain required.

### Previous milestone: Classic strategic audit and turn completion

All 13 original enemy routines have Core equivalents; the [strategic cross-check and guard review](ClassicEnemyAI.md) found no missing scoring, deployment, colonization or purchasing branch to replace. The bounded implementation closes original turn completion: after enemy orders, movement, combat, all growth/spending and investment combat, annual update is followed by elimination/overwhelm/time-limit checks and the original final score/winner screen. Confirmed Q scores immediately without a turn. Tests cover original integer ratios, IU-vs-population scoring, terminal behavior, 60-turn development and generated conflict playthroughs through actual outcomes. The approved wander correction is retained and other undefined AI states still stop explicitly. See [endgame audit](ClassicEndgame.md). The completion audit above supersedes its acceptance/deferment status; no modern strategy is added.

### 4. Restore original gameplay systems

Proposed dependency order, to refine through C-source investigation:

1. Original initialization/configuration and planet/colony/player state.
2. Fleet lifecycle, splitting/joining, distance/range/velocity movement, exploration, and arrivals.
3. Landing, ownership, population, investment/production, and research.
4. Fleet/planet combat, bombardment, withdrawal, conquest/revolt, and enemy decisions.
5. Full turn orchestration, termination, scoring, and victory/defeat.

Trace rules to original source and test them. Distinguish original behavior, conversion defects, and enhancements. Startup, movement/exploration, J/N, L, both owners' growth/investment/research, B/P?/B? combat and withdrawal, enemy expansion/orders, revolt and normal endgame/scoring are implemented within documented source-safe boundaries. Complete original acceptance still requires the remaining comparisons and defect decisions above. Debug Tick intentionally remains movement-only; manually placed free scouts remain a debug operation.

### 5. Classic acceptance

- Exercise initialization, exploration, colonies, economy, research, combat, AI, and termination in representative end-to-end scenarios.
- Validate documented original rules and boundary cases; review the completion audit's presentation adaptations and guarded legacy-defect policy before declaring completion. Exact platform RNG stream identity is not required.
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
