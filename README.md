# Conquest (C → C#)

> A faithful reimplementation of the classic text strategy game, using modern C#, clean layering, and behavior-focused tests.

## Development strategy

- **Phase 1 — Conquest Classic:** Reproduce the original C game's behavior and gameplay in a working, tested C# CLI baseline. `OriginalSource` is the primary behavior reference; investigate unclear rules rather than substitute new ones.
- **Phase 2 — Conquest Modernized:** Improve and extend the game after the Classic baseline is established.

Modern implementation techniques are encouraged when they preserve gameplay. Exact RNG sequences, transcript matching, and treatment of apparent original C defects remain decisions to resolve; the previous blanket exemption from parity testing does not define Classic acceptance.

See [AGENTS.md](AGENTS.md) for standing development instructions.

## Quick start

**Prerequisite:** .NET 8 SDK. All projects target `net8.0`; no additional workloads are required. There is no SDK pin (`global.json`).

```bash
dotnet restore Conquest.sln
dotnet build Conquest.sln --configuration Release --no-restore
dotnet test Conquest.sln --configuration Release --no-build --no-restore

dotnet run --project Conquest.CLI -- --seed 1 --width 15 --height 15 --stars 26
```

The shell starts without generating a galaxy. Try `genstars`, `map`, `place 1`, `move 1 2`, `tick`, `tf list`, and `quit`. Star IDs and board coordinates are zero-based; fleet slots are 1–26. Some displays still use inconsistent coordinate labels.

For interactive Classic startup, enter `newgame`, choose a star letter, then spend the initial 30 units. Try `C2S2R` (one cruiser, two scouts, two units of range research), `5S`, or `30R`. Quantities precede tokens; lowercase and whitespace between tokens are accepted. Invalid/unaffordable purchases leave their requested resources unspent. Startup finishes only when the full budget is spent. `H`/`help` shows purchase tokens; `quit` exits without completing an unfinished startup. At the star prompt, `Q` selects star Q; use the full word `quit` to exit. Zero starting money requires no purchase input.

`newgame` creates fresh game state using the current configuration and RNG stream (use `seed N` first for reproducibility). Its selection map always displays star letters without enemy locations or planet details, even with fog enabled. `--capacity N` enables Core's automatic starting-star sampling. After startup, maps and existing debug commands refer to the new game. A shared start reports pending combat and blocks `tick`; combat is not implemented. `genstars` cannot regenerate a Classic-initialized state; use `newgame` for another fresh game. Debug movement and tick still use scaffold rules, not a complete Classic turn cycle.

Supported shell commands: `newgame`, `help` (`h`), `quit` (`q`), `seed`, `genstars`, `map` (`m`), `fog`, `reveal`, `discover`, `stars`, `place`, `move` (`course`), `tick`, and `tf list|map`.

Startup options include `--width`/`-w`, `--height`/`-h`, `--stars`/`-s`, `--turns`/`-t`, and `--seed`/`-seed`. Dimensions above 15 are clamped. `--victory` and `--overwhelm` are parsed, but their original gameplay systems are absent. `--capacity` is used by Classic player startup. Classic CLI startup requires 1–26 stars. The old example's `--w`, `--h`, `--count`, and `--space` options are not supported by startup parsing.

## Current status

- `Conquest.Core`: board/stars, configuration, deterministic RNG, unique star placement, partial visibility state, basic fleet placement and Manhattan movement, turn counter, and limited JSON snapshots.
- `Conquest.CLI`: console bootstrap, interactive shell, map display, and fleet debugging commands.
- `Conquest.Tests`: xUnit unit tests and scripted shell scenarios.
- `OriginalSource`: original C reference; not compiled by the .NET solution.

This is an early scaffold, not a complete Classic game. Planet generation, initial attributes/transports, enemy fleet/research/start selection, and player startup purchases/research are implemented. Colonies, economy, general enemy research, combat, enemy AI, and victory conditions remain missing. Movement does not yet reproduce original distance, range, and velocity rules.

Confirmed limitations include invalid fleet-index crashes, disconnected visibility commands/rendering, coordinate/index defects, incomplete save restoration, and overlapping-fleet occupancy problems. A possible generation hang on small/dense boards still needs direct reproduction. See the [backlog](docs/DevelopmentBacklog.md) for evidence and priorities.

## Validation baseline

Reconnaissance on 2026-10-26: restore and Release build succeeded with zero warnings/errors; all 22 tests passed using SDK 10.0.402 and .NET runtime 8.0.33. SDK 8.0.427 is also installed, but was not selected for that run. Passing tests do not establish Classic completeness. CI runs restore, Release build, and tests on Ubuntu with .NET 8.

## Documentation

- [Architecture](docs/architecture.md)
- [Project plan](docs/ProjectPlan.md)
- [Development backlog](docs/DevelopmentBacklog.md)
