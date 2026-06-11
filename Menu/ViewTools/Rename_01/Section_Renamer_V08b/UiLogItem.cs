namespace Revit26_Plugin.SectionAutoRenamer._01.ViewModels;

public enum UiLogLevel { Info, Warning, Error, Success }

public class UiLogItem
{
    public UiLogLevel Level   { get; }
    public string     Message { get; }
    public UiLogItem(UiLogLevel level, string message) { Level = level; Message = message; }
}
