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


## 2026-10-04 — ToolWindowShell (CLAUDE.md v1.1)

- Shared: new `Shared/Controls/ToolWindowShell` (header / body / footer;
  the body is the window's only ScrollViewer; footer status strip bound to
  `IsRunning` / `Progress` / `SummaryText`) and `ShellBehaviors`
  (`MaxHeightRatio`, `ForwardMouseWheel`). SharedStyles 3.1 adds `Tile*`
  brushes and `MetricTile*` styles.
- SectionAutoRenamer V026 — first tool on the shell (reference
  implementation): log moved from footer into the body, grid and log
  height-capped, V024's tinted metric tiles restored via named brushes.
  ViewModel gains `IsRunning` / `Progress` / `SummaryText`; no behaviour
  change.
- Ribbon: Section Renamer pulldown is now V025, V026 (V024 removed from the
  ribbon; folder kept).
- DetailLineClosedLoop V003 on the shell: Selection/Options left (own
  ScrollViewer removed), Created Lines grid right, Processing Log moved
  from footer into the body. Shell binds to the existing `IsBusy` /
  `RunSummary`; ViewModel gains `Progress` only. Ribbon pulldown: V002,
  V003 (V001 removed from the ribbon; folder kept).
- DetailLineDimensions (DtlLineDim) V010 on the shell: Selection,
  Dimension Settings and Activity Log (moved from footer) in the body; the
  middle's own ScrollViewer removed. Binds to `IsBusy` / `RunSummary`;
  ViewModel gains `Progress` only. Ribbon pulldown: V009, V010 (V008
  removed from the ribbon; folder kept).
- Shell: an empty `Subtitle` now collapses instead of leaving a blank line.
- OuterCurveDivider V006 on the shell: type-rules grid, edges grid and
  Activity Log (moved from footer) in the body; both grids and the log use
  MaxHeightRatio (type grid's fixed MaxHeight=160 replaced). ViewModel
  gains `IsRunning` / `Progress` / `SummaryText` (not yet set, so the
  status strip stays empty). Ribbon pulldown: V005, V006 (V004 removed
  from the ribbon; folder kept).
- VertexReducer (RoofEdgeVertexReducer) V009 on the shell: Selection,
  Settings, Preview Results grid and Log (moved from footer) in the body;
  the old footer summary line is now the shell status strip
  (`SummaryText`). ViewModel gains `IsRunning` / `Progress`. Ribbon
  pulldown: V008, V009 (V007 removed from the ribbon; folder kept).
- MultiplePoints V003 on the shell: Points To Add settings, edges grid and
  Activity Log (moved from footer) in the body. ViewModel gains
  `IsRunning` / `Progress` / `SummaryText` (not yet set). Ribbon pulldown:
  V002, V003 (V001 removed from the ribbon; folder kept).
- RoofDetailLineIntersect V014 on the shell: Selection, Options and Run Log
  (moved from footer) in the body; the middle's own ScrollViewer removed.
  Binds to the existing `IsBusy`; ViewModel gains `Progress` /
  `SummaryText` (not yet set). Ribbon pulldown: V013, V014 (V012 removed
  from the ribbon; folder kept).
- CreaserAdv V012 on the shell: detail item, filters, minimum slope and Log
  (moved from footer) in the body; the middle's own ScrollViewer removed.
  Binds to the existing `IsRunning`; ViewModel gains `Progress` /
  `SummaryText` (not yet set). Ribbon pulldown: V011, V012 (V010 removed
  from the ribbon; folder kept).
- RoofTag V018 on the shell: warning banner, settings cards and Log (moved
  from footer) in the body; the middle's own ScrollViewer removed. Still a
  modal OK / Cancel dialog. ViewModel gains `IsRunning` / `Progress` /
  `SummaryText` (not yet set). Ribbon pulldown: V017, V018 (V016 removed
  from the ribbon; folder kept).
- CompareRoofs (RoofPointElevationSync) V005 on the shell: selection card,
  point-mapping grid and Log (moved from footer) in the body; the old
  footer summary is now the status strip. Binds to `IsBusy` /
  `SummaryText`; ViewModel gains `Progress`. Ribbon pulldown: V003, V004,
  V005 (V002 removed from the ribbon; folder kept).
- RoofPointComparison V003 on the shell: both metrics rows in the header;
  tolerances, marker settings and Processing Log (moved from footer) in
  the body; the middle's own ScrollViewer removed. Binds to `IsBusy` /
  `StatusMessage`; ViewModel gains `Progress`. Ribbon pulldown: V002, V003
  (V001 removed from the ribbon; folder kept).
- Shell: new `IsIndeterminate` option — the status-strip bar animates for
  tools that only have a busy flag, no percentage.
- AnnotationOverlapDetection V004 on the shell: type list and results grid
  in the body (both capped; type list's fixed MaxHeight=150 replaced); the
  old footer busy bar is now the shell's indeterminate bar on `IsLoading`.
  No log in this tool. ViewModel gains `Progress` / `SummaryText`. Ribbon
  pulldown: V003, V004 (V002 removed from the ribbon; folder kept).

## 2026-10-04 — Dimensions

- Dimensions: DtlLineDim bumped to V009 as a layout-only rebuild per
  `Revit_Plugin_UI_Standard.md` (fixed title bar + version + metrics card,
  scrolling middle, fixed footer with an always-visible log + Copy All /
  Copy Selected, Export Log on the left, Generate Dimensions → Close).
  No behaviour changes.
- Ribbon: the Detail Lines dimension button is now a pulldown — V008 first,
  V009 second.

## 2026-10-04 — Floor Tools

- Floor Tools: both live tools bumped one version as a layout-only rebuild
  per `Revit_Plugin_UI_Standard.md` (fixed title bar + version + metrics
  card, scrolling middle, fixed footer with progress, summaries, an
  always-visible log + Copy All / Copy Selected, Create Floors / Create
  Roof → Close). No behaviour changes.
  New versions: FloorsAndRoofFromLinkedRooms V012,
  FloorsAndRoofFromLinkedRoomsViaPlanView V005 (now a fixed 900 × 760 window
  with a full-height room list; was 420 wide, SizeToContent).
- Ribbon: the From Rooms pulldown lists each approach's previous version
  first and its UI-standard version second.

## 2026-10-04 — Detail Lines

- Detail Lines: every live tool bumped one version as a layout-only rebuild
  per `Revit_Plugin_UI_Standard.md` (fixed title bar + version + metrics
  card, scrolling middle, fixed footer with an always-visible log + Copy All
  / Copy Selected, Primary → Close). No behaviour changes.
  New versions: LinkedDetailLineGenerator VA008 (log Copy buttons now wired;
  metadata stamp `GeneratorVersion` = VA008), DetailLineClosedLoop V002
  (window widened to 980 for a full-height Created Lines grid).
- Ribbon: every Detail Lines button is now a pulldown — current version
  first, the UI-standard version second.

## 2026-10-04 — Setup

- Setup: every live tool bumped one version as a layout-only rebuild per
  `Revit_Plugin_UI_Standard.md` (fixed title bar + version + metrics card,
  scrolling middle, fixed footer with an always-visible log + Copy All /
  Copy Selected, Primary → Close). No behaviour changes.
  New versions: BatchDwgFamilyLinker V002 (first versioned build; now on
  SharedStyles, namespace `BatchDwgFamilyLinker.V002`), DwgToLines V006,
  DwgToDetailLines V012 (window widened to 980 for a full-height layer grid).
- Ribbon: every Setup button is now a pulldown — current version first,
  the UI-standard version second.

## 2026-10-04 — Sheet Tools

- Sheet Tools: every live tool bumped one version as a layout-only rebuild per
  `Revit_Plugin_UI_Standard.md` (fixed title bar + version + metrics card,
  scrolling middle, fixed footer with an always-visible log + Copy All /
  Copy Selected, Primary → Close). No behaviour changes.
  New versions: PlanFromScopeBox V005, SmartViewToSheetPlacer V223,
  AutomatedSectionPlacer V002, SheetAutoRearrange V027.
- Smart Placer / Auto Section Placer: the per-stage Activity Logs (stages 4
  and 5), Export Logs and Cancel buttons moved into one fixed footer; stage
  navigation (Next / Back / Place / Open) stays in the accordion stages.
- Ribbon: every Sheet button is now a pulldown — current version first,
  the UI-standard version second (Auto Section Placer became a pulldown).

## 2026-10-04 — Manage

- Manage: every live tool bumped one version as a layout-only rebuild per
  `Revit_Plugin_UI_Standard.md` (fixed title bar + version + metrics card,
  scrolling middle, fixed footer with log + Copy All / Copy Selected where
  the tool has a log, Primary → Close). No behaviour changes.
  New versions: AnnotationOverlapDetection V003 (now on SharedStyles),
  ParaManager V004, WorksetsElementsBrowser WSEB003, WorksetManager V013,
  WorksetRenamer V004 / FX04, ScheduleExportImport V006.
- Ribbon: every Manage button is now a pulldown — current version first,
  the rebuild second.

## 2026-10-04 — View Tools

- View Tools: every live tool bumped one version as a layout-only rebuild
  per `Revit_Plugin_UI_Standard.md` — three-zone window (fixed title bar +
  version + metrics card, scrolling middle, fixed footer with an
  always-visible log + Copy All / Copy Selected and Primary → Close).
  No behaviour changes; every binding/command is the previous version's.
  New versions: CreateSections V012, RoofEdgeAroundSections V006,
  RoofEdgeElementSections V003, RoofViewFocus V003, APUS V323,
  CalloutCOP V020, RefSectionHeadPlacer V014, SectionViewAutoTagger V005,
  BubbleAutoRenumber V007, SectionAutoRenamer V025, ViewAutoRenamer V005.
- Ribbon: every View Tools button is now a pulldown — current version
  first, the rebuild second.
- Shared: `Shared/Services/LogClipboardService.cs` (same file as the Roof
  Tools PR) for Copy All / Copy Selected from window code-behind.

## 2026-10-04

- Roof Tools (incl. Roof Tag): every live tool bumped one version as a
  layout-only rebuild per `Revit_Plugin_UI_Standard.md` — three-zone
  window (fixed title bar + version + metrics card, scrolling middle,
  fixed footer with an always-visible log + Copy All / Copy Selected and
  Primary → Close buttons). No behaviour changes; every binding/command is
  the previous version's. New versions: AutoSlopeByPoint V029, Ridge V002,
  MultiCopies29, AutoSlopeByDrain V011, InnerLoopDivider V010,
  OuterCurveDivider V005, RoofDetailLineIntersect V013,
  InnerLoopsAndPerpendicular V006, VertexReducer V008, MultiplePoints V002,
  RoofRidgeLines V069 / V058, CreaserAdv V011, RoofTag V017,
  RoofFromDetailLines V008, CombinedRoofTools V002, RoofTypeCreator V002,
  RoofPointComparison V002, RoofPointElevationSync V004.
- Ribbon: every Roof Tools button is now a pulldown — current version
  first, the rebuild second. Existing multi-version pulldowns get each
  rebuild right after the version it was copied from.
- Shared: added `Shared/Services/LogClipboardService.cs` (Copy All / Copy
  Selected for any log ListBox, used from window code-behind).

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
