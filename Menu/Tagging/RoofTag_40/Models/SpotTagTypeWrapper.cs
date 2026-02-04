using Autodesk.Revit.DB;
using System;

namespace RoofTagV3.Models
{
    public class SpotTagTypeWrapper
    {
        public SpotDimensionType TagType { get; }
        public string Name => TagType.Name;
        public ElementId Id => TagType.Id;

        public SpotTagTypeWrapper(SpotDimensionType type)
        {
            TagType = type ?? throw new ArgumentNullException(nameof(type));
        }

        public override string ToString() => Name;
    }
}