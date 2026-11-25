# Section To Sheet V001 + large Arrange/Rearrange buttons — Design

Date: 2026-11-25 · Status: draft for approval

## Intent
1. **Arrange Views** and **Rearrange** are hard to find as small stacked buttons. Make them large ribbon buttons.
2. A **direct, easy** way to put every section visible in the active plan onto sheets — including sections
   already placed elsewhere — without Section Placer's 5-stage wizard.

## Goals
- One window: section list + compact setup + **Place**. No stages.
- Collect all section views visible in the active plan view (placed and not placed).
- Already-placed sections are **moved** (removed from their old sheet, placed on the target).
- Start on an existing sheet or a new sheet; overflow continues on new sheets or on existing sheets the user picks.
- Neat layout, reusing the existing packing code.
- Setup is remembered between runs.

## Non-goals
- No change to Section Placer V003 or APUS behaviour.
- No duplicating views (option B in discussion was rejected).
- No obstacle-aware packing around schedules/text/legends on existing sheets.

## Anti-goals
- Partial results after an error (must roll back).
- Silently moving a view off a sheet the user can't edit (checked out by someone else).

## Approaches considered
1. **New standalone tool, reuse Section Placer's packing (chosen).** Simple UI, Section Placer untouched; costs a ribbon button.
2. Quick mode inside Section Placer V003 — keeps the complex window and changes its behaviour.
3. New tool + run Arrange Views per sheet — still needs packing for sheet assignment; two layout passes.

## Part 1 — Ribbon (SheetToolsRibbon.cs)
- `Pulldown_SheetAutoRearrange` (V028 first, V027 below) and `Btn_SheetViewArrange.V001` move out of
  `AddStackedButtons` and are added with `panel.AddItem` as **large** buttons (32 px `LargeImage`).
- Internal names, command classes, labels and order of versions unchanged.
- Plugin MINOR bump + CHANGELOG ("Rearrange and Arrange Views are now large buttons").

## Part 2 — Section To Sheet V001
**Location / identity:** `Menu/Sheet/SectionToSheet_V001`, namespace `Revit26_Plugin.SectionToSheet.V001`,
command `SectionToSheetCommand`, button `Btn_SectionToSheet.V001`, label "Section To Sheet" (large button,
Sheet Create panel), `ToolCatalog.SectionToSheet` = `1.0.0`. Entry points via `ToolGuard`. Window on the
shared `ToolWindowShell`. Icon: temporarily reuse the Section Placer icon (TODO dedicated icon).

**Launch:** active view must be a plan (Floor / Ceiling / Area / Engineering), else a clear message.

**Collection:** same rule as Section Placer V003 `ExecuteLoadViews`: `OST_Viewers` in the plan view →
`ViewSection`, `ViewType.Section`, not template, printable, not a dependent segment. Each row shows
Name, Scale, Size on sheet, Status ("Not placed" / "On A-301 → will move"). All ticked by default;
"Include already placed" checkbox (default on).

**Setup (one panel):**
| Setting | Values | Default |
|---|---|---|
| Titleblock | titleblock types (for new sheets) | last used / first |
| Start sheet | "New sheet" or any existing sheet | New sheet |
| Overflow | New sheets · Existing sheets (ordered pick list) | New sheets |
| If sheet has views | Stack below · Skip to next sheet · Rearrange all | Stack below |
| New sheet number | prefix + start number, auto-skips used numbers | `SEC-` + `001` |
| New sheet name | text | `Sections - <plan name>` |
| Margins T/B/L/R, Gap H/V (mm) | numbers | Section Placer defaults |

**Order and packing:** reading order across the plan (Section Placer's `ReadingOrderPackingService`
approach), packed per sheet with its existing packing service; views that don't fit roll to the next
sheet. A view larger than an empty sheet is skipped and logged.

**"If sheet has views" modes:**
- *Stack below:* place below the lowest existing viewport (Section Placer's
  `ComputeExistingContentBottomOffsetMm`); what doesn't fit overflows.
- *Skip to next sheet:* a sheet with any viewport is not used; log it and move on.
- *Rearrange all:* the sheet's existing viewports join the packing set with the new sections and are
  repositioned together. Schedules / legends / text are not moved (logged as a limitation).

**Overflow with existing sheets:** use the picked sheets in order; when they run out, continue on new
sheets and log a warning.

**Move of placed views:** inside the same transaction, record the old viewport's type, delete it, create
the new `Viewport` on the target, re-apply the type. Revit assigns the detail number on the target sheet.

**Robustness:** one `Transaction` for the whole run; any unexpected error rolls back everything.
Before acting, check `WorksharingUtils.GetCheckoutStatus` for the view, its old sheet and each target
sheet — views/sheets owned by others are skipped and logged. Results (placed / moved / skipped / failed,
sheets used) in the tool's log; unexpected errors via `ToolGuard.Report`.

**Settings:** JSON via `Shared/Services/SettingsService`, with a `version` field.

## Calls I made (check these)
- Packing code is **referenced** from `AutomatedSectionPlacer.V003` (its `ViewInfo` + packing services),
  not copied. If V003 is ever retired, those services move to `Shared` first.
- Section To Sheet is also a large button.
- Overflow to existing sheets falls back to new sheets when the list runs out.
- Detail numbers on the target are left to Revit.
- New-sheet number default `SEC-001`.

## Left to the builder
- Exact window layout within the shared shell; which packing service is the default.
