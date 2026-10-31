# Classic turn completion and endgame

Primary specification: `OriginalSource/update.c:check_game_over/up_year`, `main.c:main`, `command.c:quit/inputplayer`, `structs.h` configuration, and the enemy routines inventoried in [ClassicEnemyAI.md](ClassicEnemyAI.md). This is the bounded turn-completion slice following the strategic audit. No new enemy strategy or difficulty rule is added.

## Missing behavior and chosen boundary

All thirteen functions in `enemy.c` already have C# equivalents: scoring, scout/transport/warship deployment, recursive colonization, conquered-colony bombardment, corrected wandering, production, local combat choices and withdrawal. The missing normal-loop component was `check_game_over`. The previous CLI kept accepting G after elimination and reported an unimplemented-endgame boundary after the configured turn limit. Confirmed Q exited without the original final tally. There was no need to add substitute strategic scoring or purchasing rules.

The new Core `Game.CheckClassicGameOver` implements the C function as a separate step after annual update, or on confirmed quit without annual update. `ClassicGameResult`, `ClassicScore` and `ClassicEndReason` retain a final snapshot in GameState. The Classic CLI calls the check after the entire movement/combat/investment/post-investment-combat phase finishes. Original completed startup is required for automatic terminal endgame handling; NotStarted models remain the established partial development/test facility. Core callers can explicitly check model outcomes independently of the terminal.

`CompleteClassicTurn` still performs `up_year` rather than folding the next C main-loop function into it. Core full-game hosts/simulations call `CheckClassicGameOver` immediately afterward, as `main.c` does. A direct attempt to begin beyond the configured limit also finalizes the result before rejecting another turn. Direct/debug Tick remains movement-only and does not silently opt into AI/economics/endgame evaluation. Once a result is finalized, both Classic and direct/debug movement are blocked. The existing low-level 40-turn subsystem soaks remain valid as subsystem tests; the new full playthroughs evaluate outcomes after every complete turn.

## Exact original scoring and termination

For each team, sum transports in fleet slots 1..26 whose destination is active, including travelling fleets. Ignore retained counts in inactive joined/destroyed slots. Add **industrial units** on planets currently owned by that team, including conquered colonies. Neutral planets do not count. Despite C's variable name `inhabs` and the wording "population in transports", planetary population, scouts, cruisers, battlestars and bases are not scored.

`total = transports + colonyIU`. A team is initially dead only when total **equals zero**, not when it has no ships/colonies or total <= 0. A population-only colony with IU zero does not keep it alive. Negative IU/totals from unusual original arithmetic are not clamped into invented values.

If both totals are nonzero and the displayed C turn is at least 40:

- Enemy is overwhelmed when `playerTotal / enemyTotal >= conf.overwhelm`.
- Player is overwhelmed when `enemyTotal / playerTotal >= conf.overwhelm`.

Both divisions are **integer division before comparing to a double**. With overwhelm 2.5, 8/3 is 2 and does not qualify; 9/3 is 3 and does. Both flags can be true for small configured factors. Displayed turn begins at 1, so the first such check follows 39 completed G phases, not 40.

The game ends on either dead/overwhelmed flag, displayed turn **greater than** the configured limit, or confirmed quit. The last configured G phase still runs enemy orders/movement/combat but skips due investment (`turn < num_turns` in C); annual update then advances displayed turn past the limit before the check. There is no extra movement or economic phase to finish an already-ending game.

The winner is independently determined from the final scores, even on voluntary quit:

1. Enemy wins if `enemyTotal > (int)(playerTotal * victory)`.
2. Otherwise player wins if `playerTotal > (int)(enemyTotal * victory)`.
3. Otherwise draw.

Comparisons are strict, casts truncate toward zero, and enemy-first precedence is preserved if a small configured victory factor satisfies both comparisons. Quitting does not automatically forfeit. Both totals zero draws. These choices preserve the C code, including the IU-vs-population and integer-ratio quirks; they are not modernized balance rules. Score evaluation consumes **zero RNG draws** and never changes fleet contents, ownership, budgets, research or clocks.

## Classic interaction and visibility

After natural termination, or Q then Y in ordinary order input, the CLI clears the map and prints the original `*** Game over ***`, player/enemy transport/IU/total lines, and exactly one original winner message:

- `**** THE ENEMY HAS CONQUERED THE GALAXY ***`
- `*** PLAYER WINS - YOU HAVE SAVED THE GALAXY! ***`
- `*** DRAWN ***`

One key acknowledges the ending and exits. EOF exits safely; reopening a loop over the same state only redisplays the retained result and waits once, without replaying decisions, movement, combat or spending. Confirmed Q does not run enemy orders or increment either clock. Declined Q still advances the original order phase, possibly reaching a natural ending. EOF at verification remains an unconfirmed exit without a fabricated result.

Only final totals reveal enemy resources, as the original ending does. Ongoing map/report visibility, enemy intelligence and restricted arrival/departure events are unchanged. No outcome is finalized during pending startup, combat or investment. Existing exceptional unsupported-phase Q/EOF escape still exits without claiming an invented completed-phase score. A finished snapshot is retained across Game facades, but no save format/persistence is added.

Presentation retains the established monochrome, 79-printable-column snapshot/console adaptation. C's malformed diagnostic `printf` immediately preceding `clear_screen()` is not reproduced as a visible debug frame. The source ending itself has no extra menu or restart flow; none is added here.

## Guard review and remaining limits

The enemy audit found no additional guard whose gameplay fallback could be established without inventing behavior. Missing scout/withdrawal destinations cause C rejection sampling not to terminate; no transport candidate/warship destination leaves invalid/uninitialized source selections; unchecked allocation can write dummy fleet zero. Those guards and retained failure reasons remain intact. They do not become passes, arbitrary destinations, automatic fleet merges or fabricated wins/losses. Already-partial failed orders are not replayed.

The approved deterministic wander indexing correction remains unchanged. The X-only player J defect remains deliberately preserved; cascading corrupt C counter effects are not reproduced by authoritative collection queries. Zero-capacity growth and corrupt/overflowing arithmetic still need a source-defect decision rather than a new economic rule. New score summation, integer-division overflow or nonrepresentable/nonfinite victory casts are rejected without publishing a result, because their original C results are undefined. Normal configured gameplay keeps C integer arithmetic and thresholds.

Remaining acceptance work includes reproducible compiled-C comparison, platform PRNG stream identity, physical-console/color/startup presentation and exact intra-movement pause interleaving. Source-undefined destination/allocation/growth states retain explicit boundaries. Persistence and modernization remain separate phases. This milestone closes normal game termination, not every unsafe legacy state or all presentation differences.

## Tests and changed files

`ClassicEndgameTests`: active/inactive/in-flight fleet totals; IU/population/neutral/conquest distinction; elimination with surviving warships; zero/negative totals; displayed-turn-40 and fractional overwhelm thresholds; winner ties/precedence/quit; final-year investment skip; pending-phase guards; retained snapshots/no replay; post-game movement guards; undefined overflow rejection.

`ClassicEndgameUiTests`: exact screen lines, one-key ending, confirmed/declined Q, EOF at verification/ending, retained-result redisplay. One existing scout-discovery fixture now keeps a spare transport, because a scout-only player is genuinely eliminated by C's newly implemented T+IU check. The old missing-endgame expectation now checks the actual time-limit result.

Additional strategic tests exercise stale enemy memory vs actual player ownership, warship deployment/capture before clock completion, and the integer turn-dependent transport room bonus. Three paired 60-turn controlled simulations (seeds 1/7/41) run development through the configured ending, compare complete outcomes and developed fleets/colonies, and reject further advancement. Two paired generated-galaxy playthroughs (seeds 1/17) use real initialization, a fixed player attack, original player defensive/research purchases, original combat decisions and outcome checks after each full turn. They exercise production, conflict and natural completion without clearing/bypassing guards, compare final state and RNG consumption, and verify checks add no draws.

Changed this milestone: new Core `Game.Endgame.cs` and `ClassicGameResult.cs`; updated `GameState.cs` and `Game.Economy.cs` for retained outcomes/movement boundary; CLI `ClassicGameLoop.cs` for final checking/report/quit; new `ClassicEndgameTests.cs`, `ClassicEndgameUiTests.cs`, `ClassicPlaythroughTests.cs`; updated `EnemyExpansionTests.cs` and `EconomyTests.cs`; relevant enemy/economy/command/terminal documentation, README, architecture, project plan and backlog. Earlier uncommitted work remains intact. No commit/push is performed.

Validation: `dotnet test Conquest.sln -c Release --no-restore` passes **736 tests**, zero failures/skips (37 new cases over 699). Additional order-boundary regressions verify fourth-year growth restores a last IU after bombardment before elimination is checked, and newly built enemy warships create mandatory combat before annual update/outcome. `dotnet build Conquest.sln -c Release --no-restore` succeeds with zero errors and refreshes normal Release output. NuGet reports the existing NU1900 unavailable-vulnerability-data warning. `git diff --check` passes. Process checks of the normal executable exercise a configured one-turn ending with the exact original score/draw screen, Q/Y immediate final summary, and debug-shell startup/quit; all exit zero. No compiled-original or physical-console comparison is claimed.
