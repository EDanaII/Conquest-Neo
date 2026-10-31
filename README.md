# Conquest (C → C#)

> A faithful reimplementation of the classic text strategy game, using modern C#, clean layering, and behavior-focused tests.

## Development strategy

- **Phase 1 — Conquest Classic:** Reproduce the original C game's behavior and gameplay in a working, tested C# CLI baseline. `OriginalSource` is the primary behavior reference; investigate unclear rules rather than substitute new ones.
- **Phase 2 — Conquest Modernized:** Improve and extend the game after the Classic baseline is established.

Modern implementation techniques are encouraged when they preserve gameplay. Correct rules, probabilities and state transitions are required; identical platform C/C# RNG sequences are not. Deliberate source-defect corrections and terminal adaptations are documented in the [completion audit](docs/ClassicV1CompletionAudit.md).

See [AGENTS.md](AGENTS.md) for standing development instructions.

## Quick start

**Prerequisite:** .NET 8 SDK. All projects target `net8.0`; no additional workloads are required. There is no SDK pin (`global.json`).

```bash
dotnet restore Conquest.sln
dotnet build Conquest.sln --configuration Release --no-restore
dotnet test Conquest.sln --configuration Release --no-build --no-restore

dotnet run --project Conquest.CLI -c Release --no-build -- --seed 1 --width 15 --height 15 --stars 26
```

Default mode generates a Classic game, asks for a starting star and purchases, then automatically displays the Classic gameplay screen. Press H for help, D followed by a fleet letter and star letter to order travel, G or space to advance the supported Classic phase, S/T for reports, M to redraw, and Q then Y to quit. Gameplay coordinates are one-based; fleet glyphs are lowercase letters. Both owners grow/invest every fourth turn; original enemy expansion now runs before movement. Original elimination, overwhelm, time-limit and final-score checks are now implemented; enemy wandering uses the documented deterministic correction to the C index defects. See [Classic terminal interface](docs/ClassicTerminal.md).

Course replacements cancel the pending order before asking for the new star. Unknown arrivals pause for acknowledgement and display exploration losses; known arrivals do not pause. S/T accept blank/all or token lists such as `A B` and `2A3B` (quantity prefixes are ignored for reports). R refreshes the persistent research summary. M clears reports and forces a redraw. As in C, declining Q verification still ends the order phase and advances movement. B bombards eligible planets. Mandatory fleet encounters automatically pause and enter B?: G/space continues combat, W splits/withdraws, O shows odds. A final acknowledgement resumes the turn; ordinary landing/orders are unavailable inside B?. See [encounter progression](docs/ClassicEncounters.md) and the [command audit](docs/ClassicCommandAudit.md).

N splits a stationary fleet: press N, its fleet letter, then a ship line such as `2S3T` and Enter. Blank moves the whole fleet to the first free slot; it does not cancel. J reads a line such as `A B C` and joins B/C into A. Both update the map and star report without advancing time. For Classic fidelity, J deliberately preserves the C defect that checks only X, allowing otherwise eligible fleets at different Y coordinates to join. See [Classic fleet commands](docs/ClassicFleetCommands.md) for rules, examples, tests and the recommendation to correct this only through a reviewed behavior change.

L lands transports: press L, the fleet letter, a planet digit only if the star has multiple planets, then a transport quantity and Enter. A sole planet is selected automatically. Blank or zero means all transports, not cancel; the whole requested amount must fit. Landing immediately creates or reinforces a colony with one inhabitant and one IU per transport consumed. Enemy fleets, enemy planets and conquered player colonies retain the original refusals. See [Classic landing](docs/ClassicLanding.md).

Every fourth Classic turn grows colonies, then asks each player colony how to spend its IU-derived budget (`A3>  4?`). Use quantities before S/C/B ships, I infrastructure, T transports, M/A defenses or R/V/W research; H shows the original investment help. *x buys the original maximum, + builds an offensive bundle, - builds defenses, and > asks for a separate M/S/C/R report key. The whole budget must be spent; for a newly landed population/IU 3/3 at capacity 20, the first cycle gives 4/4 and `4R` finishes its budget. C's integer growth can overshoot capacity, and finishing investment merges fleets into a new letter even for research-only spending. Enemy colonies now invest automatically in original order; encounter combat resolves before production. See [Classic economy](docs/ClassicEconomy.md) for exact rules, quirks and boundaries. Debug `tick` remains movement-only.

For the separate development shell, run `dotnet run --project Conquest.CLI -- --debug-shell --seed 1`. It starts without generating a galaxy. Try `genstars`, `map`, `place A`, `move a C`, `tick`, `tf list`, and `quit`. Debug coordinates remain zero-based and numeric identifiers remain supported. The following shell command examples apply to this development facility.

Classic starts automatically in default mode; in the debug shell, enter `newgame`. Choose a star letter, then spend the initial 30 units. Try `C2S2R` (one cruiser, two scouts, two units of range research), `5S`, or `30R`. Quantities precede tokens; lowercase and whitespace between tokens are accepted. Invalid/unaffordable purchases leave their requested resources unspent. Startup finishes only when the full budget is spent. `H`/`help` shows purchase tokens; `quit` exits without completing an unfinished startup. At the star prompt, `Q` selects star Q; use the full word `quit` to exit. Zero starting money requires no purchase input.

`newgame` creates fresh game state using the current configuration and RNG stream (use `seed N` first for reproducibility). Its selection map always displays star letters without enemy locations or planet details, even with fog enabled. `--capacity N` enables Core's automatic starting-star sampling. After startup, maps and existing debug commands refer to the new game. A shared start blocks debug tick until combat resolves; the playable Classic loop now resolves that combat automatically before movement. The debug shell does not supply B? interactions. `genstars` cannot regenerate a Classic-initialized state; use `newgame` for another fresh game.

Movement now follows the original C distance, range, velocity, interpolation, and arrival exploration rules. `move a C` issues an order without advancing time; `tick` advances one movement phase. Scouts alone can exceed range. Orders can be replaced before the first tick, but not during travel. `tf list` shows destinations and remaining ETA while traveling, and the current star when stationary. Exploration may destroy ships; `tick` reports arrivals and losses. Debug `place` remains available and grants scouts as before. See [Classic movement](docs/ClassicMovement.md) for exact formulas, source references, and limitations. This is still not a complete Classic turn cycle.

Supported shell commands: `newgame`, `help` (`h`), `quit` (`q`), `seed`, `genstars`, `map` (`m`), `fog`, `reveal`, `discover`, `stars`, `place`, `move` (`course`), `tick`, and `tf list|map`.

Startup options include `--width`/`-w`, `--height`/`-h`, `--stars`/`-s`, `--turns`/`-t`, and `--seed`/`-seed`. Dimensions above 15 are clamped. `--victory` controls the original strict final-score multiplier and `--overwhelm` controls the original integer-ratio late-game termination check. `--capacity` is used by Classic player startup. Classic CLI startup requires 1–26 stars. The old example's `--w`, `--h`, `--count`, and `--space` options are not supported by startup parsing.

## Current status

See the [Classic v1.0 completion audit](docs/ClassicV1CompletionAudit.md) for the current release-candidate recommendation, source review, 775-test validation and known limitations. The audit corrects a dense-board startup hang in bounded RNG output; old seeds now produce different galaxies, while replay with the same new seed/commands remains deterministic. Classic has not been declared v1.0 complete. Color enhancements, save/load and improved AI remain deferred modernization.

The [release-candidate validation report](docs/ClassicV1ReleaseCandidate.md) records the repository review, current build/test/process results, known boundaries and a concise final human-playtest checklist. No v1.0 tag is created.

- `Conquest.Core`: Classic generation/startup, fleets/movement/exploration, landing, economy/shipbuilding/research, combat, enemy orders/expansion and original turn completion/scoring. Legacy snapshot helpers are development facilities, not supported Classic save/load.
- `Conquest.CLI`: playable Classic single-key interface with automatic map/report refresh, investment/combat prompts and final scoring, plus a separate `--debug-shell`. Line-based startup, monochrome rendering and pagination are documented presentation adaptations.
- `Conquest.Tests`: xUnit unit tests and scripted shell scenarios.
- `OriginalSource`: original C reference; not compiled by the .NET solution.

Startup, movement/exploration, J/N, L, both owners' economy/research, B/P?/B? combat, enemy expansion/orders and guard/revolt are implemented. The enemy starts with 35 transports (configurable), two scouts and a cruiser, **no colonies**; normal G/space now runs its original scout/convoy/warship decisions before movement. It establishes colonies by landing transports and develops them every fourth turn. Enemy investment no longer blocks initialized Classic games. Hidden activity stays hidden; explored/occupied stars and encounters reveal it through normal play. See [Classic enemy AI](docs/ClassicEnemyAI.md).

Classic gameplay now checks the original ending conditions after each complete turn and shows the final transport/IU tally and winner, followed by one key to exit. C scores transports plus **colony IU**, not inhabitants or warships, and begins integer-ratio overwhelm checks at displayed turn 40. The configured final turn skips investment before scoring; confirmed Q scores without advancing time. See [Classic turn completion/endgame](docs/ClassicEndgame.md). Original comparison/presentation work and unsafe legacy states still limit complete Classic acceptance.

The C wandering-split routine reads an uninitialized index and advances past its selected star. The conversion now selects the actual randomly numbered reachable star and retains the original fleet split/travel rules; see the correction in ClassicEnemyAI.md. Slot exhaustion and other undefined/nonterminating enemy destination cases also report their boundary without replaying decisions. Original victory/scoring are now implemented; persistence remains deferred.

Classic B asks for a fleet, a planet when needed, and units to bombard. It reduces capacity/population/industry on neutral or conquered player planets; enemy colonies require conquest first. Blank/zero units cancel, and a blasting fleet must wait for movement before blasting again. After any required fleet battle/enemy planetary response, P? offers G/space to fire player planetary salvos and B to break off. Selection is automatic for a sole enemy colony/fleet; multiple targets use single keys. Captures preserve population and industry and require a warship guard. See [Classic combat findings, rules and limitations](docs/ClassicCombat.md).

Legacy development-adapter limitations include disconnected debug visibility/rendering, coordinate/index defects, incomplete snapshot restoration and single-reference fleet occupancy cells. The Classic screen derives its own visibility and fleet layering from game state. Invalid fleet/star CLI identifiers are rejected; movement rebuilds fleet markers after crossings and colocation. The dense-board generation hang is now reproduced and corrected. See the [backlog](docs/DevelopmentBacklog.md) and completion audit for current scope and remaining boundaries.

## Validation baseline

Reconnaissance on 2026-10-26: restore and Release build succeeded with zero warnings/errors; all 22 tests passed using SDK 10.0.402 and .NET runtime 8.0.33. SDK 8.0.427 is also installed, but was not selected for that run. Passing tests do not establish Classic completeness. CI runs restore, Release build, and tests on Ubuntu with .NET 8.

Movement validation on 2026-10-29: `dotnet test Conquest.sln --configuration Release --no-restore` passed all 324 tests on .NET 8, including 10,092 source-reference movement scenarios. NuGet emitted NU1900 because vulnerability data was unavailable. `git diff --check` passed. See [Classic movement](docs/ClassicMovement.md) for validation scope and remaining differences.

## Documentation

- [Architecture](docs/architecture.md)
- [Project plan](docs/ProjectPlan.md)
- [Development backlog](docs/DevelopmentBacklog.md)
