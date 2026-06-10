namespace Revit26_Plugin.WorksetManager_02.Models
{
    /// <summary>
    /// Represents a linked file whose matching workset already exists
    /// AND all instances are already assigned to it.
    /// Grid 1 — read-only, no action needed.
    /// </summary>
    public class ExactMatchItem
    {
        public string LinkedFileName   { get; set; } = string.Empty;
        public string MatchedWorkset   { get; set; } = string.Empty;
        public int    InstanceCount    { get; set; }
    }
}
