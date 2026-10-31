# Classic task-force joining and splitting

Implemented 2026-10-30 using `OriginalSource/taskforce.c` as the specification. Player rules live in `FleetSystem`; the existing Classic keyboard loop only translates input and presents results. No combat, landing, economy, AI decisions or persistence operations are added.

## Original input and rules

### N — New/split task force

`make_tf` (line 10) displays `New tf- from tf: ` at column 2, row 19 and immediately reads one uppercase-normalized fleet letter. It rejects out-of-range, unused or nonstationary sources with `  !illegal tf`, then rejects blasting. Even a newly issued travel order with no movement yet makes the fleet ineligible; N does not cancel it. Withdrawal by itself is not checked when ETA is zero.

`split_tf` (line 43) calls `get_tf` (line 184), which allocates the first slot 1–26 whose destination is unused. If all 26 are active, it displays `! Taskforce limit reached!` and returns without reading a ship line. It cannot reuse the source slot even when moving the entire fleet. `make_tf` still refreshes the source star report on this limit error.

The ship prompt is ` ships:` at column 11, row 20; text starts at column 19. The existing 25-character, uppercase/backspace line reader is used. Ship tokens are quantities before T/S/C/B (transport/scout/cruiser/battlestar); an omitted quantity means one. Tokens run in input order and each requested amount is capped by that type's remaining count. Repeated types operate on the remaining source, not the initial count. Zero is allowed.

Blank input moves every ship to the new slot and removes the emptied source. This is **not cancellation**. The original tokenizer also returns its blank/end token for a first numeric-only token or a quantity followed by a space, so `2` and `2 S` likewise select the entire fleet. `-2S` means an invalid `-` token followed by a valid `2S`, not a negative quantity. Unknown ship tokens display `  ! Illegal field X` and processing continues; valid transfers are not rolled back. The last error remains visible, including when subsequent tokens succeed.

The new slot starts at the source destination star with zero ETA/original ETA/blasting and fresh departure coordinates. Its old ship counts are cleared. As in `get_tf`, its old withdrawal flag is retained. `zero_tf` in `update.c` removes source/new fleets with no ships by clearing their destination. A zero-only or invalid-only selection therefore leaves no empty new fleet. Removal does not need to erase other inactive slot data.

### J — Join task forces

`join_tf` (line 121) displays `Join tfs:` at column 2, row 19 and reads a line. The first token identifies the parent, subsequent tokens identify children. Quantity prefixes are consumed but ignored: `2A3B` joins B into A once. A blank line is an illegal parent, not a successful cancellation. A valid parent by itself is a no-op followed by a star report refresh.

Parent validation occurs in this order: letter range, ETA greater than zero, used destination, blasting. An invalid parent stops the whole command. Child validation occurs in this order: letter range, duplicate of parent, used destination, location (see defect below), nonzero ETA, blasting. A bad child reports an error and later children still run. Repeating a successfully joined child reports the original misspelled `!Nonexistant TFB`; ships are not counted twice.

`joinsilent` (line 206) adds all four ship counts to the parent and marks the child unused by clearing its destination. Its other fields/counts remain in the inactive slot, as in C. Slot reuse clears these counts before adding newly split ships. J does not call `zero_tf` on the parent: an already empty active parent with no successful transfers remains active, while a valid child can supply its ships. Destroyed/unused slots cannot be joined regardless of stale counts.

Both commands keep player fleet letters and draw `*` for multiple active fleets at one coordinate. Successful commands refresh the map and source/parent star report automatically; T subsequently reads current fleet data. They consume no RNG, money, research or movement phase. The existing board-reference rebuild is reused without clearing unrelated blasting flags, changing visibility, or moving enemy fleets. The existing enemy-arrival merge reuses the same ship-transfer helper.

## Exact J Y-coordinate defect and policy

`taskforce.c:164` contains:

```c
if (tf[PLAYER][tf2n].x != tf[PLAYER][tf1n].x || tf[PLAYER][tf2n].y!= tf[PLAYER][tf2n].y)
```

The second comparison is always false for valid integer fields. The effective test is therefore **different X means reject**. Matching Y and matching destination are not required. Two otherwise eligible stationary fleets at different stars with the same X coordinate can join, instantly transferring the remote fleet's ships to the parent without advancing time. Different X is rejected even if Y matches. Pending travel and blasting still block the relevant fleet.

The implementation deliberately **preserves this X-only Classic transfer rule**, and Core and UI tests explicitly demonstrate same-X/different-Y joining. Correcting the Y operand silently would change reachable gameplay. Recommendation: preserve it for the faithful Classic baseline; correct it only through a separately reviewed Classic-defect exception or modernization change. No new configuration mode or alternate rule is introduced here.

There are additional C bookkeeping consequences for a remote join: `joinsilent` decrements the parent's `tf_stars` count rather than the remote child's, and `on_board` only redraws the parent's coordinate. The remote glyph can remain stale and counters can disagree with active fleets, affecting reports. C# has no stored `tf_stars` counters and already composes maps/reports from fleet collections. It preserves transfer eligibility but does **not** fabricate corrupt counters or stale remote markers. References are rebuilt and the remote star is restored correctly, as required by the existing map-layering contract. Joining does not explore or update intelligence at either star.

## Explicit adaptation boundaries

- C reserves the empty slot before blocking on the ship line. C# validates eligibility/capacity before the prompt and allocates when a complete line is submitted. EOF exits cleanly without reserving an empty slot or transferring ships. The C reader has no usable EOF behavior; there is no competing successful C cancellation sequence to preserve.
- There is no new Escape-to-cancel command. Invalid immediate source keys return to the movement prompt. Blank ship input retains the original whole-fleet meaning. Existing control-character filtering, CRLF normalization, line-length limit and safe EOF behavior remain in place.
- A ship quantity outside signed 32-bit range reports `  !invalid ship quantity`, skips that token and continues. C's overflowing signed integer accumulation is undefined; arbitrary overflow-driven transfers are not a Classic rule. Ordinary large representable requests are still capped to availability.
- The report-pane, monochrome and startup limitations remain. Large reports remain inspectable through S/T pagination; automatic star reports retain pane clipping. Classic G now includes [player economy](ClassicEconomy.md); debug `Tick` remains movement-only. Production auto-merges exact-destination fleets and does not reuse manual J's X-only defect. No general occupancy redesign or legacy counter model was added.

## Regression coverage

`FleetLifecycleTests` covers all ship types, repeated/clamped/zero quantities, whole and empty splits, invalid tokens, lowest free slots, 26-slot exhaustion, stale-slot reuse, pending travel/blasting, joining order/error precedence, duplicate children, destroyed/empty fleets, no-op joins, the X-only defect, remote-star restoration, retained flags and untouched enemy/intelligence/technology state.

`ClassicFleetCommandTests` scripts N/source/ship input and J/token lines, verifies prompt columns/cursor placement and editing, blank/numeric-prefix behavior, invalid/overflow input, safe EOF, slot limits before further input, automatic `*`/letter map changes, refreshed star and fleet rows, partial-success errors, preserved X-only joining and subsequent scout movement. RNG stubs fail on any unexpected draw. Existing movement, startup, research, star-summary and debug-shell tests remain in the full suite.

Comparison is source-derived; no compiled legacy executable or physical console comparison was performed.

Validation: `dotnet test Conquest.sln -c Release --no-restore -p:OutputPath=bin/FleetLifecycleValidation/` passed all **436 tests**. This adds 55 deterministic lifecycle/keyboard cases and replaces the two old J/N-unavailable cases. Existing movement, startup, research and debug-shell cases pass. NuGet emitted NU1900 because vulnerability data was unavailable. A redirected process check completed startup, N/scout split, T listings, J merge, refreshed T listing and Q/Y exit. `git diff --check` passed. Separate output avoids overwriting a running normal-build game.
