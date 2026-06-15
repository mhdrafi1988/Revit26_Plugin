namespace Revit26_Plugin.APUS_V315.Models.Enums;

public enum PluginState
{
    Idle,
    Initializing,
    ReadyToPlace,
    Processing,
    Cancelling,
    Completed,
    Error
}