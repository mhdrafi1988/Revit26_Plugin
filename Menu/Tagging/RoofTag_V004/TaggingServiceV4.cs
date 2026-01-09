using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using Revit22_Plugin.RoofTagV4.Models;
using Revit22_Plugin.RoofTagV4.ViewModels;
using Revit22_Plugin.RoofTagV4.Helpers;

namespace Revit22_Plugin.RoofTagV4.Services
{
    public static class TaggingServiceV4
    {
        /// <summary>
        /// Places spot elevation tags using:
        /// ✔ V3-style faceRef projection (most stable)
        /// ✔ V4 improved point filtering
        /// ✔ V4 bend/end angle logic
        /// </summary>
        public static (int success, int fail) PlaceTags(
            UIDocument uiDoc,
            RoofBase roof,
            RoofLoopsModel geom,
            List<TagPoint> tagPoints,
            View view,
            RoofTagViewModelV4 vm)
        {
            Document doc = uiDoc.Document;
            int success = 0;
            int fail = 0;

            using (Transaction tx = new Transaction(doc, "RoofTagV4 - Place Tags"))
            {
                tx.Start();

                foreach (TagPoint tp in tagPoints)
                {
                    try
                    {
                        // ---------------------------------------------------
                        // 1️⃣ Project point to top face → get faceRef (V3 logic)
                        // ---------------------------------------------------
                        if (!TagReferenceHelperV4.GetFaceReference(
                                roof,
                                tp.Point,
                                out XYZ projected,
                                out Reference faceRef))
                        {
                            fail++;
                            continue;
                        }

                        // ---------------------------------------------------
                        // 2️⃣ Determine outward direction
                        // ---------------------------------------------------
                        XYZ outwardDir = GeometryHelperV4.GetOutwardDirection(
                            projected,
                            geom.Boundary,
                            geom.Centroid);

                        // ---------------------------------------------------
                        // 3️⃣ Compute bend + end points (V3 logic)
                        // ---------------------------------------------------
                        BendCalculationServiceV4.ComputeBendAndEndPoints(
                            geom,
                            projected,
                            outwardDir,
                            vm.BendOffsetFt,
                            vm.EndOffsetFt,
                            vm.SelectedAngle,
                            vm.BendInward,
                            out XYZ bend,
                            out XYZ end);

                        // Safety: ensure end is within boundary limits
                        end = GeometryHelperV4.FixBoundaryCollision(bend, end, geom.Boundary);

                        // ---------------------------------------------------
                        // 4️⃣ Create Spot Elevation Tag (V3 logic)
                        // ---------------------------------------------------
                        SpotDimension tag = null;

                        try
                        {
                            tag = doc.Create.NewSpotElevation(
                                view,
                                faceRef,
                                projected,  // reference point
                                bend,       // leader elbow
                                end,        // text location
                                projected,  // reference again
                                vm.UseLeader);
                        }
                        catch
                        {
                            tag = null;
                        }

                        if (tag == null)
                        {
                            fail++;
                            continue;
                        }

                        // ---------------------------------------------------
                        // 5️⃣ Assign tag type
                        // ---------------------------------------------------
                        tag.ChangeTypeId(vm.SelectedSpotTagType.TagType.Id);

                        success++;
                    }
                    catch
                    {
                        fail++;
                    }
                }

                tx.Commit();
            }

            return (success, fail);
        }
    }
}
