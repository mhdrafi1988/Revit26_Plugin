// File: PluginState.cs
namespace Revit26_Plugin.APUS.V322.ViewModels
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
