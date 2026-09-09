# Revit Plugin Code Audit Checklist

Use this file as a working audit guide with Claude Code. Go section by section, run the searches, and check off items. For each finding, note file + line, then fix or flag.

> **Audited 2026-09-03** across the whole repo (all `Menu/` tool folders) via 6 parallel research agents, findings fixed section by section. Status and notes recorded inline below.

---

## 1. Memory Management — ✅ audited, 1 fixed

- [x] Every `Transaction` / `TransactionGroup` is inside a `using` block or explicitly `.Dispose()`d
  - **1 violation found & fixed:** `Menu/RoofTools/Points/VertexReducer_V007/Infrastructure/ExternalEvents/RoofEdgeVertexReducerEventHandler.cs:147-151` — `TransactionGroup`/`Transaction` were never wrapped in `using`. Fixed with nested `using` blocks.
- [x] Every `FilteredElementCollector` goes out of scope right after `.ToElements()`/`.ToList()` — not stored as a field. **0 violations.**
- [x] No `Document`, `UIDocument`, `Element`, or `ElementId` is cached in a `static` field or held across method calls beyond the current operation. **0 violations.**
- [x] Every Revit event subscription (`Idling`, `DocumentChanged`, `SelectionChanged`, `ViewActivated`, etc.) has a matching unsubscribe in `Window.Closed` or command cleanup. **0 violations** — only genuine subscription in the repo is `SheetAutoRearrangeViewModel.cs:151`, correctly unhooked at line 157.
- [x] `ExternalEvent` is created once per tool (field, initialized in constructor/window load) — never re-created inside a button click handler. **0 violations** (~30 sites checked).
- [x] Large collections (`List<XYZ>`, `List<Curve>`, `Solid` caches, mesh data) are cleared or nulled after use if they outlive the method that built them. **0 violations.**
- [x] WPF: manually-subscribed `CollectionChanged`, `PropertyChanged`, or custom event handlers are unhooked in `Closed`/`Unloaded`. **0 violations** — normal MVVM lifetime pattern throughout.
- [x] Any `BitmapImage`/icon/image resource loaded manually is released (set to null) on window close. **0 violations** — only usage is ribbon icons, intentionally long-lived for the Revit session.

---

## 2. Threading — ✅ audited, 2 found & intentionally skipped

- [x] No Revit API call (`Document.*`, `Element.*`, transaction work) happens inside `Task.Run`, `async` continuations, or background threads.
  - **2 violations found, skipped by decision:** `RoofRidgeLines_V068/RoofRidgeViewModel.cs:448` and `RoofRidgeLines_V057_By_POints/.../RoofRidgeViewModel.cs:351` — Voronoi/clipping/validation math uses `Autodesk.Revit.DB.XYZ` (a value struct, not a live Document/Element handle) inside `Task.Run`. No live Document is touched off-thread. Fixing properly means converting several services to a plain POCO point type — real refactor risk for low actual benefit. **Decision: leave as-is.**
- [x] All Revit-side work is routed through `ExternalEvent.Raise()` — never called directly from a background thread. **0 violations.**
- [x] Any WPF UI update triggered from a background thread uses `Dispatcher.Invoke`/`BeginInvoke`. **0 violations** — the two `Task.Run` sites above correctly dispatch UI updates.

---

## 3. Error Handling — ✅ audited, 17 fixed

- [x] Every `IExternalCommand.Execute()` has a top-level try/catch.
  - **14 violations found & fixed** — wrapped each in try/catch (`OperationCanceledException` → `Result.Cancelled`, other exceptions → `TaskDialog.Show` + `Result.Failed`), original body moved verbatim into a new `ExecuteInternal`. Files: `FloorsAndRoofFromLinkedRooms_V011/Command.cs`, `AnnotationOverlapDetection_V002/Command.cs`, `VertexReducer_V007/Commands/RoofEdgeVertexReducerCommand.cs`, `RoofFromDetailLines_V007/Command.cs`, `RoofTag_V016/RoofTagCommand_FaceRef.cs`, `SectionAutoRenamer_V024/.../SectionManagerCommand.cs`, `SectionManager_V008/.../OpenSectionManagerCommand.cs`, `ViewAutoRenamer_V003/.../ViewAutoRenamerCommand.cs`, `SectionViewAutoTagger_V004/.../SectionViewAutoTaggerCommand.cs`, `WorksetManager_V012(New)/Commands/WorksetManagerCommand.cs`, `WSFL_V011(WSFL_10)/CreateWorksetsFromLinkedFilesCommand.cs`, `DwgToDetailLines_V011(New)/.../DwgToDetailLinesCommand.cs`, `DwgToLines_V005(New)/.../DwgToLinesCommand.cs`, `BatchDwgFamilyLinker/.../BatchLinkDwgCommand.cs`.
- [x] Every transaction-wrapped Revit API operation has try/catch with explicit `.RollBack()` on failure.
  - **1 violation found & fixed:** `SectionManager_V008/.../Services/SectionRenameService.cs:13-30` — rename loop had no per-item try/catch (one name collision would abort the whole batch). Added try/catch around `view.Name = ...` to skip-and-continue. Note: this class has no callers anywhere in the repo (dead code) — fixed anyway since it was cheap.
- [x] Recoverable per-item failures are logged as Warning and the loop continues — not thrown.
  - **2 silent `catch {}` swallows found & fixed:** `RoofTag_V016/RoofTaggingService_FaceRef.cs:124` and `CreaserAdv_V009/RoofSharedTopFaceCreaseService.cs:157` — both now log via `Debug.WriteLine` (repo's existing pattern for this) instead of silently swallowing.
- [x] Fatal failures abort, rollback, and show a dialog — not silently swallowed. **0 violations.**
- [x] Preconditions are validated before opening a transaction, not inside it. **0 violations** (sampled ~15 tools).

---

## 4. Structure — ✅ audited, 2 fixed, 3 found & intentionally skipped

- [x] `Command.cs` is thin — no business logic inline.
  - **2 violations found & fixed:**
    - `AutoSlopeByDrain_V007/Commands/AutoSlopeDrainCommand.cs` — `InitializeRoofGeometry`/`GetTopFace` moved to new `Core/Services/RoofGeometryService.cs`.
    - `ViewAutoRenamer_V003/.../Commands/ViewAutoRenamerCommand.cs` — `ClassifyView`/`BuildPlacedSheetsLookup` moved to new `Services/ViewClassificationService.cs`.
- [x] Algorithm/business logic lives in a Services class with zero Revit API dependency. **Clean** — all pathfinding/packing/clustering classes checked (Dijkstra, Delaunay, Voronoi, packing services) are Revit-API-free.
- [x] `IExternalEventHandler` implementation only does Revit-side glue. **Clean** across largest handlers checked.
- [x] ViewModel contains no direct Revit API document-mutating calls — only calls into Services.
  - **3 violations found, skipped by decision:** `CreaserAdv_V009/CreaserAdvViewModel.cs:196`, `AutoCreaser_V002_01/.../CreaserAdvViewModel.cs:118`, `Setup/Renamer/WorksetRenamer_V003/WorksetRenamerViewModel.cs:269` — all commit transactions directly from a modeless-window button-click handler on the main UI thread, which is safe in Revit's API (not a background thread). Introducing `ExternalEventHandler` for these would be a real architectural change per tool with regression risk, for something that isn't actually broken. **Decision: leave as-is.**
- [x] Window code-behind is minimal. **Clean** — zero Revit API usage found in any `.xaml.cs` repo-wide.

---

## 5. Configuration & Versioning — ✅ audited, 0 fixed (informational)

- [ ] Settings POCO includes a `SchemaVersion` field.
  - **Finding:** none of the 19 `*Settings.cs` files have one. **Decision: skip** — no migration logic exists yet to consume it; revisit if/when settings schemas start actually changing shape across versions.
- [x] Settings load path wraps `JsonSerializer.Deserialize` in try/catch and falls back to defaults. **Clean** — all 14 call sites do this correctly.
- [ ] Tool version shown in UI header matches `AssemblyInfo`/`AssemblyVersion`.
  - **Finding:** repo has no `AssemblyVersion`/`AssemblyInfo.cs` at all (single monolithic csproj/assembly, SDK default `1.0.0.0`). Per-tool "V0xx" strings in XAML titles track folder/tool revision, not any real build version — nothing to "match" since no AssemblyVersion is set. Not fixed; noted for awareness.
- [ ] `.addin` file `AddInId` GUID is unique per tool/plugin.
  - **Finding:** no `.addin` file is tracked in the repo (likely deployed/generated outside source control). Since there's only one assembly/entry point, GUID-duplication-across-tools doesn't apply here. Noted for awareness only.

---

## 6. Resource Cleanup Quick Table

| Resource | Expected pattern | Checked? |
|---|---|---|
| Transaction / TransactionGroup | `using` block | [x] 1 violation, fixed |
| FilteredElementCollector | scoped, not stored | [x] clean |
| Revit events (Idling, DocChanged, etc.) | subscribe on load, unsubscribe on Closed | [x] clean |
| ExternalEvent | created once, reused | [x] clean |
| Large collections (List\<XYZ\>, Solids) | cleared/nulled after use | [x] clean |
| WPF event handlers | unhooked in Closed/Unloaded | [x] clean |
| File handles (log export, settings) | `using` for StreamWriter/FileStream | [x] clean — only 1 manual handle in repo, correctly `using`-wrapped; everything else uses auto-disposing `File.*` convenience methods |

---

## 7. Testing — ✅ audited, 0 fixed (informational)

- [ ] Pure logic classes have no Revit.dll dependency and can be tested with plain xUnit/NUnit.
  - **Finding:** no test project exists anywhere in the repo (factual, not a defect for this kind of project — single `Revit26_Plugin.csproj`, no `.Tests.csproj`, no xUnit/NUnit/MSTest usage). Good first candidates if testing is ever introduced: `DelaunayTriangulation.cs`, `SheetBinPacker.cs`, `SheetLayout.cs`, `NamingResolver.cs` — genuinely Revit-API-free.
- [ ] Suspect Revit API behavior is isolated in a standalone diagnostic harness. **Finding:** none exists — as expected/uncommon for this kind of repo.

---

## How to run this with Claude Code

1. Point Claude Code at the tool's folder (e.g. `ToolName/`).
2. Ask it to go through each numbered section above in order.
3. For every unchecked item, have it report: file, line number, and a one-line fix suggestion.
4. Do not have it auto-apply fixes in bulk — review and approve per section, especially anything touching transactions or event subscriptions.
