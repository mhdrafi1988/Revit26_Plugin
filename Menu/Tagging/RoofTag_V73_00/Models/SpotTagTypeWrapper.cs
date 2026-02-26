using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofTag_V73.Models
{
    public class SpotTagTypeWrapper
    {
        public SpotDimensionType TagType { get; }
        public string Name => TagType.Name;

        public SpotTagTypeWrapper(SpotDimensionType type)
        {
            TagType = type;
        }

        public override string ToString() => Name;
    }
}
