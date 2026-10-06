# Changelog

All notable changes to SloperPro. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
versions follow [Semantic Versioning](https://semver.org/) (MAJOR.MINOR.PATCH).

- **Plugin version** lives in `Revit26_Plugin.csproj` (`<Version>`).
- **Tool versions** live in `Shared/Services/ToolCatalog.cs`. Each tool has its own semantic version;
  the ribbon tooltip, the tool's window title and its error dialogs all read it from there.

Each release lists every tool whose version changed, under the plugin version.

## [2.1.0] — 2026-10-06

### Added
- **Delete Workset v1.0.0 (V001)** — new Manage tool. Select one or more user worksets from a
  data grid (non-deletable built-ins and the last remaining workset are shown greyed out), choose a
  migration target workset, and delete them in a single transaction group. Migratable elements are
  reassigned; view-specific and read-only-partition elements can optionally be hard-deleted. The
  real-time log panel shows per-step progress and a completion summary. Ribbon button added to the
  Manage panel.

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
