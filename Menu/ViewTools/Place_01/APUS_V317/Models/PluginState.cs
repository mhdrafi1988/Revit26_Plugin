// File: PluginState.cs
namespace Revit26_Plugin.APUS_V317.ViewModels
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