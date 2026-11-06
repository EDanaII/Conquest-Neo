# Conquest Classic v1.0 release-candidate validation

Validated 2026-10-30 against [AGENTS.md](../AGENTS.md), the [completion audit](ClassicV1CompletionAudit.md), [project plan](ProjectPlan.md) and source-derived Classic subsystem documentation.

**Recommendation: accept this build as a candidate for final human playtesting.** Automated checks and representative executable games pass. No additional release blocker or unrelated working-tree change was found. This is not a v1.0 completion declaration; no v1.0 tag is created. Physical-console acceptance and varied human play remain outstanding.

## Repository review and scope

Starting branch: `main`; starting HEAD and live `origin/main`: `372feb81d5c7c581b7f8e55b358a9ab0ddd80a01` (`Fix classic task force CLI listing and movement`). Earlier commits establish interactive startup, purchases and player/enemy initialization. The subsequent Classic milestones were still uncommitted: 20 tracked modified files and 42 new files before RC documentation. They are one accumulated Classic change set, not 62 new changes introduced during this RC task.

| Pending group | Milestone / reason for inclusion |
|---|---|
| `ClassicGameLoop`, `ClassicScreen`, `ClassicTerminal`, CLI entry/startup changes | Default playable single-key Classic interface, original reports/map layering, refresh and separate debug shell. |
| `FleetSystem`, task-force fields, departure initialization, movement/command tests and debug-shell compatibility | Source-derived movement/exploration, fleet lifecycle/J/N, landing, symbols and letter/numeric debug identifiers. |
| Core economy/investment/research models and rules | Both owners' growth, local spending, ship construction, research and fourth-year sequencing. |
| Core combat/encounter models and rules | Bombardment, mandatory fleet encounters, planetary salvos/capture, withdrawal, cleanup and resumed turns. |
| Enemy orders/economy | Original strategic routines, colonization/expansion, observed events and approved deterministic wander correction. |
| Shared turn/result state and endgame | Original elimination/overwhelm/time limit, final scoring, quit and terminal outcomes. |
| `CRandRng`, audit tests | Approved dense-board correction, seeded progression and generation regression coverage. |
| Regression tests, README and Classic documentation | Source evidence, deterministic rules/screens/transcripts, safety boundaries and build/run instructions. |

The reviewed files contain no new dependency/project changes, source-C edits, persistence implementation, colors, expanded-galaxy feature, replacement AI, generated binaries, secrets or unrelated assets. Existing snapshot/debug adapters are retained. Earlier test expectation changes correspond to implemented Classic rules (for example Euclidean travel and retained stationary destinations), rather than weakening tests to accept an RC regression.

Both compatibility corrections are present: wander chooses the actual numbered reachable star without uninitialized/off-by-one indexing; bounded RNG output uses higher state bits to avoid unreachable dense-board coordinate pairs. Existing other undefined-C guards are preserved. This RC task changes documentation only; no gameplay or architecture was changed.

## Build, tests and complete games

| Check | Result |
|---|---|
| `dotnet test Conquest.sln -c Release --no-restore` | **775 passed; zero failed/skipped.** |
| `dotnet build Conquest.sln -c Release --no-restore` | Normal `bin/Release/net8.0` output built; zero errors. Existing NU1900 warning: NuGet vulnerability metadata unavailable. No package changes. |
| `git diff --check` / staged whitespace check | Pass; Git may report line-ending normalization notices, not whitespace errors. |
| Ordinary generated games, seeds 1–32, twice each | Re-executed as part of the full suite. Legitimate outcomes in 3–80 completed turns; matching paired state/RNG usage, no budget failures or bypassed guards. Under the fixed passive policy: 27 overwhelm defeats and five elimination defeats. |
| Generated attack games, seeds 1/17 | Repeatable development/conflict and legitimate termination; existing regression tests pass. |
| Controlled 60-turn development, seeds 1/7/41, twice each | Original enemy expansion/colony development continues through configured turn-limit scoring. |
| Capture-to-victory continuation | Actual movement/planetary capture of the last enemy colony produces a player elimination victory and rejects further turns. |
| Normal Release executable, seeded full games | Three configurations below, each run twice: identical redirected output, exit 0, original final score/winner screen, no hang/crash or enemy-processing-stopped message. |
| Documented `dotnet run` launch | Release `--no-build` launch with seed 1 and 15×15/26-star flags reaches startup/gameplay, reports, quit verification and final scoring. |
| Separate debug shell | Generation, placement, course, tick, fleet list/map and quit work; movement remains intentionally separate from the full Classic turn loop. |

Executable outcome checks use real generated startup, not edited enemy assets:

- **Player win:** seed 1, `--turns 5 --victory 1`; start B, buy `C2S2R`, N transfers one scout then J rejoins it, L lands 25 transports on B1. Continue combat and turns; spend the colony budget with original max-I/max-W actions. Final player T=2, IU=44, total=46; enemy T=31, IU=0, total=31. This is a short configured scoring victory, not evidence of a default-difficulty winning strategy.
- **Enemy win:** seed 1/default settings; start B, buy `C2S2R`, split a scout and order it toward A, inspect fleets, continue encounters. Player reaches elimination with total 0; enemy total 31. No colony investment is required in this scenario.
- **Turn-limit draw:** seed 1, `--turns 1`; start A, buy `30R`, advance/acknowledge. Both totals 35; original drawn ending. The suite also covers longer turn limits and final-year investment suppression.

The redirected input driver supplies acknowledgement keys and line terminators as well as command input. An early fixed-input probe reached investment and exhausted input there; it was a prompt-sequencing issue in the probe, not a blocked game. Completed probes supplied spending lines and reached their expected endings. Scripts may generate harmless invalid-command responses between phases; state-aware regression tests verify exact interaction sequences without that extra input.

These runs verify progression and repeatability, not statistical balance or every possible galaxy/decision. Identical RNG sequences with older builds or a platform C binary are not required.

## Terminal compatibility review

| Area | Verified behavior |
|---|---|
| Automatic map and refresh | Screen is presented on entry and after supported state changes; M invalidates the row cache and clears report/event text. Turn/economy/combat screens refresh without a manual map command. |
| Input | Original movement letters and contextual investment/B?/P? commands; single-key fleet/star selection, edited lines, uppercase handling, CRLF, cancellation/invalid-input rules and EOF exit. No cursor-navigation mode is invented. |
| Summaries and visibility | Original fleet letters/ship abbreviations, multi-fleet/multi-planet star layout, No colony, reports and remembered/observed information. Hidden enemy assets do not appear as fleet glyphs or unrestricted reports. |
| Layering/restoration | Status/star/fleet are separate characters; `*` denotes shared player locations; movement, joins, landing and destruction retain underlying star/colony symbols. |
| Clear/pagination | M, quit and final scoring use the existing clear/cache path. Reports paginate in the pane without reproducing C's overlap defects. |
| Quit and final scoring | Q asks for Y; declined Q advances the original phase. Confirmed Q scores without advancing; natural endings render the original transport/IU totals and consume one acknowledgement. |

Evidence includes source/code review, existing screen/key regression tests and redirected process checks. **No new material terminal playability difference was found in those checks.** Physical cursor placement, redraw performance, window resize/font/wrapping and console-host-specific key behavior cannot be certified by redirected output and must be checked by a person.

## Known limitations and deliberate choices

- Monochrome, 79 printable columns, safe pagination and small/redirected-console snapshot fallback are intentional. Starting-star selection remains line-based and uses the same Classic map coordinates as gameplay; purchasing now uses the original initialization panes (see the post-RC corrections below). The welcome pause and intermediate arrival pause timing still differ from C. Core completes all movement before individual exploration acknowledgements, while economy/year updates still wait.
- Original J's Y self-comparison is deliberately retained as X-only joining. The approved wander indexing correction is retained; no other undefined behavior receives a speculative rule.
- No reachable scout/withdrawal destination, missing transport/warship candidate, exhausted enemy slots, invalid growth capacity and overflowing arithmetic remain explicit original-C safety boundaries. Partial failed orders are not silently replayed. None occurred in the audited ordinary seeded games; this is not proof that all player decisions avoid them. If encountered during human testing, record the full message, seed and commands and treat it as a release review issue.
- Legacy debug rendering/visibility and incomplete snapshot adapters are separate development limitations. Save/load is not a supported Classic feature. Color enhancements, expanded galaxies, improved AI, new rules and refactoring remain modernization.
- Comparison is against supplied C source and fixtures, not a compiled legacy executable. Exact platform RNG stream identity is not claimed. The dense-board correction changes older seeded maps.

## Build and launch

From the repository root, with a .NET 8-compatible SDK/runtime and restored test packages:

```powershell
dotnet restore Conquest.sln
dotnet build Conquest.sln -c Release --no-restore
dotnet test Conquest.sln -c Release --no-build --no-restore
dotnet run --project Conquest.CLI -c Release --no-build -- --seed 1
```

Alternatively launch the built game with `dotnet Conquest.CLI/bin/Release/net8.0/Conquest.CLI.dll --seed 1`. Add `--debug-shell` for the separate development shell. No publish/install step is required. Restore requires NuGet access or cached packages; unavailable vulnerability metadata may produce NU1900 without preventing a cached build.

## Final human-playtest checklist

Use a real Windows console at least 80×25, then repeat key checks in Windows Terminal. Record seed, options, commands, outcome and any full error message. Resize once and confirm redraw remains usable; a small window may intentionally use snapshots.

1. Start seed 1 at B with `C2S2R`. Check the automatic map, starting-star report, fleet letters and technology. Exercise H, M, S/T with blank/all and selected identifiers, C/R, invalid input and backspace. Verify long reports pause and resume without losing commands.
2. N a fleet, inspect shared `*`, J it back, then split a scout and D it to another star. Advance using both G and space. Check ETA, exploration acknowledgement, underlying star restoration, arrivals and refreshed reports.
3. In a fresh game, L to B1, enter a valid quantity, then inspect population/IU/transports. Advance through fourth-year investment, spend the whole budget, build ships/research, and check the new fleet letter and refreshed colony report. G during unpaid investment does not skip the budget.
4. Explore and develop several colonies over a longer default game. Encounter enemies through legitimate exploration; check mandatory B? prompts, odds/continue/withdrawal, casualties, cleanup and resumed turns. Exercise P? attack/break-off and B bombardment only on eligible targets. Verify no hidden enemy information leaks.
5. Repeat a seed and decisions. Reach a natural ending; verify score lines and one-key exit. Also run `--seed 1 --turns 1` for a short ending. Check Q/Y immediate scoring and declined Q's intentional turn advance. Keep the configured five-turn victory distinct from default-game balance testing.
6. Launch `--debug-shell --seed 1`; try `genstars`, `place A`, `move a B`, `tick`, `tf list`, `map`, `quit`. Confirm returning to ordinary startup does not require the debug shell.

## Version-control preparation

The user authorizes a single release-candidate commit/push containing only the reviewed accumulated Classic work plus this RC documentation, provided validation and staged review pass. The remote main was checked before preparation and matched the starting HEAD. Use a normal fast-forward push to `origin/main`; do not force, discard work or create a v1.0 tag. The actual resulting commit hash/push/working-tree status is reported separately after the operations, rather than embedding a self-referential commit hash here.

## Post-RC correction: starting-star map continuity

Human playtesting found that the selection map differed from gameplay. The galaxy itself was not replaced: `Program.Main` generates once, initializes both owners and passes the same Game through startup into `ClassicGameLoop`. Debug `newgame` likewise generates its candidate once and retains it after successful startup. No generator is called by selection, purchases, finalization or screen rendering.

The defect was a duplicate renderer in `ClassicStartupFlow.WriteSelectionMap`: zero-based axes, a different grid layout and omitted Classic status characters. Original `init.c:init_player` instead calls the ordinary `printmap`, after `startup` has generated the galaxy in `initconst`. Startup now uses the map/axis rows of `ClassicScreen.Render` on the existing Game, sharing gameplay's one-based axes, orientation, cell geometry and visibility rules. Unused report/input pane rows are omitted so they do not scroll the map off a 25-row console before the existing line-based prompt. Line-based selection/purchases, capacity-based automatic selection, valid-letter/shared-start eligibility and Core startup rules are unchanged. Rendering consumes no RNG or exploration actions and exposes no hidden enemy fleets/planet data.

Four new regression cases first failed against the old renderer, then verify map/axis correspondence through actual startup and gameplay entry, including star A at internal (0,0), C at (14,14), two player choices, invalid-choice retry, zero-money and purchased fleets. Scripted generation draws are exhausted exactly once; object references, board links, identifiers and coordinates remain unchanged into gameplay. Existing fog/automatic-selection/shared-start/seed-stream tests remain enabled. Updated full Release validation: **779 tests pass**, zero failures/skips; `git diff --check` passes. NuGet still reports the existing unavailable-vulnerability-metadata warning.

At the time of this rendering correction, repeated fresh unseeded launches had a separate confirmed cause: `CRandRng()` initialized seed 1 and `Program` reseeded only when `--seed` was supplied. That initialization defect is addressed by the randomization correction below. It was not evidence of generation between selection and gameplay; the map correction itself did not alter seeding.

Recheck in a real console: choose B using `--seed 1`, note its position on the selection map, buy `C2S2R`, and verify the same B position/axes with fleet a on the initial gameplay screen. Only exploration/status and fleet contents should change. This post-RC correction is pending review: **do not commit or push** under the current request.

## Post-RC correction: original startup purchase screen

The supplied original-executable screenshot and `OriginalSource/init.c:init_player`, `input.c:get_line/get_token`, `display.c:print_tf` and `misc.c:help0` specify a persistent map with initialization on row 18, the balance prompt on row 19, fleet inventory on row 20, and the original transport/budget instructions on rows 21–23 (one-based). Events and Research use the ordinary adjacent panes. The old default C# startup instead printed purchasing/status text down the console.

Default Classic now uses the bounded `ClassicStartupPurchases` view/controller. It composes the existing `ClassicScreen` with startup text and sends frames through the same `ClassicConsoleTerminal` instance used by subsequent gameplay. That adapter writes changed rows and clears only on initial presentation or explicit >M; no console lines are appended while purchasing. The startup-only view suppresses the map fleet glyph until finalization, matching C and the screenshot. No map renderer, normal gameplay layout/commands, Core rules or RNG behavior was changed. The already generated Game and board pass through finalization into gameplay unchanged.

Purchase input is uppercased, editable with backspace, and returns at Enter or 25 characters without consuming a following Enter. Physical Enter is normalized by the existing terminal; redirected LF/CRLF purchase lines leave no extra LF at gameplay handoff. C/S/B and R/V/W call existing Core purchase/research operations; all tokens in a submitted line are processed, even after the budget reaches zero. H shows original startup help in the report pane. > reads a separate M/R key, with R then reading a field-selector line; all research rows remain composed from Core, as in the existing gameplay report adaptation. Q is illegal during purchases in C; default startup preserves that, while EOF exits without finalizing an incomplete purchase. Safe rejection of unrepresentable quantities remains an adaptation for undefined C overflow. A historical help quirk is preserved: `help0` advertises B at 75, but `B_COST` and the existing Core charge 70.

The development shell retains its existing line-based startup/status/help/quit behavior. Starting-star selection is also unchanged by this purchase-screen correction. The new view is tested independently with an injected terminal, and process checks cover the default Program wiring into gameplay. Regression coverage includes screenshot-derived row/column fixtures, persistent map/axes, no hidden enemy/planet reports, typed input/editing/CRLF/length, every ship/research purchase type, breakthroughs, insufficient funds/invalid quantities, H/>M/>R, zero budget, EOF, stable galaxy references/RNG counts and finalization/gameplay glyph transition. Physical-console redraw remains a human check. No commit or push is authorized for these post-RC fixes until review.

Validation: **801 Release tests passed, zero failures/skips**, including 22 new startup-purchase cases over the 779-test selection-map correction. `dotnet test Conquest.sln -c Release --no-restore` rebuilt the normal `Conquest.CLI/bin/Release/net8.0/Conquest.CLI.exe`; `git diff --check` passes. A first build was blocked by the user's running executable; focused tests passed in `bin/StartupPurchaseRC/`, then normal Release build/tests succeeded after that process closed. NuGet still emits the existing NU1900 warning. Redirected executable checks verify CRLF incremental C/2S/2R purchasing, original pane/fleet/research frames, gameplay handoff and Q/Y scoring, a configured one-turn ending, and separate debug-shell startup/list/quit; all exit 0. Shared-start purchase/finalization/battle handoff is additionally covered by a deterministic terminal test without advancing time or regenerating the galaxy. No compiled-original or physical-console redraw comparison is claimed beyond the supplied screenshot/source and screen fixtures.

## Post-RC correction: normal launch randomization

Root cause was reproduced through the actual executable, not inferred from a model test: four fresh unseeded processes all produced the same starting-star map, causing the new required-randomization regression to fail before correction. `GameConfig.Seed` defaults to null; `Program.Main` nevertheless constructed `CRandRng()`, whose default constructor sets seed 1. It called `Seed` only for an explicit command-line value, so every fresh unseeded process repeated seed 1. The ordinary entry point was unintentionally using Core's deterministic default. No static RNG/reset or second galaxy generation was found in this path. The earlier dense-board output-bit correction did not change initialization and could not fix this defect.

The smallest responsible path is now corrected: Program constructs `CRandRng(config.Seed ?? RandomNumberGenerator.GetInt32(1, int.MaxValue))`. Unseeded launches choose an independent, nonzero OS-random seed once. Explicit seeds bypass that choice and reproduce the existing Core sequence exactly, including zero and negative seed semantics. Core's RNG algorithm/default constructor, configuration, rules and generation are unchanged. No timing delay, clock-based workaround, forced map difference, global state or reseeding between startup phases is introduced. Random seeds can theoretically coincide; independent randomness does not promise that no two galaxies can ever match.

Same-process lifecycle was already correct: debug `newgame` allocates a fresh GameState, generates once, initializes and completes startup, then retains that candidate. It uses the same advancing RNG instance without resetting the seed. This now begins from an OS-selected stream unless `--seed` was requested; successive games differ, and the same explicit seed plus command sequence repeats the same series. The explicit debug `seed N` command intentionally resets that stream. Default Classic ends the process after its final acknowledgement, as the original does; no new restart command was added. `OriginalSource/init.c:startup` calls `srand((UINT)time(0))` once before `initconst`. The correction preserves its intent of nonconstant normal starts while using OS entropy rather than second-resolution time, which can itself repeat on rapid launches.

Eight new executable-level regression cases cover four independent unseeded launches, identical explicit-seed launches (1/0/-123) compared with Core's existing sequence, two successive unseeded/seeded `newgame` operations and replay of the whole seeded sequence, coordinate/identifier continuity across selection/purchases/gameplay for both seed modes, and unseeded full 4x4 generation reaching selection. Tests use process timeouts to report hangs, not sleeps to generate different seeds. Existing bounded dense-board and seeded gameplay tests remain enabled.

Validation: `dotnet test Conquest.sln -c Release --no-restore` passes **809 tests, zero failures/skips**, and rebuilds the normal Release executable. `git diff --check` passes. NuGet emits the existing NU1900 warning for unavailable vulnerability metadata. This fix changes only Program's seed creation, adds `ClassicLaunchRandomizationTests.cs`, and updates README/this report; earlier unreviewed map/purchase-screen fixes remain intact. Nothing is committed or pushed.

Manual check: launch `Conquest.CLI/bin/Release/net8.0/Conquest.CLI.exe` twice **without `--seed`**, note several star positions, and exit at selection with `quit`; normally the maps differ. Repeat with `--seed 123`; the maps match. For same-process testing launch `--debug-shell`, run `newgame`, select B, spend `5S`, and repeat `newgame` with the same choices; the next map differs. Issue `seed 123` before each `newgame` to reproduce one map. In a normal game, note the selected star's position, buy `C2S2R`, and verify its position remains fixed into gameplay, with fleet a appearing after startup. No v1.0 tag, commit or push is authorized pending review.
