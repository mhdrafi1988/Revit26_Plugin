namespace Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V005
{
    /// <summary>
    /// Per-tool persisted settings (System.Text.Json POCO), stored at
    /// %AppData%\Revit26_Plugin\FloorsAndRoofFromLinkedRooms\settings.json.
    /// Loaded on window open, saved on window close and after each run.
    /// Fields per confirmed spec: last link, last floor type, last roof type —
    /// matched by name on restore and silently skipped if no longer present in the model.
    /// </summary>
    public class ToolSettings
    {
        public string LastLinkName { get; set; }
        public string LastFloorTypeName { get; set; }
        public string LastRoofTypeName { get; set; }
    }
}
