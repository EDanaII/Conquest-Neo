# Classic landing and immediate colonization

Implemented 2026-10-30. Primary specification: `OriginalSource/command.c:land` (line 201), `input.c:get_line/get_token`, `update.c:zero_tf`, and `display.c:print_star/print_col`. Population growth and production belong to `update.c:invest` and are outside this milestone.

## Original command sequence

All screen coordinates here are one-based, matching C.

1. L displays `Land Taskforce: ` at column 2, row 19, and reads **one fleet letter**, without Enter. C echoes the selected letter at column 17, overwriting the prompt's final space. Lowercase input is normalized to uppercase.
2. Validate letter range, active destination, then ETA zero. Errors are `  !illegal tf`, `  !nonexistent tf`, and `  !tf is not in normal space`. A pending pre-departure order also blocks landing; L does not cancel a course. Unlike D/J/N, C's L does not check blasting or withdrawal flags, so no such restriction was added.
3. Refresh the fleet's star report. Reject a star with no planets (`  !no planets at this star`) before checking stationary enemy fleets (`  !enemy ships present`). The latter uses stationary destination IDs, not enemy X/Y, remaining transport counts, or fighting strength. A travelling enemy destination is not yet a stationed enemy fleet.
4. Show `planet: ` at column 10, row 20. With exactly one planet, automatically select and echo its actual number **without reading a key**. With multiple planets, read one character and select that numbered planet. C converts the character by subtracting `'0'`; invalid letters/control keys therefore echo their converted number before ` !Not a habitable planet`. There is no star-selection prompt: the fleet's current star is authoritative.
5. Reject an enemy-owned planet, or a player-owned planet with `conquered`, with `  !Enemy infested planet  !!`. This happens before transport input. Neither path initiates combat, conquest or revolt.
6. Show `transports: ` at column 6, row 21 and read the existing uppercase/backspace line, limited to 25 characters. Take only the **numeric value of the first C token**. Ignore its symbol and all later tokens.
7. Zero means all transports currently in that fleet. Reject values less than one (`  !illegal transports`), more than available (`  !only NN transports in tf`), then more than free planetary capacity (`  !only room for NN transports`), in that order. Requests are not clamped or partially applied. Exact equality with availability or free capacity is allowed.

## Immediate state changes

On success, assign the planet to PLAYER, add the landed amount to inhabitants and industrial units, and subtract exactly that amount from the fleet's transports. One transport is consumed for each population/IU unit; no transport hull is returned or allocated elsewhere. Existing population/industry are incremented, not replaced. Capacity, remembered capacity, defenses, conquest/attack flags, enemy intelligence, technology, research and money are untouched.

New colonies normally start with zero inhabitants; C increments its player colony counter in that case. Reinforcement of an existing unconquered player colony uses the same immediate rule. C# derives colony presence from planet ownership instead of adding a parallel `col_stars` counter model. The map's existing visited-star status becomes `@`, while normal fog and attack-symbol rules remain in force. Landing does not explore an unknown star or refresh capacity intelligence.

`zero_tf` removes the fleet if all ship counts become zero. Surviving scouts/cruisers/battlestars retain the fleet even after its last transport lands. Removing one of several fleets restores the remaining fleet glyph or `*`; a colony remains visible when the last fleet disappears or later departs. Core reuses the existing empty-fleet cleanup and board-reference rebuild without moving other fleets or resetting unrelated flags.

The star report refreshes before target selection and after success. Input receipt lines remain visible until the next command, as in C. T and C reports subsequently read the new fleet/colony contents. No successful-landing message, movement tick or RNG draw is invented.

## Input quirks and cancellation

- Blank input and `0` mean **all transports**, not cancel. All still has to fit; a capacity failure leaves everything unchanged. With no transports, blank/zero fails as illegal transports, whereas explicitly requesting one reports only zero available.
- `get_token` returns quantity one for a nonnumeric first token, and L ignores the token itself. Thus `X`, `?` and `-3` each request one transport. The minus sign is a token, not a negative quantity. `2T9S`, `2 99` and numeric-only `2` each request two. `0X` still means all. This differs from N, where an empty/blank first **symbol** can mean whole-fleet splitting.
- A full or decimated planet is not specially rejected before the quantity prompt: its actual free capacity produces the room error. Landing uses `capacity - inhabitants`, not remembered capacity, and reports negative remaining room verbatim in malformed/legacy over-capacity state.
- Enemy ownership is rejected even at zero inhabitants. A conquered player colony is rejected even without enemy ships. Conversely, NONE with a stale conquered flag is not rejected by C's ownership condition; landing leaves the flag intact. No extra eligibility rule or flag repair was added.
- There is no explicit C Escape-to-cancel workflow. Invalid immediate fleet/planet selections return without effects; a valid transport line commits. EOF at any stage safely exits without consuming transports or changing ownership. Existing console control-character filtering, CRLF normalization and line editing remain unchanged.
- C signed integer overflow during quantity parsing is undefined. Unrepresentable numeric prefixes are safely mapped to the original illegal-transports rejection, with no mutation. They do not create wrapped quantities or partial landings.

## Unsupported dependencies and remaining limits

No L path calls combat in the C source. Hostile fleet/planet and conquered-colony paths are isolated by the original refusals; there is no stand-in battle or automatic conquest. Shared-start encounter state is not resolved or cleared by landing. Future combat/revolt systems may make additional targets eligible, but are not part of L itself.

C's later `invest` phase now has a player implementation through Classic G/space: fourth-year growth, recurring IU budgets and original colony spending. See [economy](ClassicEconomy.md). Debug `Game.Tick` remains movement-only; L itself still only applies its immediate transport/population/IU changes. Enemy investment, combat, endgame and persistence remain deferred.

The existing monochrome screen, startup flow and 17-row report-pane boundaries remain. Automatic star reports can clip long reports; S/T pagination remains available. No new input framework, colony-count cache, combat abstraction or occupancy redesign was introduced.

## Implementation and comparison

`FleetSystem.ValidatePlayerLandingFleet`, `PlayerLandingPlanets`, `ValidatePlayerLandingPlanet` and `LandPlayerTransports` mirror the validation stages needed by the original prompts. All state-changing rules are in Core and revalidated before commit. The existing Classic loop supplies selection/quantity text and redraws; its line reader now also supports the original third input row without changing the J/N/report sequences.

Source-derived regression fixtures in `LandingTests` cover new colonies, repeated resupply, exact capacity, insufficient ships/room, zero capacity, enemy presence by destination/ETA, enemy/conquered planets, invalid/unused/moving fleets, target lookup, last-ship removal, retained flags/intelligence and no later invented growth. `ClassicLandingTests` checks exact prompt rows/cursors, automatic vs manual planet selection, parser quirks, editing, cancellation/EOF, failures without partial changes, fleet/colony reports, map restoration and colony persistence after departure. RNG stubs reject unexpected draws. Existing startup, research, J/N, movement and debug-shell tests remain in the full suite.

Comparison is against inspected C source and explicit rendered/state fixtures. A compiled legacy execution or physical Windows console comparison was not performed.

Validation: `dotnet test Conquest.sln -c Release --no-restore -p:OutputPath=bin/LandingValidation/` passed all **511 tests** (76 landing cases replace the old L-unavailable case). `dotnet build Conquest.sln -c Release --no-restore` also succeeded with zero errors and rebuilt the normal `bin/Release/net8.0` output. Both commands reported NU1900 because NuGet vulnerability data was unavailable. A process check of that normal build completed startup, landed three transports at seeded star A, showed population/IU 3/3 and fleet transports reduced from 35 to 32, displayed T/C reports and confirmed quit. The debug shell startup/quit check and `git diff --check` passed.
