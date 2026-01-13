using Autodesk.Revit.DB;
using Revit26_Plugin.CSFL_V07.Helpers;
using Revit26_Plugin.CSFL_V07.Models;
using Revit26_Plugin.CSFL_V07.Services.Naming;

namespace Revit26_Plugin.CSFL_V07.Services.Creation
{
    /// <summary>
    /// Creates a section view inside an already open Transaction.
    /// Uses SubTransaction for per-section rollback safety.
    /// </summary>
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

        /// <summary>
        /// Creates a section view.
        /// ASSUMES a parent Transaction is already open.
        /// </summary>
        public SectionCreationResult Create(
            SectionCreationRequest req,
            out bool nameRenamed)
        {
            nameRenamed = false;

            var vm = req.ViewModel;

            double halfLength = req.GeometryLine.Length / 2.0;

            double baseZ = req.HostElement.BoundingBox.Min.Z;
            double topZ = req.HostElement.BoundingBox.Max.Z;

            double bottomOffset = UnitConversionHelper.MmToFt(vm.BottomOffsetMm);
            double topPadding = UnitConversionHelper.MmToFt(vm.TopPaddingMm);
            double farClip = UnitConversionHelper.MmToFt(vm.FarClipMm);

            double minZ = baseZ - bottomOffset;
            double maxZ = topZ + topPadding;

            double centerZ = (minZ + maxZ) / 2.0;
            double halfHeight = (maxZ - minZ) / 2.0;

            Transform transform = Transform.Identity;
            transform.Origin = new XYZ(
                req.Orientation.MidPoint.X,
                req.Orientation.MidPoint.Y,
                centerZ);

            transform.BasisX = req.Orientation.XDir;
            transform.BasisY = req.Orientation.YDir;
            transform.BasisZ = req.Orientation.ZDir;

            BoundingBoxXYZ box = new BoundingBoxXYZ
            {
                Transform = transform,
                Min = new XYZ(-halfLength, -halfHeight, -farClip),
                Max = new XYZ(halfLength, halfHeight, farClip)
            };

            using SubTransaction st = new SubTransaction(_doc);
            st.Start();

            ViewSection section = ViewSection.CreateSection(
                _doc,
                vm.SelectedSectionType.Id,
                box);

            if (section == null)
            {
                st.RollBack();
                return SectionCreationResult.Fail("Revit failed to create section.");
            }

            section.Name = _naming.GenerateName(
                _plan,
                vm.SectionPrefix,
                req.SourceLine.Id,
                out nameRenamed);

            if (vm.SelectedTemplate != null)
                section.ViewTemplateId = vm.SelectedTemplate.Id;

            st.Commit();
            return SectionCreationResult.Ok(section);
        }
    }
}
