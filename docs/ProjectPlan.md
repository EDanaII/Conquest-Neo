# Project Conversion Plan: C Text Game → C# Modernization (Updated)

**Change summary (2025‑09‑03):** We are **not** pursuing bit‑exact C parity or “golden transcript” testing. Test strategy pivots to **behavioral/invariant** checks and small CLI acceptance smoke tests. Everything else in scope remains.

---

## Phase 0 — Capture & Baseline (trimmed)
- Keep a pristine copy of the original C sources (read‑only) for reference only.
- Build the C version once for orientation (no transcript capture needed).
- Inventory the legacy modules (I/O, parser, RNG, world/board, combat, etc.).

**Out of scope:** Golden transcripts; byte‑for‑byte output matching; C‑parity RNG.

---

## Phase 1 — Repo & Solution Scaffold
**Solution layout**
- `Conquest.Core` — logic & state (no console calls)
- `Conquest.CLI` — text UI wiring
- `Conquest.Tests` — unit/behavioral tests
- (Future) `Conquest.Graphics` — GUI/engine wrapper

**General**
- Target .NET 8
- Nullable enabled; warnings-as-errors

---

## Phase 2 — C→C# Porting Strategy (transliterate first)
- Introduce core interfaces: `IGameIO`, `IRng` (+ optional `ISeedableRng`).
- Replace C globals with a temporary `GameState` aggregate.
- Transliterate features slice-by-slice (RNG → parser → world/board → commands).
- Keep console rendering **out** of `Conquest.Core`.

---

## Phase 3 — Behavioral Tests (replaces "Golden Tests")
**Goals**
- Ensure **correctness of rules**, not verbatim output.
- Use deterministic seeding where helpful, but not to match the C outputs exactly.

**Test buckets**
1. **Star generation invariants**
   - Count == `NumStars`
   - Bounds: `0 <= x < Width`, `0 <= y < Height`
   - Uniqueness: all `(x,y)` distinct
   - Light fuzz: run N seeds to catch overlap regressions
2. **Map & fog behavior**
   - Fog ON + no visible cells → map shows no `*`
   - Fog OFF → all stars visible
   - `RevealAll` / `HideAll` / `Discover x y [r]` affect rendering as expected
3. **CLI smoke**
   - `seed N; genstars; map; quit` returns 0 and doesn’t throw
   - Renderer invoked at least once; last state is the game’s state (via `SpyRenderer`)

**Not required**
- No transcript diffing
- No C RNG state matching

---

## Phase 4 — OO Refactoring (progressive)
- Encapsulate state into types (`Star`, `Cell`, later `Player`, `Sector`, etc.).
- Extract subsystems incrementally: parsing, movement/exploration, save/load.
- Replace error codes with exceptions or `TryX` patterns as appropriate.

---

## Phase 5 — Presentation Path (keep Core pure)
- **Current**: external `IMapRenderer` + `MapRenderer` (text map).
- **Future**: add a GUI project (MonoGame/Godot/Avalonia). Core remains presentation‑free via interfaces (`IRenderer`, `IInput`, `IAudio`).

---

## Phase 6 — Tooling & Quality
- CI: GitHub Actions (`dotnet build`, `dotnet test`).
- Analyzers/formatting: Roslyn, `.editorconfig`.
- Docs: minimal `/docs/dev-notes.md` (commands, layering, test philosophy).

---

## Current Implementation Snapshot (today)
**Core**
- `Game` (partial): 4‑arg ctor `(IGameIO, IRng, GameState, IMapRenderer)`; `Run()` with commands:
  - `seed N` (uses `ISeedableRng` if available)
  - `genstars`
  - `map`
  - `fog [on|off]` (toggle with no arg)
  - `reveal [on|off]` (toggle with no arg)
  - `revealall`, `hideall`, `discover x y [r]`, `stars`, `quit`
- `GameState` (partial): `Config`, `Stars`, `Board`, visibility:
  - `bool FogEnabled` (default on)
  - `bool RevealStars`
  - `bool[,]? Visible` + helpers `EnsureVisibility`, `RevealAll`, `HideAll`, `Discover/DiscoverRadius`
- `IMapRenderer` + `MapRenderer`: externalized text map drawing that respects `FogEnabled`/`RevealStars`.
- RNG: `CRandRng` (LCG) implements `IRng`, `ISeedableRng` and bridges `Random.ICRandRng` (for star tools).

**Tests**
- `SpyRenderer` with `CallCount`, `LastState` for assertions.
- Invariant tests for star gen + map/fog; CLI smoke.

**Removed/avoided**
- No in‑`Game` `RenderMap` (renderer lives behind `IMapRenderer`).
- No "golden transcripts", no exact C parity assertions.

---

## Near‑Term TODOs
- Flesh out invariant tests (map/fog cases listed above).
- Add short `/docs/dev-notes.md` with:
  - Command list & usage
  - How to seed & reproduce behaviors
  - Layering overview (Core vs CLI vs Tests)
- Wire a simple CI pipeline.

---

## Definition of Done (alpha)
- Build & tests green on CI.
- CLI supports the command set above without exceptions.
- Star gen and map/fog behavior covered by invariants.
- Core independent of UI; renderer(s) behind interfaces.
- Docs explain how to build, run, and test.

---

## Appendix — Interfaces & Contracts (quick reference)
- `IRng { int Next(int minInclusive, int maxExclusive); }`
- `ISeedableRng : IRng { void Seed(int seed); }` (optional)
- `Conquest.Core.Random.ICRandRng { void Seed(uint); int NextInt(int); }` (satisfied via CRandRng explicit impl)
- `IMapRenderer { void Render(IGameIO io, GameState state); }`
- `IGameIO { string? ReadLine(); void WriteLine(string); }` (minimal for tests/CLI)

---

**Guiding principle:** Favor **behavioral clarity** over historical exactness. Preserve the feel of the original, not its quirks.

