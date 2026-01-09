using Autodesk.Revit.DB;
using System;

namespace Revit22_Plugin.AutoRoofSections.Services
{
    public class SectionCreator
    {
        private readonly Document _doc;
        private readonly Action<string> _log;

        public SectionCreator(Document doc, Action<string> log)
        {
            _doc = doc;
            _log = log;
        }

        public ViewSection CreateSectionAt(XYZ midpoint, XYZ direction, int scale)
        {
            // Convert mm to ft
            double halfLength = UnitUtils.ConvertToInternalUnits(2000, UnitTypeId.Millimeters) / 2;
            double up = UnitUtils.ConvertToInternalUnits(1000, UnitTypeId.Millimeters);
            double down = UnitUtils.ConvertToInternalUnits(1000, UnitTypeId.Millimeters);
            double farClip = UnitUtils.ConvertToInternalUnits(100, UnitTypeId.Millimeters);

            XYZ right = direction.Normalize();
            XYZ upVec = XYZ.BasisZ;

            // Construct section box
            XYZ origin = midpoint;

            Transform t = Transform.Identity;
            t.Origin = origin;
            t.BasisX = right;
            t.BasisY = upVec;
            t.BasisZ = right.CrossProduct(upVec);

            BoundingBoxXYZ box = new BoundingBoxXYZ();
            box.Transform = t;
            box.Min = new XYZ(-halfLength, -down, 0);
            box.Max = new XYZ(halfLength, up, farClip);

            // Get a section ViewFamilyType
            ViewFamilyType vft = GetSectionViewType();

            ViewSection section = ViewSection.CreateSection(_doc, vft.Id, box);
            section.Scale = scale;

            return section;
        }

        private ViewFamilyType GetSectionViewType()
        {
            FilteredElementCollector col = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewFamilyType));

            foreach (ViewFamilyType t in col)
            {
                if (t.ViewFamily == ViewFamily.Section)
                    return t;
            }

            throw new Exception("No Section ViewFamilyType found.");
        }
    }
}
