using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.SectionAutoRenamer._09.ViewModels;

// UiLogLevel removed — use Revit26_Plugin.Shared.Models.LogLevel (Info/Warning/Error/Success)

public class UiLogItem
{
    public LogLevel Level   { get; }
    public string   Message { get; }
    public UiLogItem(LogLevel level, string message) { Level = level; Message = message; }
}
