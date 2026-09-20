# Revit26_Plugin

Revit add-in ("Water Poofer_2015" ribbon tab, see `App.cs`), single monolithic
`Revit26_Plugin.csproj` (`net8.0-windows`) with ~60 independent tools under `Menu/`.

## Build prerequisites

- `RevitAPI.dll` / `RevitAPIUI.dll` are referenced via a local `HintPath`
  (currently `D:\Github\Revit_26\Apiref\...` in the `.csproj`) — not NuGet,
  not committed. A build will fail on any machine without a Revit install at
  that path; update the `HintPath` rather than trying to restore it as a package.

## Folder & versioning convention

Tools live at `Menu/<Category>/<Subcategory>/<ToolName>_V0xx/`, each with its
own subtree:

```
<ToolName>_V0xx/
  Commands/        IExternalCommand — thin, Revit-side entry point only
  Core/
    Models/        POCOs
    Services/      business/geometry logic — zero Revit API dependency
    (Engine/Parameters/... as needed)
  Infrastructure/
    ExternalEvents/  ExternalEventHandler + event-raising manager
    Helpers/
  UI/
    ViewModels/
    Views/         WPF window (.xaml/.xaml.cs)
```

A new tool version is a **new `_V0xx` folder**, not an in-place edit — older
versions are routinely left on disk even after a newer one becomes "live."
"Live" means wired into a `Menu/00_Push_Button_Menu_items/Ribbon/*Ribbon.cs`
file that's called from `App.cs:InitializeRibbonPanels`. A folder with no
`PushButtonData` referencing it is dead/orphaned — check the ribbon file
before assuming a version is in use, and don't delete old version folders
without confirming nothing references them.

Some tool folders only contain a `.zip` with no extracted source — these were
never wired up and can't have a PushButton added until unzipped.

## Established architecture pattern

(Confirmed by a full repo audit — see `Claude/REVIT_PLUGIN_AUDIT.md`.)

- `Command.cs` (`IExternalCommand.Execute`) wraps its whole body in try/catch:
  `OperationCanceledException` → `Result.Cancelled`, anything else →
  `TaskDialog.Show` + `Result.Failed`. Keep it thin; move logic into
  `Core/Services`.
- `Core/Services` classes have **no Revit API dependency** — pure logic,
  testable in principle even though no test project exists yet.
- All Revit document mutation happens through `ExternalEvent.Raise()` /
  an `IExternalEventHandler` in `Infrastructure/ExternalEvents/` — created
  once (field, constructor/window-load), never re-created per click.
  (A few older ViewModels commit transactions directly from a modeless
  window's UI-thread click handler instead — known, intentionally left
  alone; don't "fix" this pattern on sight, it's a confirmed exception.)
- Every `Transaction`/`TransactionGroup` is in a `using` block.
- WPF windows are modeless (`window.Show()`, not `ShowDialog()`); set the
  window's `Owner` via `WindowInteropHelper` — stated convention, but
  historically missed by some tools, so don't assume it's already done.
- Settings load via `JsonSerializer.Deserialize` wrapped in try/catch,
  falling back to defaults on failure.

## Ribbon & icons — read before touching either

Full detail in `Claude/LOGO PROMPT FOLDER/Ribbon_Code_Notes.md`. Sharp edges:

- Only the parent `PulldownButton` gets an icon by default. To icon an
  individual `PushButtonData`, capture `AddPushButton`'s return value (cast
  to `PushButton`) and set `.Image`/`.LargeImage` on it explicitly.
- `Resources/Icons/*.png` is **not** globbed into the build — each file
  needs its own `<EmbeddedResource Include="Resources\Icons\<file>.png" />`
  line in the `.csproj`, or `ImageUtils.Load` throws `FileNotFoundException`
  at `OnStartup` the first time that button is built. Several PNGs already
  sit on disk unregistered — check the `.csproj` before assuming a
  `Resources/Icons/` file is actually wired in.
- `QuickAcces.cs` (note: not `*Ribbon*.cs`) defines `QuickAccessRibbon`,
  called from `App.cs` — easy to miss in a filename-based search.

## Known gaps — don't "fix" these without asking

Deliberate/accepted per the audit, not oversights to patch reflexively:
- No test project (no xUnit/NUnit anywhere).
- No `AssemblyVersion`/`AssemblyInfo.cs` — single assembly, SDK default
  `1.0.0.0`; the `V0xx` in folder/class names tracks tool revision, not
  assembly version.
- No `SchemaVersion` on settings POCOs — no migration logic exists to need one.
- No `.addin` file tracked in source control (deployed/generated separately).

## Decision trail in code

Non-obvious decisions are recorded as dated comments in the relevant file
header rather than in a separate doc, e.g. `// per Rafi's confirmed decision
(2026-07-21)`. Check a tool's `Commands/*.cs` header before assuming
behavior is accidental — it may be a deliberate, previously-confirmed choice.

## Other docs

- `Claude/REVIT_PLUGIN_AUDIT.md` — repeatable audit checklist (memory,
  threading, error handling, structure) + findings log.
- `Claude/LOGO PROMPT FOLDER/Ribbon_Code_Notes.md` — ribbon/icon wiring notes.
- `Claude/LOGO PROMPT FOLDER/Icon_Prompts.md` — icon generation prompts per
  live button.
