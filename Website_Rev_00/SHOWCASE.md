# AutoSlope by Drain — Showcase Site

A single-page HTML showcase built for the **AutoSlope by Drain** Revit plugin (source: `ByDrains_0/AutoSlopeByDrain_V010`). Published as a Claude Artifact, not a file in this repo — this note documents what it contains and how to regenerate or extend it.

**Live link:** https://claude.ai/artifact/EXdKZt9TGJxekuddZ6KY8D
*(private — only the owner and people it's explicitly shared with can open it)*

## What it's for

A pitch/demo page for the plugin: what it does, how much time it saves, what the dialog looks like, and what data it produces — aimed at anyone evaluating or approving the tool (not a user manual).

## Source of truth

Every concrete detail on the page was pulled from the real V010 source, not invented:

| Page content | Source file |
|---|---|
| Dialog layout, fields, toggles | `ByDrains_0/AutoSlopeByDrain_V010/UI/Views/AutoSlopeByDrainWindow.xaml` |
| Circle Marker groups (Drain / Highest Offset / Allowed Offset) | Same XAML — `DrainMarkerGroup` / `HighestPointMarkerGroup` / `AllowedOffsetMarkerGroup` |
| Excel export columns & sheet names | `Infrastructure/Helpers/ExcelExportHelper.cs` |
| Written parameter names (`AutoSlope_*`) | `Infrastructure/Helpers/AppConstants.cs` + `Core/Parameters/AutoSlopeDrainParameterWriter.cs` |

Numbers not traceable to a real log (points processed, time-saved %, Power BI/costing/planning dummy figures) are explicitly illustrative and labeled as such in the page copy.

## Sections (top to bottom)

1. **Hero** — CSS/SVG digital-twin roof render (drain/highest-offset/clamped-offset markers, slope-direction arrows), version badge
2. **Metrics banner** — 9 stat cards (points processed, time saved, manual/plugin time, accuracy, parameters written, Excel sheets, roofs/batch, marker types)
3. **Live Calculator** — single roof, points-only (8–10s manual vs. 1s plugin per vertex), 200/400/500 tiers + slider
4. **Multi-Roof Calculator** — same math scaled by an editable roof count (slider + typed exact count) × points-per-roof tier (20/50/100/150/200/250)
5. **UI Mockup** — reconstructed `AutoSlopeByDrainWindow` dialog, including a live animated progress strip under Run
6. **Before / After** — same roof plan flat (0 mm everywhere) vs. after a run (elevation heat-map from drain to highest point)
7. **Excel Export** — mock workbook view of the real "Vertex Data" + "Run Summary" sheets
8. **Parameter Updates** — all 11 `AutoSlope_*` instance parameters with dummy values, plus a downstream-integration diagram (Power BI / Costing-QS / Planning, illustrative)
9. **Capability Matrix** — manual workflow vs. plugin, feature by feature
10. **Footer**

Both calculators and the mockup's progress strip share one "live" visual language: a pulsing dot + animated diagonal-stripe race bar (Manual vs. AutoSlope), added per Rafi's request to make the comparisons feel live rather than static.

## Updating the page

The page is authored as a single HTML file and republished via the Artifact tool to the same URL above (keeps the link stable). To change it:

1. Edit the working copy (ask Claude to pull it up, or re-describe the change).
2. Republish to the existing artifact URL — never create a new one unless a separate page is actually wanted.

If real screenshots or a generated hero image become available, they should replace the CSS/SVG hero art and the reconstructed dialog mockup — the current versions are explicitly stand-ins pending real assets.
