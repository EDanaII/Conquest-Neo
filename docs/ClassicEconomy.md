# Classic planetary economy and turn processing

Audited and implemented 2026-10-30. Primary references: `main.c:main`, `movement.c:move_ships`, `update.c:up_year/invest/revolt`, `command.c:inv_player`, `research.c:research/research_limited`, `taskforce.c:get_tf/joinsilent`, `utils.c:round/any_bc`, `defs.h` costs and `misc.c:help4`. The later [enemy AI milestone](ClassicEnemyAI.md) adds original enemy orders before movement and `inv_enemy`/research to this same growth/spending flow.

## Source audit and implementation boundary

| Area | Original C | Previous conversion | This milestone |
|---|---|---|---|
| Turn order | Player orders; `inputmach`; movement/exploration/revolt; `battle`; optional investment; `up_year`; game-over check. | G called movement-only `Game.Tick`. | Classic Core begin/process/complete flow keeps movement and acknowledgements before economy, then increments clocks after all colony decisions. Combat and original enemy turns were added subsequently; scoring/endgame now follow ClassicEndgame.md. |
| Calendar | Displayed turn and production year start at 1. Invest when production year is exactly 4 and displayed turn is less than configured turn limit. Invest resets production year to 0; year update increments both clocks. | No production clock; Game counter started at 0 per facade. | Shared completed-turn count plus production year in GameState. Display remains completed turns + 1. Investment occurs on G 4, 8, 12, etc.; the final configured turn skips investment. |
| Growth | One all-planets pass at investment time; not every movement turn. Uses integer population/capacity, signed C rounding, then conquered halving. Adds newborn to both population and IU. | Landing added population/IU; no growth pass. | Both owners use the exact shared formula and their own rate, without a capacity clamp; all colonies grow before spending. |
| Production | Each colony copies current IU into a local spending balance; planets processed in star order then linked-list order. | No recurring budget. | Ordered Core-owned colony sessions preserve balance while awaiting input. Spending is not a global money pool and does not normally subtract persistent IU. |
| Player decisions | Full `inv_player` line-token loop, including construction, infrastructure, research, bundles and report subprompts. | Startup purchases/research only; existing models and reports already had relevant fields. | Full player investment vocabulary, reusing cost constants, research rules, slot initialization, fleet merging and reference rebuilding. |
| Ownership | T can abandon a colony; `revolt` changes conquered ownership without a guard. | Landing and J/N; no annual ownership step. | Exact transport mobilization/abandonment and deterministic post-movement revolt. The following [combat milestone](ClassicCombat.md) also creates conquered states through player planetary salvos and checks guards at assault completion. |
| Enemy economics | `inv_enemy` spends IU using heuristics and random choices, then invests leftovers in research. | Enemy startup/research selection only. | Implemented in original star/planet order, interleaved with player sessions. See [costs, exact RNG and C quirks](ClassicEnemyAI.md). Uninitialized debug enemy research still reports its prerequisite; normal Classic startup supplies it. |

## Arithmetic, resources and C quirks

Growth is literally:

```text
factor = 1 - (population / capacity)       // INTEGER division
newborn = C_round(population * teamGrowthRate * factor)
if conquered: newborn = newborn / 2       // INTEGER division after rounding
population += newborn
IU += newborn
```

C rounding adds/subtracts 0.5 before integer truncation, including floating-point effects just below a half. Original initialized rates are player 0.3 and enemy 0.5 (with the existing enemy startup advantage). The current player's stored rate is authoritative; no hidden fallback rate is invented for debug states.

Below capacity, integer division makes the factor 1. A population of 19 at capacity 20 grows by 6 at rate 0.3 to **25**, not 20. At populations from capacity through just below twice capacity, the factor is 0 and growth stops. Larger over-capacity states can have negative growth. There is no source capacity clamp or automatic removal of the overshoot. Conquered population 4 at rate 0.3 gets rounded growth 1, then halves to 0. Existing IU receives exactly the same signed newborn change; no separate growth tax is imposed.

During the same growth pass, remembered enemy intelligence with `EnemySeenOwner == Player`, capacity >10 and seen defense <12 increments that defense estimate. This occurs even if current ownership is None. It is deterministic bookkeeping from `invest`, not a new enemy decision or RNG draw.

After all growth, a colony's budget is its IU **at entry to inv_player**. Infrastructure bought now increases later recurring income, but cannot enlarge the current balance. For example, IU 10 grants budget 10; buying 2I costs 6, leaves budget 4 and persistent IU 12. Spending 4R finishes the phase while IU remains 12. The next cycle grants the then-current IU, without carrying the old budget forward. Every colony must finish its own budget before the next colony or year update.

| Token | Immediate effect and cost |
|---|---|
| I | 3 per additional IU; cannot exceed 2 × current inhabitants. Planet capacity is not the infrastructure limit. |
| S / C / B | Scout 6, cruiser 16, battlestar 70. Ships appear immediately in a fresh stationary production fleet; there is no build-time queue. |
| M / A | Missile base 8, advanced missile base 35. Require nonzero population and an unconquered colony. Only existing defense fields change; no combat engine is added. |
| T | 1 per transport, also removes that many inhabitants. IU becomes `min(oldIU - amount, 2 * remainingPopulation)`. Requires sufficient people and an unconquered colony. |
| R / V / W | 1 per research point, using original thresholds/progress and multiple breakthroughs. An affordable normal request is charged fully even at the maximum level. Effects apply after this turn's movement; existing journey ETAs are unchanged. |
| *x | Maximum affordable item count, attempting at least one for ships/bases/I. *I also respects support capacity. *T uses min(population,balance). *R/V/W invests only up to the **next** breakthrough; at the cap it costs zero and leaves the budget to be spent another way. |
| + | Spend on as many battlestars as possible, then cruisers with the remainder. |
| - | Spend on as many advanced missile bases as possible, then missile bases. Same abandoned/conquered restrictions as defenses. |

T can remove the last inhabitant. Ownership becomes None and IU/MB/AMB become zero; capacity and intelligence are not reset. The already granted balance only loses the transport cost. Its remaining funds can still buy ships or research from that abandoned planet. Ordinary I is permitted on a conquered colony if support allows it; defenses and transports are not. Original error spelling/case, including `can't affort` and `conqered`, is retained.

Quantities precede tokens, default to one, and run sequentially. Repeated tokens use updated state. Zero quantities are permitted. Unknown or unaffordable tokens report errors without charge, and later tokens continue. All tokens on the submitted line still execute after balance reaches zero. Undefined C signed-overflow quantities are rejected safely instead of creating wrapped/free purchases.

## Production fleets and ownership

C allocates the lowest free player slot for **every** player colony before reading its spending line, even for zero income or research-only investment. The existing `get_tf` initialization behavior is reused, including retaining a reused slot's withdrawal flag. During spending the temporary fleet is visible in appropriate star reports once it has ships, but its map marker is deferred until finalization, matching C's cached map behavior.

After the submitted line exhausts the budget, the new fleet absorbs every other stationary player fleet at that exact destination, including arrivals from this turn. Empty production fleets are removed. Old slots retain inactive fields/counts until reuse. This automatic merge checks **destination**, not manual J's preserved X-only defect; remote same-column fleets are not absorbed. Thus research-only spending can change fleet letters. C prints its final report before merging; C# refreshes afterward so its report and map agree, following the existing screen-layering contract.

`move_ships` calls `revolt` before investment. A conquered planet with no stationary cruiser/battlestar of its current team flips to the other team, clears conquered, and refreshes remembered player capacity. Arrivals can supply the guard before this check. No damage, conquest battle, enemy orders or RNG is introduced. Guard/colony presence uses authoritative collections; cascades caused by corrupt legacy counters after the remote-J defect are not fabricated.

## Classic interaction

G/space and declined Q confirmation enter the Classic turn workflow. On an investment year, the screen shows the original `$$$$$$$$$ Investment $$$$$$$$$` heading and prompts such as `A3>  4?` on row 19, cursor at column 8. The event pane shows the old displayed turn and production year 0 while budgets are pending. Only after all colonies finish do turn and production year increment. A zero budget still requires one line, matching C's do/while loop.

H shows the original investment help. `>` on a submitted line asks for a **separate immediate key** M/S/C/R, then any required report input, and returns to the same budget. C does not consume `>M` as a combined shortcut: text left after `>` remains investment tokens after the separate key is read. For example, line `>C`, followed by immediate M, redraws then attempts a cruiser purchase. This unusual sequence is regression tested.

There is no G/space/blank-line skip or Q exit within `inv_player`: resources must be spent. Normal research requests can consume a remainder even at a research cap. EOF exits safely with the Core session retained in memory; another facade/loop can resume without repeating movement, growth or credits. No persistence feature is implied. H/report output remains readable until the next submitted line instead of being immediately overwritten by C's next prompt redraw. Investment help retains its original 18 rows; ordinary reports retain the established pane/pagination limits.

Example: L lands 3 inhabitants with IU 3 at capacity 20. The first three G commands leave both unchanged. G 4 produces population/IU 4/4 and budget 4. `4R` plus Enter spends that budget, leaves IU 4, automatically merges fleets, and completes displayed turn 5 with production year 1.

## Core boundaries and explicit remaining gaps

- `BeginClassicTurn` performs movement and revolt. The UI acknowledges exploration, then `PrepareClassicEconomy` applies growth once and opens the ordered sessions. `InvestInClassicColony` owns costs/effects; `FinishClassicInvestmentLine` completes a colony only after all line tokens. `CompleteClassicTurn` increments the clocks. State owns the clock/session/queue, preventing new Game facades from resetting budget or replaying growth.
- `Game.Tick` and the development shell remain **movement-only** and do not run Classic economics or advance its production-year clock. They cannot bypass a pending Classic phase. `Game.Turn` still counts completed phases from zero, now shared in GameState. Direct fleet ticks still do not increment the turn count. Mixing debug ticks into a Classic calendar is not a gameplay workflow.
- Fleet battles/withdrawal and planetary attacks resolve before growth; see [encounters](ClassicEncounters.md). Unresolved combat retains its input session. Enemy investment now executes automatically in the common ordered queue, before/between/after player prompts as C dictates; the enemy-owned-colony boundary is removed for initialized Classic games. Unarmed co-location remains permitted. Undefined growth/slot/AI states retain explicit boundaries; see [enemy audit](ClassicEnemyAI.md).
- C's `inv_player` fails to check `get_tf == 0` at fleet-slot exhaustion, writing its inactive dummy slot and potentially losing fleets on merge. C# refuses that state. Initial exhaustion stops before the due movement phase. A later colony exhausting slots retains the unfinished queue and prior legitimate effects; no growth or spending is replayed. Freeing a slot explicitly permits continuation of that same pending turn.
- Owned zero/negative capacity is rejected before growth: C integer division there is undefined. None-owned zero-capacity planets are skipped normally. Normal model values preserve original integer arithmetic; corrupt/overflowing state is not assigned invented growth results.
- The last configured turn skips investment, as C does. The full-turn host now checks original elimination/overwhelm/time-limit outcomes and shows the final score; see ClassicEndgame.md. Core score rules consume no RNG and run only after all due investment/combat, or confirmed quit. No save format change is added.
- Enemy inv_enemy now follows original minimum bases, conditional rnd(5)/rnd(7)/rnd(2) purchases, rnd(12) spending, transport quantities and research-field rerolls; see [enemy AI](ClassicEnemyAI.md). Player growth/investment/revolt/reports still consume zero draws. Enemy decisions use the existing shared RNG in source order, before movement and during ordered investment. Whole-game platform-C PRNG equivalence remains unestablished.

## Regression coverage

`EconomyTests` checks literal C arithmetic cases (halves, overshoot, integer division and conquered halving), fourth-year scheduling, all-growth-before-spending order, local budgets and recurring IU, every purchase cost, construction/arrival merging, max/bundle spending, evacuation/abandonment, research thresholds/caps and journey timing, shared-phase guards, zero budgets, intelligence aging, revolt, fleet-slot boundaries, unsupported enemy/contested states and the last-turn condition. `ClassicEconomyTests` scripts landing/advancement, exact prompts and clocks, errors/EOF/resumption, help and separate report keys, construction, infrastructure, abandonment and ordered colony budgets. Existing movement/startup/J/N/L/research/debug-shell tests remain in the full suite.

Comparison uses inspected C and source-derived state/screen fixtures. A compiled legacy execution and physical Windows console review remain outstanding. No new RNG implementation or persistence is included. Enemy turns added subsequently follow ClassicEnemyAI.md. Player combat added subsequently is described in [ClassicCombat.md](ClassicCombat.md).

Validation: `dotnet test Conquest.sln -c Release --no-restore -p:OutputPath=bin/EconomyValidation/` passed all **583 tests**, including 72 new economic/interaction cases. `git diff --check` passed. `dotnet build Conquest.sln -c Release --no-restore` rebuilt the normal `bin/Release/net8.0` output with zero errors. NuGet reported NU1900 because vulnerability data was unavailable. A process check of that build completed seeded startup, L landing of three inhabitants, four G phases, the original `A1>  4?` prompt, `4R` spending, population/IU 4/4, the automatic fleet-letter change, C/T reports and confirmed quit. Debug-shell startup/quit passed.

## Changed files

- Core: new `Game.Economy.cs` and `ColonyInvestment.cs`; updated `GameState.cs`, `Game.Turns.cs`, `FleetSystem.cs`.
- CLI: `ClassicGameLoop.cs`, `ClassicScreen.cs`.
- Tests: new `EconomyTests.cs`, `ClassicEconomyTests.cs`; updated `ClassicLandingTests.cs` for the restored first investment cycle. Existing movement-only debug tests remain unchanged.
- Documentation: `README.md`, new `ClassicEconomy.md`, `ClassicCommandAudit.md`, `ClassicTerminal.md`, `ClassicLanding.md`, `ClassicFleetCommands.md`, `ClassicMovement.md`, `DevelopmentBacklog.md`, `ProjectPlan.md`, `architecture.md`.
