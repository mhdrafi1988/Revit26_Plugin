using Autodesk.Revit.DB;

namespace Revit26.RoofTagV42.Models
{
    public class SpotTagTypeWrapper
    {
        public SpotDimensionType TagType { get; }
        public string Name => TagType.Name;
        public ElementId Id => TagType.Id;

        public SpotTagTypeWrapper(SpotDimensionType type)
        {
            TagType = type ?? throw new System.ArgumentNullException(nameof(type));
        }

        public override string ToString() => Name;
    }
}