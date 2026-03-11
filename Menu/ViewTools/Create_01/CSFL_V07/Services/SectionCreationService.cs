using Autodesk.Revit.DB;
using Revit26_Plugin.CSFL_V07.Helpers;
using Revit26_Plugin.CSFL_V07.Models;
using Revit26_Plugin.CSFL_V07.Services.Naming;

namespace Revit26_Plugin.CSFL_V07.Services.Creation
{
    public class SectionCreationService
    {
        private readonly Document _doc;
        private readonly ViewPlan _plan;
        private readonly SectionNamingService _naming;

        public SectionCreationService(Document doc, ViewPlan plan)
        {
            _doc = doc;
            _plan = plan;
            _naming = new SectionNamingService(doc);
        }

        public SectionCreationResult Create(
            SectionCreationRequest req,
            out bool renamed)
        {
            renamed = false;
            var o = req.Options;

            double halfLen = req.GeometryLine.Length / 2.0;

            double baseZ = req.HostElement.BoundingBox.Min.Z;
            double topZ = req.HostElement.BoundingBox.Max.Z;

            double minZ = baseZ - UnitConversionHelper.MmToFt(o.BottomOffsetMm);
            double maxZ = topZ + UnitConversionHelper.MmToFt(o.TopPaddingMm);

            double centerZ = (minZ + maxZ) / 2;
            double halfHeight = (maxZ - minZ) / 2;

            Transform t = Transform.Identity;
            t.Origin = new XYZ(
                req.Orientation.MidPoint.X,
                req.Orientation.MidPoint.Y,
                centerZ);

            t.BasisX = req.Orientation.XDir;
            t.BasisY = req.Orientation.YDir;
            t.BasisZ = req.Orientation.ZDir;

            BoundingBoxXYZ box = new()
            {
                Transform = t,
                Min = new XYZ(-halfLen, -halfHeight, -UnitConversionHelper.MmToFt(o.FarClipMm)),
                Max = new XYZ(halfLen, halfHeight, UnitConversionHelper.MmToFt(o.FarClipMm))
            };

            using SubTransaction st = new(_doc);
            st.Start();

            ViewSection section =
                ViewSection.CreateSection(_doc, o.SectionType.Id, box);

            if (section == null)
            {
                st.RollBack();
                return SectionCreationResult.Fail("Failed to create section.");
            }

            // Set the view name
            section.Name = _naming.Generate(
                _plan,
                o.Prefix,
                req.SourceLine.Id,
                out renamed);

            // Apply template if selected
            if (o.Template != null)
                section.ViewTemplateId = o.Template.Id;

            // Set the view scale (1:100 = 100, 1:200 = 200, etc.)
            // "Hide at coarser than" is typically set in the view template or view type
            // But we can also set the scale directly
            section.Scale = o.ViewScale;

            // If you need to set "Hide at coarser than" specifically,
            // you might need to set a parameter:
            // Parameter hideParam = section.get_Parameter(BuiltInParameter.VIEW_HIDE_AT_SCALE);
            // if (hideParam != null && hideParam.IsReadOnly == false)
            //     hideParam.Set(o.ViewScale); // Or set to 500 for 1:500

            st.Commit();
            return SectionCreationResult.Ok(section);
        }
    }
}