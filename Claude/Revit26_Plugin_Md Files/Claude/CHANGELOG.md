# Changelog

Tracks *why* a tool's live version on the ribbon changed, or a cross-tool
convention shifted — not a mirror of `git log`. Add an entry here whenever:

- a new `_V0xx` becomes the one wired into a `*Ribbon.cs` file (see
  `Claude/TOOL_VERSIONING.md`), or an old one is retired,
- a convention documented in `CLAUDE.md`, `Claude/ARCHITECTURE.md`, or
  `Claude/RIBBON_AND_ICONS.md` changes,
- a fix crosses more than one tool (shared parameters, shared services).

Routine single-tool bug fixes with no version bump don't need an entry —
the commit message is enough for those. Newest first.

Commit history before 2026-09-09 is terse/inconsistent ("Updated", "DOne",
"V020") and isn't reliably summarizable — use `git log` directly for that
period rather than trusting a reconstruction here.

---

## 2026-09-20

- Added baseline documentation set: `CLAUDE.md`, `Claude/ARCHITECTURE.md`,
  `Claude/RIBBON_AND_ICONS.md`, `Claude/TOOL_VERSIONING.md`.
- Ribbon: every button now shows a version tooltip; buttons with no
  surviving source were dropped.
- Added Edge Element pulldown with `RoofEdgeElementSections` V002.

## 2026-09-19

- Fixed a ribbon startup failure: V005 icon wasn't embedded, and dead V003
  buttons were still wired — dropped them.
- Fixed `AutoSlopeByDrain` V010 `Parameter 'source'` init failure.
- Ribbon: consolidated to one icon per tool, with versions listed as
  individual push buttons inside a dropdown (see `Claude/RIBBON_AND_ICONS.md`
  for the current pulldown-wiring pattern this led to).
- Added `ParaManager` V003 — 4-step wizard UX.
- Standardized run timing/logging across the `ByPoints` AutoSlope engines to
  match `ByDrains`.

## 2026-09-18

- `AutoSlopeByDrain` bumped to V010 — multi-roof support (see the "per
  Rafi's confirmed multi-roof decision (2026-09-08)" note in
  `Commands/AutoSlopeDrainCommand.cs`, and the `TransactionGroup` pattern
  this introduced, documented in `Claude/ARCHITECTURE.md` §5).
- Added `RoofEdgeAroundSections` V005 with V011-style section orientation.
- Callout COP window now displays its version as `V19.0`; its view grid was
  advanced to a sortable/filterable/searchable datagrid.
- In-progress updates across RoofTools, Sheet, FloorTools, and Setup tools.

## 2026-09-17

- `ParaManager` bumped to V002, adopting the shared `Settings`/`Log`
  services from `Shared/` instead of a local implementation.
- Root-tag brushes in `ParaManager` and `WorksetRenamer` switched to
  `DynamicResource` (from `StaticResource`) — a pack-URI/resource-lookup fix.
- Run start/end/duration now logged in all AutoSlope roof-slope tools.
- `AutoSlopeByDrain` V009: drain grid now shows circle diameter/radius.
- Drain tolerance matching restricted to the same roof edge/opening
  (cross-edge false matches fixed).
- Missing ribbon icons assigned to "Worksets From Links" and
  "By Drain (Multi) V009".

## 2026-09-16

- `AutoSlopeByDrain` bumped to V009.
- Added Tool 007, a boundary-scoped detail line generator.
- `ParaManager` added to the Manage ribbon panel; startup/build bugs fixed.
- Fixed an `AutoSlope_HighestElevation` type mismatch in `ByDrain` tools.
- AutoSlope shared parameters standardized across all RoofSlope tools —
  a cross-tool convention change (parameter names/types now consistent
  between `ByPoints` and `ByDrains` engines).

## 2026-09-15

- Added `SheetAutoRearrange` V026 with configurable priority groups.
- Ribbon: split buttons replaced with pulldown buttons for every
  multi-version tool (an earlier ribbon-layout iteration than the
  2026-09-19 one-icon-per-tool consolidation above) — fixed blank text and
  broken rendering on stacked buttons along the way.
  RibbonLayoutHelper's stacked-item text-rebinding workaround (see
  `Claude/RIBBON_AND_ICONS.md`) traces back to this pass.
  Combined Roof Tools grouped with the two Auto Slope By Point variants;
  single-tool roof panels merged; empty `QuickAcces` panel dropped.
- Ribbon icons renamed to encode their real size in the filename (the
  `_16`/`_32` suffix convention documented in `Claude/RIBBON_AND_ICONS.md`
  dates from here); all 51 icons shrunk to 16×16 with 32×32 kept as backups.
- Ribbon tab name and build output path bumped (recurring machine-specific
  `BaseOutputPath` bumps — see the `HintPath`/build-prerequisite note in
  `CLAUDE.md`).

## 2026-09-14

- Ribbon panels split by section, version numbers dropped from button
  labels, then flattened to plain icon buttons with no dropdown menus (an
  earlier layout tried and later revised — see 2026-09-15/09-19 above).
- Added Auto Slope V028 Excel export: run start/end time and total seconds.
- Slope Percentage made editable in AutoSlope V028; the HTML preview
  experiment for its window was dropped after being tried.

## 2026-09-13

- Added `AutoSlopeByPointRidge` V001 (V028 plus ridge-point handling) —
  the ridge-aware variant that later became one of the three coexisting
  "By Point" implementations described in `Claude/TOOL_VERSIONING.md`.

## 2026-09-11

- Superseded tool versions removed; all remaining active tool folders
  backed up.
- Combined Roof Tools gained a single "Run All" button, defaulting to the
  Auto Slope tab.
- Setup Tools converted from SplitButton to PulldownButton.
- Every ribbon button given its own icon (the per-button-icon convention
  in `Claude/RIBBON_AND_ICONS.md` dates from here — before this, only
  pulldown headers had icons).
- Added an optional max-slope filter to Multiple Points; fixed its edge
  extraction on sloped/shaped roofs.

## 2026-09-10

- Added Multiple Points roof tool (midpoint, quarter points, extra points
  on long edges).
- Added Combined Roof Tools window, merging 5 roof tools into one tabbed UI.
- Setup folder nesting flattened; added DWG To Detail Lines V002; dropped
  dead ribbon buttons.
- Setup tools split into Family/Project ribbon groups; added VA006 detail
  line tool.

## 2026-09-09

- Plugin modules reorganized into the `Core/Commands/UI` structure that
  `CLAUDE.md` and `Claude/ARCHITECTURE.md` now document as the standing
  convention.
