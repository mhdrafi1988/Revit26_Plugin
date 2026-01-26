namespace BatchDwgFamilyLinker.Models
{
    public class BatchOptions
    {
        public string FamilyFolderPath { get; set; }
        public string DwgFolderPath { get; set; }

        public DwgPlacementMode PlacementMode { get; set; }

        // DEFAULT = LINK
        public DwgLoadMode LoadMode { get; set; } = DwgLoadMode.Link;
    }

    public enum DwgPlacementMode
    {
        OriginToOrigin,
        CenterToCenter
    }

    public enum DwgLoadMode
    {
        Link,
        Import
    }
}
