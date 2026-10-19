# Architecture

How a request actually flows through this add-in, end to end. Conventions
and folder layout are in the root `CLAUDE.md`; this file is the runtime
picture — what calls what, and where the sharp edges are.

## 1. Startup → ribbon

`App.cs : OnStartup` creates the `"SloperPro"` ribbon tab, then calls
one `Build(application, tabName, assemblyPath)` per category from
`Menu/00_Push_Button_Menu_items/Ribbon/*Ribbon.cs` (`RoofToolsRibbon`,
`FloorToolsRibbon`, `SheetToolsRibbon`, ... plus `QuickAcces.cs` →
`QuickAccessRibbon`, named without "Ribbon").

Each `*Ribbon.cs` builds `RibbonPanel`s and `PushButtonData` (often via
`RibbonLayoutHelper.AddStackedButtons` / `CreatePulldownButtonData`), wiring
each button to a **command class by fully-qualified type-name string**, e.g.:

```csharp
new PushButtonData("Btn_AutoSlopeByDrain_V010", "By Drain V010", assemblyPath,
    "Revit26_Plugin.MultiRoofSlopeByDrain.V010.Commands.AutoSlopeByDrain")
```

This string is not compiler-checked. A typo, or a namespace that drifts from
what's in the ribbon file, fails silently at build and only breaks when the
button is clicked in Revit. When renaming a namespace or moving a tool,
grep the ribbon files for the old fully-qualified name — folder moves alone
don't update it.

**Namespace does not mirror the folder path.** `AutoSlopeByDrain_V010` lives
under `Menu/RoofTools/RoofSlope/ByDrains_0/` on disk but its code is in
namespace `Revit26_Plugin.MultiRoofSlopeByDrain.V010` — a leftover/renamed
identity from when it was split out as a multi-roof variant. To find what
code a ribbon button actually runs, read the fully-qualified type string in
the `*Ribbon.cs` file — don't infer it from the folder name.

## 2. Per-tool request lifecycle

```
PushButton click
  → Command.Execute(ExternalCommandData)      [Commands/]
      synchronous Revit API work that must happen before the window
      shows: PickObject(s), enable shape editing, reset vertices,
      initial detection/analysis — safe here because no window is open yet
  → new Window(new ViewModel(...)).Show()      [UI/Views, UI/ViewModels]
      modeless; user edits settings, previews, adjusts options
  → ViewModel builds a Payload (Core/Models), sets it on a static field
    on the Handler, then calls EventManager.Init() (idempotent — guarded
    by `if (Handler != null) return`) followed by Event.Raise()
  → Handler.Execute(UIApplication)             [Infrastructure/ExternalEvents/]
      runs on Revit's API thread; opens a TransactionGroup, does the
      actual document mutation (often delegating to a Core/Engine or
      Core/Services class), commits/rolls back, then reports results
      back to the ViewModel (static event/callback + Dispatcher for
      cross-thread UI update)
```

Each tool's `EventManager` (`Infrastructure/ExternalEvents/*EventManager.cs`)
is a static holder for one `Handler` + one `ExternalEvent`, created once.

**Documented vs. actual `Init()` call site:** several `EventManager.cs`
header comments say `Init()` "must be called once from
`IExternalApplication.OnStartup` — NOT from the Command or the ViewModel
constructor." In practice, **every tool in the repo calls `Init()` from
the ViewModel**, not from `App.cs`. This works because `Init()` no-ops if
`Handler` is already set, but it means the stated convention in those
comments is aspirational, not real — follow the actual repo-wide pattern
(call from the ViewModel) rather than the comment when adding a new tool.

## 3. Layer dependency direction

```
Commands/        → Core/, Infrastructure/, UI/
UI/ViewModels     → Core/, Infrastructure/ExternalEvents/ (to raise events)
Infrastructure/   → Core/, Revit API
Core/Services,Engine,Models → nothing but plain .NET (no Revit API, no WPF)
```

`Core/` being Revit-free is the one layering rule the audit found completely
clean repo-wide — keep it that way; it's what would make these classes
unit-testable if a test project is ever added.

## 4. Shared cross-tool code

`Shared/` (namespace `Revit26_Plugin.Shared.*`) holds code used by multiple
tools: `Services/SettingsService.cs` (JSON settings load/save, try/catch
with defaults-fallback), `Services/LogExportService.cs`, `LogEntry.cs` /
`LogLevel.cs`, `Converters.cs`, `SharedStyles.xaml`. A change here has
repo-wide blast radius — check for other tools' usages before editing
signatures, since there's no test suite to catch a break in a tool that
wasn't manually tested afterward.

## 5. Multi-item batch pattern (where it exists)

Newer tools that operate on multiple picked elements in one run (e.g.
`AutoSlopeByDrain_V010`'s multi-roof support) wrap the whole batch in a
single `TransactionGroup`, with each item's own `Transaction` still
committing/rolling back independently inside it. The group is
`Assimilate()`d (kept) as long as at least one item succeeded — `RollBack()`
on the group would undo every item's transaction, including ones that
already succeeded. If you add batch support to another tool, this is the
established pattern to copy, not a per-item `TransactionGroup`.
