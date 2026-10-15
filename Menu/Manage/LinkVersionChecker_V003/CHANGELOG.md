# Changelog — Link Version Checker

## V003 — 2026-10-14

### Fixed
- **Build error CS1061** — `'CADLinkType' does not contain a definition for 'Unload'`. The Revit 2026 API has no unload for CAD links (`CADLinkType` only offers `Reload`, `ReloadFrom`, `LoadFrom`). In-Model "Unload now" now unloads RVT/IFC links and **skips CAD links** with a clear reason (use Manage Links › CAD Formats, or Pre-Open mode, which still works for CAD via `TransmissionData`).
- Unload confirmation in In-Model mode says up front how many CAD links will be skipped.

### Removed
- `LinkActions.RunAllowingTransaction` helper (only existed for the CAD unload call).

### Changed
- Namespace / folder / window title bumped to `LinkVersionChecker_V003`.

## V002 — 2026-10-13

### New
- **Folder Audit mode** — scans every host model in a folder (optional subfolders) into one grid and report; Host column and Host filter appear in this mode.
- **Batch actions** — tick column, Tick problems / Tick all shown / Clear ticks; Upgrade, Rebuild IFC cache and Skip-load run on all ticked rows. Each file is upgraded once even when several hosts link it; host edits are grouped into one backup + one write per host.
- **Measured load times** — `LinkLoadTimeLogger` records real host and link open times from Revit events; Measured column + Last full open card; included in the Excel report.
- **Progress bar + Cancel** for scans and batch actions, with the window staying responsive.
- **Rule-based checks** — `ILinkRule` + `RuleEngine`; each check is its own class; findings carry severity, message and estimated seconds; multiple findings per link.
- **New checks** — duplicate links, imported (not linked) CAD, loaded point clouds (with ReCap support-folder size), all worksets open in big workshared links, absolute paths, cloud models.
- **Excel export** — formatted .xlsx (Summary / Links / Findings) via ClosedXML; CSV fallback.
- **Remembered settings** — last host + recent hosts dropdown, last folder, options, filters, window size, export folder.
- **In-Model** "Unload now" action; CAD links can be unloaded too. *(Didn't compile — fixed in V003.)*
- `LinkCheckStartup.Register(app, panel)` — one call adds 3 ribbon buttons and starts the logger.

### Changed
- Scan is a pipeline: link tables → parallel file-system probes (8 threads) → cached version headers → nested links → rules. Shared links are read once across hosts.
- `LinkInfo` now holds raw facts; status, severity, estimate and recommendations come from the rules.
- Pill colour follows severity (Critical / Warning / Info / OK) instead of a fixed status list.
- Backups go through `BackupService` (once per file per session, timestamped).
- Recommendations panel lists every finding, not just one.

## V001 — 2026-10-13
- First version: Pre-Open and In-Model checks, single-row actions, CSV export.
