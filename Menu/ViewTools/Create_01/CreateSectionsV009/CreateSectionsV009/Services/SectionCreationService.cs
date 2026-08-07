using Autodesk.Revit.DB;
using Revit26_Plugin.CreateSectionsFromDetailLines.V009.Helpers;
using Revit26_Plugin.CreateSectionsFromDetailLines.V009.Models;

namespace Revit26_Plugin.CreateSectionsFromDetailLines.V009.Services
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
            out bool renamed,
            out bool usedFallback)
        {
            renamed = false;
            var o = req.Options;

            double halfLen = req.GeometryLine.Length / 2.0;

            double minZ, maxZ;
            usedFallback = req.HostElement == null;

            if (!usedFallback)
            {
                // Normal path: height derived from the matched host element's
                // bounding box, same math as V07.
                double baseZ = req.HostElement.BoundingBox.Min.Z;
                double topZ = req.HostElement.BoundingBox.Max.Z;

                minZ = baseZ - UnitConversionHelper.MmToFt(o.BottomOffsetMm);
                maxZ = topZ + UnitConversionHelper.MmToFt(o.TopPaddingMm);
            }
            else
            {
                // V008 fallback path: no qualifying host was found. Build the
                // height window from the line's own elevation using
                // SearchThresholdMm both directions, THEN still apply
                // Top Padding / Bottom Offset on top of that (per confirmed spec).
                double lineZ = req.Orientation.MidPoint.Z;
                double thresholdFt = UnitConversionHelper.MmToFt(o.SearchThresholdMm);

                minZ = lineZ - thresholdFt - UnitConversionHelper.MmToFt(o.BottomOffsetMm);
                maxZ = lineZ + thresholdFt + UnitConversionHelper.MmToFt(o.TopPaddingMm);
            }

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

            section.Name = _naming.Generate(
                _plan,
                o.Prefix,
                req.SourceLine.Id,
                out renamed);

            if (o.Template != null)
                section.ViewTemplateId = o.Template.Id;

            section.Scale = o.ViewScale;

            // ================= VIEW CROP (V009 — new) =================
            // Applied inside this same SubTransaction, on the just-created
            // section, before Commit(). CropBoxActive is set FIRST — Revit
            // requires an active crop box before CropBoxVisible or the
            // annotation crop parameter/offsets can be meaningfully set.
            ApplyViewCrop(section, o);

            st.Commit();
            return SectionCreationResult.Ok(section);
        }

        /// <summary>
        /// V009: new. Applies the 3 crop toggles and 4 annotation crop
        /// offsets to a freshly created section view.
        ///
        /// - CropBoxActive / CropBoxVisible are direct View properties
        ///   (verified: Autodesk.Revit.DB.View.CropBoxActive / .CropBoxVisible).
        /// - Annotation Crop Active is a parameter, accessed via the modern
        ///   ForgeTypeId-based parameter API: section.GetParameter(
        ///   ParameterTypeId.ViewerAnnotationCropActive) — note GetParameter
        ///   (capital G, no underscore) is the ForgeTypeId overload;
        ///   get_Parameter (lowercase, underscore) is the older
        ///   BuiltInParameter overload — they are NOT interchangeable.
        ///   CORRECTED from an earlier draft that referenced
        ///   BuiltInParameter.SECTION_ATTR_ANNO_CROP_ACTIVE via get_Parameter
        ///   — that BuiltInParameter member does not exist. Verified against
        ///   Autodesk's Revit API docs that the real, documented member is
        ///   ParameterTypeId.ViewerAnnotationCropActive (display name
        ///   "Annotation Crop"), a static ForgeTypeId, in the API since 2022,
        ///   retrieved via the GetParameter(ForgeTypeId) overload.
        ///   Flagging for Rafi: on Revit versions before 2022 this parameter
        ///   API may not be available — if this add-in must support pre-2022
        ///   Revit, this needs a version-gated fallback, which I have not
        ///   implemented since the suite targets Revit 2026.
        /// - The 4 offsets go through ViewCropRegionShapeManager
        ///   (Top/Bottom/Left/RightAnnotationCropOffset). Per Revit API docs
        ///   these MUST be non-negative — they only ever expand the
        ///   annotation crop outward from the model crop, never shrink it.
        ///   ValidateInputs() on the ViewModel already rejects negative
        ///   values before this is ever reached, so no clamping here.
        /// - If CropBoxActive is false, CropRegionVisible and
        ///   AnnotationCropActive are skipped entirely — Revit ignores
        ///   both without an active crop box, and attempting to set the
        ///   annotation crop parameter or offsets with no active crop box
        ///   is unreliable, so we don't attempt it.
        /// </summary>
        private static void ApplyViewCrop(ViewSection section, SectionCreationOptions o)
        {
            section.CropBoxActive = o.CropBoxActive;

            if (!o.CropBoxActive)
                return;

            section.CropBoxVisible = o.CropRegionVisible;

            Parameter annoCropParam =
                section.GetParameter(ParameterTypeId.ViewerAnnotationCropActive);
            annoCropParam?.Set(o.AnnotationCropActive ? 1 : 0);

            if (!o.AnnotationCropActive)
                return;

            ViewCropRegionShapeManager shapeMgr = section.GetCropRegionShapeManager();

            shapeMgr.TopAnnotationCropOffset =
                UnitConversionHelper.MmToFt(o.TopAnnotationOffsetMm);
            shapeMgr.BottomAnnotationCropOffset =
                UnitConversionHelper.MmToFt(o.BottomAnnotationOffsetMm);
            shapeMgr.LeftAnnotationCropOffset =
                UnitConversionHelper.MmToFt(o.LeftAnnotationOffsetMm);
            shapeMgr.RightAnnotationCropOffset =
                UnitConversionHelper.MmToFt(o.RightAnnotationOffsetMm);
        }
    }
}
