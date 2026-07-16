using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.PlanFromScopeBox.V001.Core.Models;

namespace Revit26_Plugin.PlanFromScopeBox.V001.Core.Services
{
    /// <summary>
    /// Reads scope boxes from the document and resolves the level from the active view.
    /// Pure read-only Revit queries — safe to call to populate the grid before any transaction.
    /// </summary>
    public sealed class ScopeBoxScanner
    {
        private const double FeetToMetres = 0.3048;

        /// <summary>
        /// Resolves the level associated with the active view. Returns null if the active
        /// view has no associated level (e.g. a drafting view or 3D view).
        /// </summary>
        public Level GetActiveLevel(Document doc, View activeView)
        {
            if (activeView == null) return null;

            // ViewPlan exposes GenLevel directly; other views fall back to LevelId.
            if (activeView is ViewPlan vp && vp.GenLevel != null)
                return vp.GenLevel;

            ElementId levelId = activeView.LevelId;
            if (levelId != null && levelId != ElementId.InvalidElementId)
                return doc.GetElement(levelId) as Level;

            return null;
        }

        /// <summary>
        /// Collects all scope boxes in the document as grid rows. The <paramref name="levelName"/>
        /// (from the active view) is stamped onto every row, since all plans use that level.
        /// Size is computed from each scope box's bounding box (model coordinates), in metres.
        /// </summary>
        public List<ScopeBoxRow> GetScopeBoxes(Document doc, string levelName)
        {
            var rows = new List<ScopeBoxRow>();

            var collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_VolumeOfInterest)
                .WhereElementIsNotElementType();

            foreach (Element el in collector)
            {
                BoundingBoxXYZ bb = el.get_BoundingBox(null);
                double widthM = 0, heightM = 0;
                if (bb != null)
                {
                    widthM = (bb.Max.X - bb.Min.X) * FeetToMetres;
                    heightM = (bb.Max.Y - bb.Min.Y) * FeetToMetres;
                }

                rows.Add(new ScopeBoxRow
                {
                    ScopeBoxId = el.Id.Value,
                    Name = el.Name,
                    Level = levelName,
                    WidthM = widthM,
                    HeightM = heightM,
                    Fits = true
                });
            }

            return rows;
        }
    }
}
