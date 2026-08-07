using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofEdgeSections.V002
{
    /// <summary>
    /// Searches for the nearest wall along a given direction from a search origin,
    /// used to size the section crop width dynamically around the roof-to-wall junction.
    /// </summary>
    public static class NearbyWallFinder
    {
        public class Result
        {
            public Wall FoundWall { get; set; }
            public double WallWidthFeet { get; set; }
            public bool WallFound { get; set; }

            /// <summary>True when the search could not run at all (no 3D view available),
            /// as distinct from WallFound = false (search ran, nothing in range).</summary>
            public bool SearchUnavailable { get; set; }
        }

        /// <summary>
        /// Casts a ReferenceIntersector search from origin along searchDir, up to
        /// searchDistanceFeet, for the nearest Wall. Returns WallFound = false if none
        /// found or if the found wall has no valid Width (e.g., curtain wall).
        /// Returns SearchUnavailable = true if searchView3D is null (caller resolves it
        /// once per Run via FindAny3DView — not re-queried here per call).
        /// </summary>
        public static Result FindNearbyWall(Document doc, View3D searchView3D, XYZ origin, XYZ searchDir, double searchDistanceFeet)
        {
            if (searchView3D == null)
            {
                return new Result { WallFound = false, SearchUnavailable = true };
            }

            ReferenceIntersector intersector = new ReferenceIntersector(
                new ElementCategoryFilter(BuiltInCategory.OST_Walls),
                FindReferenceTarget.Element,
                searchView3D);

            IList<ReferenceWithContext> hits = intersector.Find(origin, searchDir);

            foreach (ReferenceWithContext hit in hits.OrderBy(h => h.Proximity))
            {
                if (hit.Proximity > searchDistanceFeet)
                    break;

                Element el = doc.GetElement(hit.GetReference().ElementId);
                if (el is Wall wall)
                {
                    double width = wall.Width; // 0 for curtain walls / invalid compound structure
                    return new Result
                    {
                        FoundWall = wall,
                        WallWidthFeet = width,
                        WallFound = width > 0.001
                    };
                }
            }

            return new Result { WallFound = false };
        }

        /// <summary>
        /// Reuses any existing non-template 3D view for the ReferenceIntersector context;
        /// required because RI needs a View3D even though nothing is rendered. Does not
        /// create or modify any document state. Call once per Run and pass the result to
        /// FindNearbyWall for every row — resolving it per-row would re-run this collector
        /// query redundantly for an answer that cannot change mid-Run.
        /// </summary>
        public static View3D FindAny3DView(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => !v.IsTemplate);
        }
    }
}
