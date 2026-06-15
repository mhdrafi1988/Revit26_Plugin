// File: Models/PluginState.cs
namespace Revit26_Plugin.APUS_V330.ViewModels
{
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
}
