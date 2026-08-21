# Ribbon / Icon-Loading Code — Notes From The Cleanup Pass

These are observations about `Utilities/ImageUtils.cs`, the `Menu/00_Push_Button_Menu_items/Ribbon/*.cs` files, and `Revit26_Plugin.csproj`, found while auditing PushButtons against existing tool folders. They're separate from the PushButton add/remove cleanup itself — recorded here for a follow-up pass if the new per-tool icons (see `Icon_Prompts.md`) get generated.

## 1. Individual PushButtons never get their own icon today

Every `Ribbon.cs` file only sets `.LargeImage` on the parent `PulldownButton` (e.g. `SlopeMenu.LargeImage = ImageUtils.Load(...)`). None of the ~64 individual `PushButtonData` calls set `.Image` or `.LargeImage` on the button itself, so every tool inside a pulldown currently renders Revit's default gray placeholder icon — only the pulldown header has a custom icon.

The one place that *did* assign per-button icons was `QuickAcces.cs` (`auto_slope_button.LargeImage = ImageUtils.Load(...)`), but all three of its buttons pointed at deleted tool versions and have been removed as part of this cleanup, leaving that panel empty (see §4).

**If new per-tool icons are generated from `Icon_Prompts.md`**, each `PushButtonData` needs a matching line added right after it, e.g.:

```csharp
PushButton btn_DtlLine08 = DimMenu.AddPushButton(new PushButtonData("Btn_DtlLine_08", "...", assemblyPath, "...")) as PushButton;
btn_DtlLine08.Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DtlLineDim_V008_16.png");
btn_DtlLine08.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DtlLineDim_V008_32.png");
```

`AddPushButton` currently discards its return value everywhere — it needs to be captured (cast to `PushButton`) to set the icon.

## 2. New icon files must be added to the .csproj by hand — no wildcard

`Resources/Icons/*.png` is **not** globbed in. The csproj lists each embedded icon explicitly:

```xml
<EmbeddedResource Include="Resources\Icons\Addlines_32.png" />
<EmbeddedResource Include="Resources\Icons\Addtag32.png" />
...
```

Only 22 of the ~34 PNGs physically sitting in `Resources/Icons/` are actually in this list — the rest exist on disk but are invisible to `ImageUtils.Load`, which throws `FileNotFoundException` if the resource path isn't embedded. Any new icon (from the prompts file) needs its own `<EmbeddedResource Include="Resources\Icons\<file>.png" />` line or it will crash `OnStartup` the first time that button is built.

## 3. Several per-tool icons already exist but were never wired up

These files are already sitting in `Resources/Icons/` and match a live tool by name, but are **not** in the csproj's embedded-resource list and are not referenced by any `Ribbon.cs`:

- `AutoSlopeByPoint_32.png` — matches `Btn_AutoSlopeByPoint_028`
- `AutoSlopeByDrain_32.png` — matches `Btn_AutoSlopeByDrain_V006` / `V007`
- `RoofRidgeLines_32.png` — matches the `RoofRidgeLines_V*` family
- `SheetAutoRearrange_32.png` — matches `Btn_SheetAutoRearrange.V022` / `V023`
- `SmartViewToSheetPlacer_32.png` — matches `Btn_SmartViewToSheetPlacer.V221`
- `SectionAutoRenamer_32.png` — matches `Btn_SectionAutoRenamer_V024`
- `ViewAutoRenamer_32.png` — matches `Btn_ViewAutoRenamer_V003`
- `SectionViewAutoTagger` has no exact file, but `SpatialCommentTagger_32.png` is unused and unclear what it was for
- `RoofDrainCalloutPlacing_32.png` / `RoofEdgeViewRefBatch_32.png` — no longer have a live tool folder behind them (removed in this cleanup), so these two are now orphaned regardless

Worth checking these against the corresponding prompt in `Icon_Prompts.md` (marked "♻ reuse/refresh") before generating brand-new art — several may just need re-exporting at the current style rather than recreating from scratch.

## 4. `QuickAcces.cs` is a ribbon-definition file that doesn't match `*Ribbon*.cs`

`Menu/00_Push_Button_Menu_items/Ribbon/QuickAcces.cs` defines `QuickAccessRibbon.Build(...)`, which **is** called from `App.cs:49`, but the filename doesn't contain "Ribbon" so it's easy to miss in a search-by-filename audit. All three of its PushButtons referenced now-deleted tool versions (`AutoSlopeByPoint.V025`, `AutoSlopeByPoint.WithRidge`, `RoofRidgeLines.V53`) and have been removed, leaving `QuickAccessRibbon.Build` creating an empty panel with nothing in it.

This is a product decision, not something fixed automatically: either delete the empty panel/its `App.cs` call, or repoint it at 2–3 of the current flagship tools (e.g. latest `AutoSlopeByPoint`, latest `RoofRidgeLines`) for one-click access outside the pulldowns.

## 5. Tool folders with no PushButton possible (source not extracted)

A handful of tool folders exist on disk but contain only `.zip` archives with no extracted `.cs` source, so there's no compiled class a `PushButtonData` could reference. These were left out of the ribbon rewrite entirely (not "removed," since they were never wired in the first place):

- `RoofTools/Lines/Mechanicaltolines/*` (LinesFromMechanical V0007/V009/V010)
- `RoofTools/RoofSlope/ByPoints_0/ASPWithRidge.zip`, `AutoSlopeByPointRPF_V002.zip`, `AutoSlopeByPointTwoSlopes_V002.zip`/`V003.zip`, `AutoSlopeByPoint_0026.zip`, `AutoSlopeByPoint_RPF_001.zip`, `AutoSlopeByPoint_V0027.zip`
- `ViewTools/Place_01/Roof Drain Callout Placing/*` (all five variants)

If any of these are still wanted, unzip the source into the project first — then a PushButton can be added for it.

## 6. Minor pre-existing bugs noticed in passing (left alone — out of scope)

- `SetupRibbon.cs` had a leading space inside a type-name string literal (`" Revit26_Plugin.WorksetManager_02..."`) on the button that got removed — harmless now since the whole line is gone, but worth remembering if a similar copy-paste pattern recurs.
- `SheetToolsRibbon.cs` had two `AddPushButton` calls concatenated on a single source line (no newline between them) — also removed with the invalid entries, but indicates the file was probably hand-edited without reformatting.
