// File: PluginState.cs
namespace Revit26_Plugin.APUS_V321_01.ViewModels
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
