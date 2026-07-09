namespace Revit26_Plugin.RoofEdgeVertexReducer.V003.Models
{
    /// <summary>Plain display row bound to the preview DataGrid — no Revit types.</summary>
    public class VertexPreviewRow
    {
        public string Segment { get; set; }
        public string PointId { get; set; }
        public string Z { get; set; }
        public string Action { get; set; }

        /// <summary>"Success" / "Danger" / "Secondary" — for future color styling if needed.</summary>
        public string ActionColorKey { get; set; }
    }
}
