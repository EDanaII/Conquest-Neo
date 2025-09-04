# Conquest Project Plan (Revised --- 2025-09-01)

## Phase 0 --- Capture & Baseline

-   Keep a pristine copy of the original C sources in `/GameLegacyC`
    (reference only).
-   Build the legacy binary once for orientation.
-   Document high-level game loop, core mechanics (stars, fleets,
    economy).

## Phase 1 --- Repo & Solution Scaffold

-   Git repo: ✅ Conversion/.gitignore, `.editorconfig` already in
    place.
-   Projects:
    -   `Conquest.Core`: all game logic (board, stars, RNG, state,
        fleets, commands).
    -   `Conquest.CLI`: minimal bootstrap, console I/O, forwards
        commands to Core.
    -   `Conquest.Tests`: xUnit/NUnit for unit + scenario tests.

## Phase 2 --- Core Porting

-   Board, stars, fog-of-war: ✅ already ported.
-   RNG (`CRandRng`): ✅ works.
-   CLI commands in place: `help`, `config`, `seed`, `roll`, `genstars`,
    `map`, `explore`, `fog`, `liststars`, `reveal`, `nameat`.
-   Next: introduce **fleets** (`TaskForce`, `place`, `tick`) and expand
    **Player** state (money, colonies, upkeep).

## Phase 3 --- Playability Tests

-   Implement scripted I/O to run canned CLI sequences.
-   Add acceptance tests that validate **feel**, not exact parity:
    -   Star density and spacing within expected bands.
    -   Exploration reveals stars at consistent pace.
    -   Fleets move and arrive correctly; overlaps trigger encounters.
    -   Economy: no runaway growth or early starvation in 20--50 turns.

## Phase 4 --- Refactoring & Expansion

-   Encapsulate subsystems: `FleetSystem`, `Economy`, `Combat`.
-   Replace magic globals with typed classes (`Player`, `TaskForce`,
    `Star`, `Planet`).
-   Improve error handling, exceptions, collections, immutability where
    helpful.

## Phase 5 --- Graphics Upgrade Path

-   Keep Core logic UI-agnostic.
-   Later add `Conquest.UI` (MonoGame/Avalonia/Godot).
-   Prototype tile/sprite rendering, map overlays.

## Phase 6 --- Tooling & Quality

-   CI pipeline (`dotnet build`, `dotnet test`).
-   Roslyn analyzers + stylecop.
-   `docs/` folder: architecture.md, command reference.
-   Example playthrough transcripts as **reference only**, not golden
    outputs.

## Quick Start Checklist

-   Clone repo, run `dotnet build`.
-   Run CLI: `dotnet run --project Conquest.CLI`.
-   Commands to try: `seed 1`, `genstars`, `map`, `explore`, `place`,
    `tick`.
-   Tests: `dotnet test` --- includes scenario tests.
-   Scripts: add `script run demo1` to auto-exercise multi-turn
    sequence.

## Revision History

-   **2025-09-01:** Dropped strict C parity; tests now focus on
    playability bands.\
-   **2025-09-03/04:** Repo snapshots uploaded; confirmed scaffold and
    CLI/Core split.
