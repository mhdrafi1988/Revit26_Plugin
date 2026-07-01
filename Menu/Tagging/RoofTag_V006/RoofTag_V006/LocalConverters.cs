using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofTag_V006
{
    // ── Thin local wrappers ───────────────────────────────────────────────
    // Rule: never instantiate Revit26_Plugin.Shared.Models converters directly
    // from XAML via a foreign assembly xmlns — the XAML parser cannot resolve
    // them at compile time.  Declare sealed subclasses here (same assembly as
    // the Window) and reference them via the local: xmlns prefix instead.

    public sealed class InverseBoolConverter        : Revit26_Plugin.Shared.Models.InverseBoolConverter        { }
    public sealed class BoolToVisibilityConverter   : Revit26_Plugin.Shared.Models.BoolToVisibilityConverter   { }
    public sealed class LogLevelToColorConverter    : Revit26_Plugin.Shared.Models.LogLevelToColorConverter    { }
}
