# Section To Sheet V001 + large Arrange/Rearrange buttons — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Large ribbon buttons for Rearrange / Arrange Views, and a new one-window tool that places every section visible in the active plan onto sheets.

**Architecture:** Modal WPF window on `ToolWindowShell` (Place runs in the command's API context, so no ExternalEvent). A `SectionCollector` reads the plan; a `SectionPlacementEngine` drives sheets one by one, using Section Placer V003's public `ReadingOrderPackingService.RepackSingleSheet` for the layout, inside one `Transaction`.

**Tech Stack:** C# / .NET 8, WPF, CommunityToolkit.Mvvm, Revit 2026 API.

**Spec:** `docs/superpowers/specs/2026-11-25-section-to-sheet-design.md`

## Global Constraints
- Build: `dotnet build Revit26_Plugin.csproj -p:EnableWindowsTargeting=true` → 0 errors (no test project exists; Revit runtime needed for behaviour).
- Never rename `Btn_SheetAutoRearrange.*`, `Pulldown_SheetAutoRearrange`, `Btn_SheetViewArrange.V001` or command classes.
- Entry points through `ToolGuard`. One `Transaction`, roll back on any error.
- `ToolCatalog.SectionToSheet` = `1.0.0`, build tag `V001`, namespace `Revit26_Plugin.SectionToSheet.V001`.
- Plugin version 3.9.1 → 3.10.0; CHANGELOG entry.

## Review Focus
1. A section already on the start sheet: must be removed first and repacked, not counted as "existing content".
2. A view larger than an empty new sheet: skipped and logged, never an endless loop of empty sheets.
3. Existing target sheet with no titleblock, or checked out by another user: skipped and logged.
4. "Rearrange all" with a pinned viewport, or existing views that no longer fit: falls back to "stack below".
5. Workshared model where the view's old sheet is owned by someone else: view skipped, nothing deleted.

---

### Task 1: Ribbon — large Rearrange / Arrange Views (+ Section To Sheet button)
**Files:** Modify `Menu/00_Push_Button_Menu_items/Ribbon/SheetToolsRibbon.cs`
- [ ] Keep Scope Box, Sheet Placer, Section Placer in `AddStackedButtons`.
- [ ] Add `rearrangePulldownData` (with `LargeImage`), `viewArrangeV001`, `sectionToSheetV001` via `createPanel.AddItem`; wire the pulldown with `WirePulldownButton(new[]{item}, "Pulldown_SheetAutoRearrange", rearrangeV028, rearrangeV027)`.
- [ ] Build → 0 errors. Commit.

### Task 2: Section To Sheet tool
**Files (create under `Menu/Sheet/SectionToSheet_V001/`):**
- `Commands/SectionToSheetCommand.cs` — plan-view check, opens window modally.
- `Core/SectionToSheetSettings.cs` — settings POCO (`Version = 1`) + enums `OverflowMode { NewSheets, ExistingSheets }`, `ExistingContentMode { StackBelow, SkipSheet, RearrangeAll }`.
- `Core/SectionCollector.cs` — sections (V003 rule), titleblocks, sheets.
- `Core/SectionPlacementEngine.cs` — `RunResult Run(Document, IReadOnlyList<ViewInfo>, PlacementRequest, Action<LogLevel,string>)`.
- `UI/SectionToSheetViewModel.cs`, `UI/SectionToSheetWindow.xaml(.cs)`.
**Modify:** `Shared/Services/ToolCatalog.cs` (entry), `Revit26_Plugin.csproj` (3.10.0), `CHANGELOG.md`.
- [ ] Implement; build → 0 errors. Commit and push.
