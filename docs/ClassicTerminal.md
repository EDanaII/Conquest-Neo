# Classic terminal interface

Current enemy behavior: [original enemy orders, colonization and investment](ClassicEnemyAI.md) now run automatically on G/space. E Arrive/E Depart letters appear only at player-observed locations in the existing event pane. No hidden colony details, new command letters or input redesign are added.

Investigated and first interactive increment implemented 2026-10-30. Player economy, B/P? planetary combat, B? fleet battles/withdrawal and immediate enemy planetary responses have since been restored. Enemy orders/investment now follow ClassicEnemyAI.md; wandering now uses the documented C-index correction; original ending checks and final tally now follow ClassicEndgame.md. See [the corrected encounter progression](ClassicEncounters.md).

## Original C findings

- `defs.h`, `display.c:printmap`, `update.c:update_board`: intended 80x25 screen with one-based cursor positions. Map (1,1), reports (50,1), input (1,18), events (32,18), research (55,18). Sector (x,y) starts at column `3*x+1`, row `16-y`. The 15x15 map descends from y=15 to 1; axis labels mark 1 and multiples of 5. Hard-coded row positions assume 15 rows.
- `main.c:EMPSEC`, `utils.c:dispStar/dispTF`: three independent characters per sector: status, star, player fleet. Empty is ` . `; stars A-Z; fleets **lowercase a-z**. Status `?` unexplored, blank visited, `@` player colony, `!` attack. Terminal pens distinguish statuses.
- `misc.c:on_board`, `movement.c:move_ships`: multiple player fleets produce `*` in the third column. Enemy fleets are not map glyphs. Movement clears moving fleet markers, advances positions, then redraws survivors; star/status remain independent. `on_board` rescans all fleet slots. Mutable C markers can retain stale `*` values in some combinations; deriving the glyph afresh avoids this presentation defect.
- `display.c:print_star`: star positions are public before exploration. Reports require a player visit; capacities are remembered. Current colony/enemy details require a stationed player fleet or colony. This differs from the conversion's debug fog/reveal abstraction.
- `init.c:startup/init_player`: welcome and character pause, initialization, automatic map before star selection and quantity-prefixed purchases, then starting-star report. Shared starts invoke battle.
- `command.c:inputplayer`, `misc.c:help1`: immediate uppercase-normalized B blast, C colonies, D destination, G/space done, H help, J join, L land, M map, N new fleet, Q quit, R research, S stars, T fleets. `?` does nothing. Unknown keys show `!illegal command`; E is debug-only.
- `send_tf`, `movement.c:set_des`: D reads one fleet letter and one star letter without Enter, then reports distance/ETA. S/T/R take lines; blank selects all. `input.c:get_line` echoes uppercase, supports backspace, and caps at 25 characters. The original tokenizer also supports quantity prefixes. Q clears the screen, asks `Quit game....[verify]`, and exits only on Y.
- `printmap` clears/redraws map, technology and event headings. Changes usually update affected sectors and clear input/report rectangles. `update_board` currently redraws all three characters regardless of its left/right argument. `clear_field` is commented out. Long reports pause for a key.
- `machine.c`: Windows uses `_getch` and Win32 cursor/color calls, translating one-based positions. Amiga temporarily enables raw input and emits terminal controls. Linux uses `getchar` (potentially canonical/echoed depending on external setup) and ANSI positioning. Modern .NET can avoid busy waits and shell `cls`/`clear` calls.

## Implemented increment and comparison

Previously Program entered the line-oriented debug shell with no galaxy and explicit map/refresh commands. That shell remains available unchanged through `--debug-shell`.

Default mode now generates and initializes Classic state, reuses `ClassicStartupFlow`, and automatically enters `ClassicGameLoop` after purchases. `ClassicScreen` purely composes the original pane positions, descending map and three-character sectors. It reads all player fleets rather than a cell's single fleet reference. Core coordinates remain zero-based; gameplay labels/reports add one. Smaller maps align to the original bottom row. Unknown stars retain letters and `?`; debug reveal preferences cannot expose reports or enemy glyphs.

Supported keys: B, D, G/space, H, M, Q, S, T, R, C, J, N and L. Orders call Core course preparation/`SetCourse`; G uses Classic begin/process/complete turn operations. J/N use Core joining/splitting with automatic map and star-report updates; see [fleet commands](ClassicFleetCommands.md), including the preserved X-only J defect. L restores immediate landing/colonization and colony resupply; see [landing](ClassicLanding.md). B restores deterministic planetary bombardment, and G can enter the optional P? planetary salvo/capture/break-off loop; see [combat](ClassicCombat.md). Shared-start fleet combat resolves before movement; see ClassicEncounters.md. S/T support edited token lists, blank/all, and pagination. Q requires Y; declining also ends the order phase, as in C. See [command audit](ClassicCommandAudit.md).

`IClassicTerminal` separates keyboard/screen I/O. `ClassicConsoleTerminal` blocks on `Console.ReadKey`, normalizes Enter/backspace, and updates changed rows on consoles at least 80x25. Screens reserve the last column to avoid wrapping. Redirected output and small consoles receive plain snapshots; redirected input consumes characters and EOF exits cleanly. Resize/I/O failures fall back to snapshots.

## Remaining differences

- Startup still uses existing line-based purchases and a zero-based selection map, without the welcome pause or original in-screen initialization prompts. Restoring startup presentation is the next interface increment.
- Rendering remains monochrome; original pen colors remain unimplemented. Prompt cursor placement now follows typed input.
- Star summaries include original visibility-dependent planet/colony/enemy fields. T uses the original single-row inventory layout. C reports paginate. Research shows next-level requirements from Core. Reports use a 17-row pane instead of reproducing C's lower-pane overlap and inconsistent pagination.
- D now reproduces cancellation timing and distance/ETA/status text; debug-shell invalid-identifier handling remains unchanged.
- G/space now performs the supported Classic movement/revolt and fourth-year player economy before incrementing turn/production clocks. Exploration acknowledgements precede investment/year updates, though Core still finishes all fleet movement before individual arrival screens. Visible enemy departures and normal combat/AI/endgame orchestration are now implemented within documented source-safe boundaries. See [economy](ClassicEconomy.md).
- M forces a clear/redraw and clears reports/events. Quit verification uses a clear screen. Snapshot output adapts to limited consoles.
- Player colony investment uses the original banner, budgets, H, construction/research tokens and > report subprompts. Temporary production fleets stay off the map until finalization. Combat now resolves before growth and after spending. Enemy orders/investment now follow ClassicEnemyAI.md; wandering now uses the documented C-index correction; original ending checks and final tally now follow ClassicEndgame.md. This remains an incomplete Classic game.

## Validation

`ClassicTerminalTests` compares source-derived pane/coordinate fixtures and scripted key streams: lowercase symbols, shared locations, hidden enemies, unknown reports, colony preservation, departure/intermediate/arrival restoration, automatic refreshes, orders, editing, quit confirmation, invalid input and unsupported commands. Existing debug-shell/startup/movement tests remain in the full suite.

No gcc, clang or MSVC compiler was found. Comparisons use inspected C source and rendered fixtures, not a running legacy executable. Interactive Windows cursor behavior still needs manual review; fake-terminal tests cover screen composition and input behavior.

Validation: `dotnet test Conquest.sln -c Release --no-restore` passed all 338 tests (14 terminal cases plus the existing 324). NuGet emitted NU1900 because vulnerability data was unavailable. Redirected process smoke checks completed Classic startup/screen/confirmed quit and debug-shell startup/quit. `git diff --check` passed.
