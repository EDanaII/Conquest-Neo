# Conquest Architecture

## Overview
Conquest is a modernization of a legacy C text-based strategy game, ported to C# with a modular structure.

- **Core**: Game logic (rules, state, combat, parser).
- **CLI**: Console interface for text-based play.
- **Graphics (future)**: Planned GUI/graphics interface using MonoGame, Godot, or Avalonia.
- **Tests**: Automated verification of game rules and parser behavior.

## Modules

### Conquest.Core
- `GameState`: Encapsulates global state (players, world, inventory).
- `Parser`: Interprets player commands.
- `CombatSystem`: Handles combat mechanics.
- `MovementSystem`: Handles navigation between locations.
- `SaveSystem`: Persists and restores game state.

### Conquest.CLI
- `ConsoleIO`: Implements `IGameIO` for user interaction.
- `RandomRng`: RNG adapter for `IRng`.

### Conquest.Graphics (planned)
- Interfaces: `IRenderer`, `IInput`, `IAudio`.
- Goal: Decouple rendering and input from core logic.

### Conquest.Tests
- NUnit/xUnit-based tests.
- State and rule tests.
- Parser normalization tests.
- Save/load round-trip verification.

## Key Interfaces
```csharp
public interface IGameIO {
    string ReadLine();
    void Write(string s);
    void WriteLine(string s);
}

public interface IRng {
    int Next(int minInclusive, int maxExclusive);
}
