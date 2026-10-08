# Changelog

All notable changes to SloperPro. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
versions follow [Semantic Versioning](https://semver.org/) (MAJOR.MINOR.PATCH).

- **Plugin version** lives in `Revit26_Plugin.csproj` (`<Version>`).
- **Tool versions** live in `Shared/Services/ToolCatalog.cs`. Each tool has its own semantic version;
  the ribbon tooltip, the tool's window title and its error dialogs all read it from there.

Each release lists every tool whose version changed, under the plugin version.

## [2.3.2] — 2026-10-08

### Changed
- **Delete Line Styles v1.0.1 (V001)** — has its own ribbon icon (line styles with a red delete
  badge) instead of the Detail Lines icon. No behaviour change.

## [2.3.1] — 2026-10-08

### Fixed
- **Delete Workset v1.0.3 (V001)** — the run failed with "The file-based central model could not
  be reached" whenever central was offline or its path was not reachable from this PC (unmapped
  drive, VPN down, detached or copied model). The tool now deletes the selected worksets you
  already own and skips the rest, with a log line for each, instead of aborting the whole run.

**User note:** when central is unreachable, only worksets already checked out to you (or all
worksets in a detached model) can be deleted. Reconnect and Synchronize with Central to delete
the others.

## [2.3.0] — 2026-10-07

### Added
- **Delete Line Styles v1.0.0 (V001)** — new tool on the Manage panel. Lists every custom line
  style with how many lines use it (model, detail, symbolic and sketch lines, including lines in
  groups and filled-region boundaries). Two modes:
  - **Unused line styles only** (default) — deletes styles no line uses.
  - **All custom line styles** — also deletes used styles; their lines are changed to a
    replacement style you choose (default `<Thin Lines>`) before the style is deleted.

  Revit's built-in line styles are never touched. Styles owned by another user, or whose lines
  are owned by other users, are shown as "Blocked" and skipped. A style is skipped (and left
  unchanged) if any of its lines cannot take the replacement style. Everything runs in one
  transaction, so Ctrl+Z undoes the whole run.

**User note:** edges overridden with the Linework tool cannot be read through the Revit API, so a
style used only there counts as unused; those edges revert to their default style if it is deleted.

## [2.2.2] — 2026-10-07

### Fixed
- **Delete Workset v1.0.2 (V001)** — no workset could ever be selected. The tool treated every
  workset you had not already checked out as "Built-in", and then blocked the one you did own as
  "the only editable workset". Now every user workset is deletable unless another user has it
  checked out ("In use"), some of its elements are owned by other users ("Blocked"), or it is the
  only user workset. The badge tooltip gives the reason.
- Selected worksets are checked out from central automatically when you press Delete Workset;
  any that cannot be checked out are skipped and logged. Revit's own `CanDeleteWorkset` check runs
  before each deletion.
- "Migrate elements to" now lists every workset not being deleted (it only offered "deletable"
  ones). A target is required when a selected workset has elements or is closed; an empty open
  workset can be deleted without one (it used to fail).
- Answering **No** to a per-workset confirmation now skips just that workset instead of rolling
  back the whole run.
- Toolbar buttons (Select All / Clear / Refresh) and "Closed" status pills were unreadable
  (white text on a light background).

**User note:** running the tool now checks the selected worksets out to you in central.
Synchronize with Central afterwards to publish the deletion and release them.

## [2.2.1] — 2026-10-07

### Fixed
- **Delete Workset v1.0.1 (V001)** — the tool crashed on open with "'Border' TargetType does not
  match type of element 'TextBlock'". The metric-strip labels used a tile (Border) style and the
  options separator used a Border style on a `Separator`; both now use the correct styles. No
  behaviour change.

## [2.2.0] — 2026-10-07

### Added
- **Sheet View Arrange v1.0.0 (V001)** — new tool in the Sheet Place panel ("Arrange Views").
  Re-orders the views already on the active sheet by their existing Detail Number (natural
  order: 1, 2, 10 … A1, A2, B1) and lays them out like a reading table: left → right, wrapping
  into rows top → bottom. Each row is spread across the full usable width, rows are spread to
  the full usable height, and views in a row share a bottom line so their titles align. The last
  row can be packed left (default), justified or centred. Views are only moved — never resized,
  rescaled or removed. Usable area = title block inset by margins; live to-scale preview; nothing
  moves if the views don't fit, and Apply is refused if the sheet changed after the preview.
  Pinned views are moved and re-pinned (option); views owned by another user or changed in
  central (Reload Latest needed) are skipped and reported. One Undo step. Settings saved in
  `%AppData%\Revit26_Plugin\SheetViewArrange\settings.json`.

## [2.1.0] — 2026-10-06

### Added
- **Delete Workset v1.0.0 (V001)** — new Manage tool. Select one or more user worksets from a
  data grid (non-deletable built-ins and the last remaining workset are shown greyed out), choose a
  migration target workset, and delete them in a single transaction group. Migratable elements are
  reassigned; view-specific and read-only-partition elements can optionally be hard-deleted. The
  real-time log panel shows per-step progress and a completion summary. Ribbon button added to the
  Manage panel.
- **Export DWG to Folder v1.0.0** — new tool in the Setup › Project Tools panel.
  Scans the active document for all embedded CAD links (`IsLinked = true`) and CAD
  imports, classifies each by source type (link / import) and view category
  (plan / section / drafting / no_views), and exports them to a user-selected folder.
  Linked DWGs are copied directly from their source path; imported DWGs are exported
  from their host view via `Document.Export`.  Optional sidecar `.txt` file per DWG
  records View Name, Associated Sheet, Element ID and Symbol Name.  Options: toggle
  linked / imported sources, per-view-type subfolder checkboxes, sidecar toggle,
  overwrite toggle.  Settings are persisted in
  `%AppData%\Revit26_Plugin\ExportDwgToFolder\settings.json`.

## [2.0.0] — 2026-10-02

### Removed
- Superseded tool versions and their ribbon buttons (only the newest of each tool is kept):
  Detail Lines From Links VA003/VA006, Schedule Export/Import V001–V003, Plan From Scope Box V003,
  Smart View To Sheet Placer V221, Sheet Auto Rearrange V024/V025, DWG To Detail Lines V002,
  Roof Edge Element Sections V001, Roof View Focus V001, Roof Point Elevation Sync V002,
  and the unused WSFL V011 workset tool.
- All `.zip` backup copies from the repository (still in git history).

### Changed
- Ribbon: a button with a single tool now runs it on click, and is no longer a one-item dropdown.
  Dropdowns remain only where they group different tools (By Point, By Drain, Ridge Lines, From Rooms).
- Ribbon labels show the tool name only; the version moved to the tooltip
  ("Name — vX.Y.Z (build Vnnn)"). Internal button names and command classes are unchanged.
- Tools moved from build tags to semantic versions. Starting version = build number
  (e.g. build V004 → 4.0.0); see the table below.

### Added
- Suite-wide error safety net (`ToolGuard`): every command and external-event handler is guarded,
  and unhandled errors from tool windows are caught, so a tool error no longer reaches or crashes Revit.
  Errors are shown in a dialog and written to `%AppData%\Revit26_Plugin\Logs\errors-YYYY-MM-DD.log`.
- Each tool window's title ends with its version, e.g. "Sheet Auto Rearrange — v26.0.0".

### User notes
- Old tool versions are gone from the ribbon. If you relied on one, tell the maintainer before upgrading.
- A failing tool now shows an error dialog naming the tool and the log file — send that log when
  reporting a problem.
- Revit keyboard shortcuts must be reassigned for removed versions and for the 11 tools that moved out
  of a one-item dropdown (Revit keys shortcuts by the button's ribbon path). Other buttons keep theirs.

### Tool versions at 2.0.0

| Tool | Version | Build tag |
|---|---|---|
| Annotation Overlap Detection | 2.0.0 | V002 |
| Auto Dim Detail Line | 8.0.0 | V008 |
| Auto Place Sections | 322.0.0 | V322 |
| Auto Slope By Drain | 7.0.0 | V007 |
| Auto Slope By Drain (Multi-Roof) | 10.0.0 | V010 |
| Auto Slope By Point | 28.0.0 | V028 |
| Auto Slope By Point (Multi Copies) | 28.0.0 | MultiCopies28 |
| Auto Slope By Point (Ridge) | 1.0.0 | V001 |
| Automated Section Placer | 1.0.0 | V001 |
| Batch Link DWG Family | 0.1.0 | Working build |
| Bubble Auto Renumber | 6.0.0 | V006 |
| Callout To Section View Placement | 19.0.0 | V019 |
| Combined Roof Tools | 1.0.0 | V001 |
| Creaser Adv | 10.0.0 | V010 |
| Create Sections From Detail Lines | 11.0.0 | V011 |
| Detail Line Closed Loop | 1.0.0 | V001 |
| Detail Lines From Links | 7.0.0 | VA007 |
| Divide Inner Loops | 9.0.0 | V009 |
| DWG To Detail Lines | 11.0.0 | V011 |
| DWG To Lines | 5.0.0 | V005 |
| Floors And Roof From Linked Rooms | 11.0.0 | V011 |
| Floors And Roof From Linked Rooms (Via Plan View) | 4.0.0 | V004 |
| Inner Loops And Perpendicular | 5.0.0 | V005 |
| Multiple Points (1/2–1/4) | 1.0.0 | V001 |
| Outer Curve Divider | 4.0.0 | V004 |
| ParaManager | 3.0.0 | V003 |
| Plan From Scope Box | 4.0.0 | V004 |
| Reference Section Head Placer | 13.0.0 | V013 |
| Ridge By Openings | 68.0.0 | V068 |
| Ridge By Points | 57.0.0 | V057 |
| Roof Detail Line Intersect | 12.0.0 | V012 |
| Roof Edge Around Sections | 5.0.0 | V005 |
| Roof Edge Element Sections | 2.0.0 | V002 |
| Roof Edge Vertex Reducer | 7.0.0 | V007 |
| Roof From Detail Lines | 7.0.0 | V007 |
| Roof Point Comparison | 1.0.0 | V001 |
| Roof Point Elevation Sync | 3.0.0 | V003 |
| Roof Tag | 16.0.0 | V016 |
| Roof Type Manager | 1.0.0 | V001 |
| Roof View Focus | 2.0.0 | V002 |
| Schedule Export / Import | 4.0.0 | V004 |
| Section Auto Renamer | 24.0.0 | V024 |
| Section View Auto Tagger | 4.0.0 | V004 |
| Sheet Auto Rearrange | 26.0.0 | V026 |
| Smart View To Sheet Placer | 222.0.0 | V222 |
| View Auto Renamer | 4.0.0 | V004 |
| Workset Manager | 12.0.0 | V012 |
| Workset Renamer | 3.0.0 | V003 |
| Workset Renamer (From Excel) | 3.0.0 | FX03 |
| Worksets & Elements Browser | 2.0.0 | WSEB002 |

## [1.0.0] — 2026

### Added
- Initial public release on the Autodesk App Store: 30+ tools across Roof, Floor, View, Sheet,
  Setup and Manage panels.
