# Project Plan: C Game to C# Modernization

## Phase 0 — Capture & Baseline
- Keep a pristine copy of the original C sources (read-only).
- Build the C version and save the executable as a reference.
- Inventory all .c/.h files, group them by responsibility (I/O, parser, RNG, combat, etc.).

## Phase 1 — Repo & Solution Scaffold
- Create a Git repository.
- Solution layout:
  - `/Game.sln`
  - `/src/Game.Core`
  - `/src/Game.CLI`
  - `/src/Game.Graphics` (future)
  - `/tests/Game.UnitTests`
- Target .NET 8, enable nullable and warnings-as-errors.

## Phase 2 — C→C# Porting Strategy
- Introduce `IGameIO` and `IRng` to abstract input/output and RNG.
- Keep logic and UI separate.
- Transliterate C code mechanically to C# (1:1).
- Replace globals with `GameState` class.

## Phase 3 — Object-Oriented Refactoring
- Encapsulate globals into classes (`Player`, `World`, `Inventory`).
- Extract subsystems (`Parser`, `CombatSystem`, `MovementSystem`, `SaveSystem`).
- Replace error codes with exceptions or Try-patterns.

## Phase 4 — Graphics Upgrade Path
- Keep `Game.Core` pure logic.
- Add `Game.Graphics` project (MonoGame, Godot, Avalonia).

## Phase 5 — Tooling & Quality
- Set up CI with GitHub Actions.
- Enable Roslyn analyzers.
- Document architecture in `/docs/architecture.md`.
- Migrate save formats to JSON or versioned binary.
