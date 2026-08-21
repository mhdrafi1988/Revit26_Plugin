# Revit26_Plugin — Master Guide

Project-wide rules, conventions, and shared learnings for all tools in the `Revit26_Plugin` suite. Use this alongside per-tool status MD files.

---

## 1. Stack

- C# / WPF
- Revit 2026 API
- `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`) — requires `PackageReference` in `.csproj`, or generated properties silently don't exist
- `System.Text.Json` for settings persistence
- `Microsoft.Win32` for dialogs

## 2. Namespace & Versioning

- Namespace pattern: `Revit26_Plugin.{FeatureName}.{Version}` — Claude suggests, Rafi confirms
- **Behavior change** → new version folder + full namespace rename across all `.cs`/`.xaml` files
- **Bugfix** → patch in place, no version bump
- Historical changelog comments must preserve the version where each feature *actually originated* — never bulk-relabel past entries

## 3. Vertical-Slice Structure

```
Commands/
Core/Models/
Core/Services/
Core/Engine/
Infrastructure/ExternalEvents/
Infrastructure/Helpers/
UI/Views/
UI/ViewModels/
```

Shared infra only lives in `Revit26_Plugin.Shared.Models`:
- `Converters.cs` (canonical shared converter file)
- `LogEntry.cs` / `LogLevel.cs`
- `SharedStyles.xaml` (navy theme)

## 4. Workflow Gates (mandatory every session)

1. **File confirmation rule** — never write/edit/update any file without explicit confirmation. Ask a direct yes/no question, wait for literal "yes." No proceeding on implied go-ahead.
2. HTML mockup (screenshot-style) always before any XAML/UI code
3. Clarifying questions asked and answered before coding begins — never assume on ambiguous requirements
4. Zip files only on explicit per-session confirmation ("yes, zip it")
5. No `.md` files unless explicitly requested
6. Deliver only changed files individually unless a full zip is explicitly requested
7. Delete leftover/unused code when creating a zip

## 5. Communication Style

Rafi uses terse shorthand, typos, uppercase directives, single-word confirmations ("yes," "go ahead," "bump," "zip"). Respond to multi-part questions with short numbered answers. Interpret intent, don't nitpick grammar. Be concise. Ask if in doubt — never assume.

---

## 6. UI Conventions

- Navy theme (`#1E3A5F`), compact cards
- Header format: `"ToolName — Vxxx"`
- Fields: `MaxLength="10"` (numeric), `MaxLength="25"` (name/pattern/text fields)
- `FontSize="10"` or `"11"` for inputs; `FontSize="11.5"` for labels
- Modeless dialogs: Close-only (no OK/Cancel), Esc = Close
- Buttons right-aligned: Primary → Cancel/Close → Apply. Dialog-only buttons (e.g. Reset) far-left
- Resizable, never below default size; grid columns grow proportionally
- Grid/table titles: Title Case, bolded; subtle alternating row shading
- Card titles: noun phrase, no punctuation
- Run button: disable during operation, re-enable after (confirm per-tool if N/A)

### DataGrid Spec (when used)

1. Toolbar: filter box + Select All / Clear / Refresh
2. Checkbox column: centered, TwoWay binding to per-row VM property, `PreviewMouseLeftButtonDown` to block row-select cascade
3. Text left-align, numeric right-align (including header)
4. Sortable columns show direction arrow
5. Select All/Clear scoped to visible/filtered rows only
6. Virtualizing (Recycling mode) for 100+ rows
7. `SharedStyles.xaml` only — no new brushes/styles per tool

---

## 7. Architecture

- Converters: all live in `Shared/Converters.cs`; each Window instantiates only what it needs, locally in its own `Window.Resources` — **never** in the shared `ResourceDictionary`
- `IExternalEventHandler` + `ExternalEvent.Create()` (called inside `Execute()`) per tool — one handler/event pair by default; shared orchestrator only if one window drives multiple distinct Revit-side actions
- `Mode=OneWay` for read-only `Run.Text` bindings (Run.Text defaults to TwoWay, unlike TextBlock.Text)

## 8. Transactions (Autodesk-standard)

- One `Transaction` per logical operation
- `TransactionGroup` for multi-step Undo merge (`.Assimilate()`) or all-or-nothing rollback
- `SubTransaction` only for isolated retryable sub-steps, with explicit `Commit()`/`RollBack()`
- Never open/commit inside a loop — wrap the loop in one `Transaction`
- `SubTransaction` does not support `SetFailureHandlingOptions` — attach `IFailuresPreprocessor` to the outer `Transaction`

## 9. Code Robustness

All Revit API/transaction calls wrapped in try/catch with rollback on failure.

---

## 10. Window & Logging

- Window never auto-closes
- Real-time log via `ObservableCollection<LogEntry>`, with Copy All / Copy Selected buttons
- Silent skips → Warning only; critical/transaction errors → dialog + rollback
- Completion summary line, e.g. `"X placed | Y skipped | Z failed"`
- Auto-save log on completion + manual Export (`.txt`)
  - Filename: `{ToolName}_Logs_{yyyy-MM-dd}_{HH-mm}.txt`
  - Ask save folder once per session, reuse thereafter
- **Logging depth**: capture params, item counts/IDs at each collection stage, entry/exit of major steps — for traceability. Applies to new tools AND existing tools whenever touched.

## 11. Settings

- Per-tool JSON: `%AppData%\Revit26_Plugin\{ToolName}\settings.json`
- `System.Text.Json` POCO
- Load on open, save on close / after Run
- `ExtensibleStorage` only if data is project-specific

---

## 12. Shared Code Conventions

- `WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle` for window parenting (never `Application.Current.MainWindow`)
- `Microsoft.Win32.OpenFolderDialog` (not WinForms `FolderBrowserDialog`) for folder picking

---

## 13. Revit API Gotchas

- `EdgeArrayArray` / `EdgeArray` do not support `.Count` or `foreach` — use `.Size` and `.get_Item(i)` with explicit casting
- `SlabShapeEditor` handles go stale after `doc.Regenerate()` — always re-fetch after commit/regenerate
- `NewFootPrintRoof()` throws bare `ArgumentNullException` inside `IExternalEventHandler.Execute()` context (cause unconfirmed) — `FootPrintRoof.Create()` static method is the recommended alternative to test
- `ViewSection.CreateReferenceCallout()` places a reference marker pointing to an existing drafting view (no crop box config needed)
- `ViewSection.CreateReferenceSection()` creates an `OST_Viewers` annotation instance, not a new `ViewSection` — diff using `OST_Viewers` collector, not `typeof(ViewSection)`
- `ElementId.Value` (returns `long`) replaces `.IntegerValue` in Revit 2026
- Full-circle arcs must be split into two half-circle `DetailCurve` arcs (Revit rejects 2π sweep)
- Voronoi/circumcenter geometry must be computed in world space using an orthonormal `PlaneBasis` — computing in Revit's parametric UV face space produces silently distorted positions

## 14. WPF/XAML Gotchas

- `Run.Text` defaults to `TwoWay` binding (unlike `TextBlock.Text`) — always add `Mode=OneWay` for read-only log properties
- Converters must be instantiated in `Window.Resources` **after** `MergedDictionaries`, never inside `SharedStyles.xaml`
- Same-assembly `xmlns` references omit the `;assembly=` suffix
- `pack://application:,,,/` URIs fail in Revit add-ins (no `Application` object) — use assembly-relative form: `/Revit26_Plugin;component/Shared/SharedStyles.xaml`
- `StaticResource` on root `<Window>` attributes resolves before `Window.Resources` is parsed — use `DynamicResource` or move to a child element
- Cascading "does not exist in namespace" errors across multiple types = upstream C# compile failure, not a genuine XAML/namespace issue — check Build Output for `CS####` errors first
- Stale `bin`/`obj` cache causes false-positive converter "incompatible type" errors — always delete `bin`/`obj` and full-clean before diagnosing
- Named elements (`x:Name`) inside `ContentProperty`-typed content of a `UserControl` merge into the parent window's namescope, causing collisions

## 15. Cascading Build Failures

When every custom type fails simultaneously across multiple namespaces, the root cause is always a single upstream compile error — never treat it as multiple independent issues.

---

## 16. Output & Delivery

- Output path: `/mnt/user-data/outputs/`
- Deliver only changed files unless full zip explicitly requested
- Zip only on explicit confirmation; strip leftover/unused code before zipping
