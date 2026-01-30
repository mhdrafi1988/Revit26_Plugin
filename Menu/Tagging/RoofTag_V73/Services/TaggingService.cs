// =======================================================
// File: TaggingService.cs
// Project: Revit26_Plugin.RoofTag_V73
// Layer: Services
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.DwgSymbolicConverter_V03.Models;
using Revit26_Plugin.RoofTag_V73.Helpers;
using Revit26_Plugin.RoofTag_V73.Models;
using System;
using System.Linq;

namespace Revit26_Plugin.RoofTag_V73.Services
{
    /// <summary>
    /// Handles placement of roof tags aligned to sheet/view directions.
    /// </summary>
    public class TaggingService
    {
        private readonly UIApplication _uiApp;

        public TaggingService(UIApplication uiApp)
        {
            _uiApp = uiApp ?? throw new ArgumentNullException(nameof(uiApp));
        }

        /// <summary>
        /// Places a roof tag using sheet-aligned logic.
        /// </summary>
        public void PlaceRoofTag(
            RoofBase roof,
            View view,
            ElementId tagTypeId,
            TagPlacementCorner corner,
            TagPlacementDirection direction,
            bool useLeader)
        {
            UIDocument uiDoc = _uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            ValidateContext(doc, view, roof);

            // --------------------------------------------------
            // 1. Get TOP FACE reference (correct for roof tags)
            // --------------------------------------------------
            Reference topFaceRef =
                HostObjectUtils.GetTopFaces(roof).FirstOrDefault();

            if (topFaceRef == null)
                throw new InvalidOperationException(
                    "Failed to obtain top face reference from roof.");

            // --------------------------------------------------
            // 2. Calculate anchor point (roof top face centroid)
            // --------------------------------------------------
            XYZ anchorPoint =
                GeometryHelper.GetRoofTopFaceCentroid(
                    roof,
                    topFaceRef,
                    doc);

            // --------------------------------------------------
            // 3. Calculate sheet-aligned tag head position
            // --------------------------------------------------
            double offset =
                GetPaperOffset(view, 3.0); // 3mm on paper (office standard)

            XYZ tagHeadPosition =
                GeometryHelper.CalculateTagPosition(
                    anchorPoint,
                    view,
                    corner,
                    direction,
                    offset);

            // --------------------------------------------------
            // 4. Create tag (single safe transaction)
            // --------------------------------------------------
            using (Transaction tx =
                   new Transaction(doc, "Place Roof Tag"))
            {
                tx.Start();

                IndependentTag tag =
                    IndependentTag.Create(
                        doc,
                        tagTypeId,
                        view.Id,
                        topFaceRef,
                        useLeader,
                        TagOrientation.Horizontal,
                        tagHeadPosition);

                if (tag == null)
                    throw new InvalidOperationException(
                        "Revit failed to create the roof tag.");

                tx.Commit();
            }
        }

        // ==================================================
        // Validation & Utilities
        // ==================================================

        private static void ValidateContext(
            Document doc,
            View view,
            RoofBase roof)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));

            if (view == null)
                throw new ArgumentNullException(nameof(view));

            if (roof == null)
                throw new ArgumentNullException(nameof(roof));

            if (view.IsTemplate)
                throw new InvalidOperationException(
                    "Cannot place tags in a view template.");

            // Supported view types for roof tags
            if (view.ViewType != ViewType.FloorPlan &&
                view.ViewType != ViewType.EngineeringPlan &&
                view.ViewType != ViewType.CeilingPlan &&
                view.ViewType != ViewType.Section &&
                view.ViewType != ViewType.Elevation)
            {
                throw new InvalidOperationException(
                    $"Roof tags are not supported in view type: {view.ViewType}");
            }
        }

        /// <summary>
        /// Converts a paper distance (mm) to model distance (feet),
        /// taking view scale into account.
        /// </summary>
        private static double GetPaperOffset(
            View view,
            double paperMm)
        {
            // Revit internal units = feet
            // 1 foot = 304.8 mm
            return (paperMm / 304.8) * view.Scale;
        }
    }
}
