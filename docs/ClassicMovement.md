# Classic fleet movement

Implemented 2026-10-29. This replaces the conversion's Manhattan travel scaffold, preserving zero-based Core star indices, one-based fleet slots, and CLI letter/numeric identifiers.

## Source rules and conversion changes

| Area | Original C reference and semantics | C# implementation |
|---|---|---|
| Distance | `input.c:dist`: Euclidean distance between stars. | `FleetSystem.SetCourse` replaces Manhattan distance. |
| Distance tolerance | `movement.c:set_des`: `dst = (int)(r - 0.049 + 1)`. For example, sqrt(122) maps to 11, while sqrt(101) maps to 11. This is not simply ceiling(distance). | Same double arithmetic and truncation. |
| Range | `set_des`: reject `dst > range` if any transports, cruisers, or battlestars are present. Scouts alone have no range limit. Equality is permitted. | Uses player's researched range. Debug states without Classic initialization use starting range 5 when technology is unset. |
| Travel time | `set_des`: `((dst - 1) / velocity) + 1`, integer division. `r < 0.5` leaves the fleet stationary. | Uses player's researched velocity, stores initial ETA and departure coordinates. Uninitialized debug velocity uses starting velocity 1. |
| Orders | `command.c:send_tf`: reject unused fleets, blasting fleets, and traveling fleets unless ETA still equals original ETA and the fleet has not withdrawn. | Validates in Core; existing CLI placement/ship guards retained. No time advances when ordering. |
| Replacement | `set_des`: cancel a previous pre-departure order before checking the replacement's range. Failed replacement range leaves the fleet stationary at departure; ordering the current star cancels travel. Original ETA is retained on cancellation. | Same behavior for valid destination identifiers. |
| Movement | `movement.c:move_ships`: decrement ETA once, then for player coordinates compute `origin + round((1 - eta/origeta) * (destination - origin))`. | Both axes interpolate from fixed departure coordinates, never from the previous rounded position. |
| Rounding | `utils.c:round`: truncate `x - 0.5` for negatives and `x + 0.5` otherwise. | Exact addition/subtraction and integer cast, including floating-point effects near halves. |
| Player arrival | `move_ships`: retain destination at ETA zero; explore unknown stars; surviving fleets visit the star and learn every planet's capacity, including revisits. | Same state effects. Null destination means unused/destroyed, not stationary; star index zero is valid. |
| Enemy movement | `move_ships`: decrement enemy ETA without changing enemy x/y. At arrival mark visited, refresh planet owner intelligence, and join an existing stationary enemy fleet at the destination. | Same behavior for already assigned enemy journeys. Enemy orders/AI are not implemented; `SetCourse` accepts player orders. |
| Board | `move_ships`: clear moving player markers before moving, then redraw all surviving player fleets. | Rebuild cell fleet references after all moves, preventing crossings/colocation from erasing fleets. Collections drive `tf list`; cells retain one fleet per team. |
| Time | `main.c`: player orders, enemy decisions, movement, battle, optional investment, then `update.c:up_year` increments turn. | Debug `Game.Tick(n)` remains movement-only. Classic G uses begin/process/complete phases: movement/revolt, optional player growth/investment, then shared turn/production clocks. Orders do not tick. Combat and enemy AI remain absent; see [economy](ClassicEconomy.md). |

## Exploration semantics

`movement.c:fleet_explore` and `lose` are reproduced without changing their apparent quirks:

- For each nonempty ship type in transport/scout/cruiser/battlestar order, draw `rnd(5/10/10/3)` and assign `(base + draw * shipCount) / 100.0`, with bases 10/70/90/97. Each assignment overwrites the shared probability; the last nonempty type controls losses for every type.
- Preserve the source's `if (prob > 100) prob = 100` after division; do not substitute a probability cap of 1.
- Then draw `rnd(100)` for every original ship in transport/scout/cruiser/battlestar order. A ship is destroyed only if `draw / 100.0 > probability`; equality survives. Draws still occur when probability exceeds 1.
- If no ships survive, clear destination and do not mark the star explored or update capacity intelligence. Otherwise retain destination and mark visited.
- Known stars and Core `GameConfig.SafeExplore = true` bypass loss draws. The default is false, matching C configuration. There is no new CLI configuration switch.

`Game.Tick` uses the existing game RNG. Direct `FleetSystem.Tick()` remains available; an unsafe unknown arrival requires passing an RNG, otherwise it rejects before advancing any fleet. This avoids creating a second hidden random stream. RNG failures can interrupt a phase; transactional recovery remains outside this milestone.

## CLI behavior

`move a C`, `move A c`, and `move 1 2` are interchangeable; `course` is an alias. `place A z 4` remains a debug operation and creates four scouts in slot 26. Orders cannot change after their first movement tick. `tf list` shows stationary fleets at their current star and traveling fleets with destination and remaining ETA; it does not label an intermediate star as the fleet's stationary location. `tick` reports surviving arrivals, exploration losses, and fleets destroyed during exploration. Arrival at star A/index zero remains listed and can depart again.

## Validation and remaining boundaries

The subsequent [enemy AI milestone](ClassicEnemyAI.md) supplies original enemy orders before Classic movement and corrects an arrival conversion defect: C retains the arriving fleet slot and absorbs the first existing stationary enemy fleet, not the reverse. This matters for live-slot AI iteration and subsequent allocation/RNG order. The movement regression now checks that exact surviving slot. Combat, withdrawal and enemy production are also implemented subsequently; older scope notes below describe the earlier movement-only milestone.

`ClassicMovementTests` contains an independent source transcription of C distance/ETA/interpolation/rounding. It checks all 841 signed board displacements at every velocity 1–12 (10,092 scenarios), comparing every tick position and ETA. Additional deterministic fixtures cover range tolerance, mixed fleets, replacement/cancellation, withdrawn/blasting guards, known/safe/unsafe arrivals, loss draw order and strict comparisons, complete destruction, enemy arrival joins, crossing fleets, and letter/numeric CLI arrival listings. Existing startup and CLI tests are retained with scaffold expectations updated.

No C compiler is installed in the validation environment. These are comparisons against transcribed original C logic and explicit source-derived fixtures, not executions of a compiled legacy game. Exact `CRandRng` sequence equivalence with platform C `rand()` remains unestablished; exploration draw order and inclusive bounds are tested with scripted RNG values.

Remaining differences and scope boundaries:

- `Game.Turn` and CLI `Turn=` retain completed-phase counts from zero; Classic displays one greater, matching C. The count is now shared in GameState alongside the production clock and pending investment. Direct fleet ticks still do not increment it; debug ticks do not run Classic economics. See [economy](ClassicEconomy.md).
- Malformed or unavailable CLI identifiers are rejected before Core orders run, preserving the identifier usability fix. C prompts cancel a prior order before reading/checking the replacement star and can index invalid memory for some inputs; those invalid-input effects are not reproduced.
- Enemy x/y intentionally remain unchanged during and after travel, matching `move_ships`. C AI generally uses stationary destination IDs. No enemy order-generation formula is substituted for player orders.
- General `tf_stars`/colony counts and player/enemy arrival-event arrays are absent. Stationary fleet queries and CLI messages cover movement bookkeeping; there is no combat/encounter/revolt resolution. Post-startup arrivals at hostile stars do not fight. The existing shared-start encounter gate remains enforced.
- Classic N/J, L and player investment/research are now implemented; see [fleet commands](ClassicFleetCommands.md), [landing](ClassicLanding.md) and [economy](ClassicEconomy.md). Withdrawal execution, enemy economic decisions, combat and persistence changes remain absent. Debug placement stays free and can overwrite a cell's single per-team reference until the next reference rebuild; `tf list` still enumerates every placed fleet.
- Existing JSON snapshots do not preserve fleets, departure coordinates, turn state, or SafeExplore. They remain incomplete prototypes.
