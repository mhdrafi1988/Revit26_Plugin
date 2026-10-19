// File: PluginState.cs
namespace Revit26_Plugin_Revised.APUS.V324.ViewModels
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
