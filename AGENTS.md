# Conquest development instructions

## Development objectives

Conquest development has two phases:

- **Phase 1 — Conquest Classic:** Faithfully reproduce the original C game's behavior and gameplay using C# and a command-line interface.
- **Phase 2 — Conquest Modernized:** Improve and extend the game after establishing a working, tested Classic baseline.

## Development principles

1. Preserve original gameplay rules and behavior during Phase 1.
2. Treat `OriginalSource` as the primary reference for original game behavior.
3. When original behavior is unclear, investigate and report rather than inventing replacement rules.
4. Modern C# architecture and implementation techniques are encouraged, provided they don't unintentionally alter gameplay.
5. Keep game logic independent of user interfaces.
6. Maintain .NET 8 compatibility unless explicitly directed otherwise.
7. Add regression tests for bug fixes and meaningful new functionality.
8. Prefer incremental, reversible changes over broad rewrites.
9. Distinguish original game behavior, conversion defects, and proposed enhancements.
10. Discuss significant architectural decisions before implementing them.
11. Do not commit, push, merge, reset, or switch branches without explicit permission.

## Project references

- [README](README.md): setup and current status.
- [Architecture](docs/architecture.md): project responsibilities and boundaries.
- [Project plan](docs/ProjectPlan.md): development sequence.
- [Development backlog](docs/DevelopmentBacklog.md): confirmed defects, suspected risks, missing original functionality, and modernization proposals.
