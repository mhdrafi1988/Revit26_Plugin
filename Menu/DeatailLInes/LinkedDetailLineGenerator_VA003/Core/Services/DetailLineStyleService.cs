using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Services
{
    /// <summary>
    /// Loads valid Detail Line Styles (Lines subcategories under the Lines category)
    /// available in the host project, for the Mapping Grid's style selector
    /// (spec Section 11).
    /// </summary>
    public class DetailLineStyleService
    {
        public List<GraphicsStyle> GetAvailableLineStyles(Document hostDoc)
        {
            Category? linesCategory = hostDoc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
            if (linesCategory == null) return new List<GraphicsStyle>();

            var styles = new List<GraphicsStyle>();
            foreach (Category sub in linesCategory.SubCategories)
            {
                GraphicsStyle? gs = sub.GetGraphicsStyle(GraphicsStyleType.Projection);
                if (gs != null) styles.Add(gs);
            }
            return styles.OrderBy(s => s.Name).ToList();
        }

        public GraphicsStyle? FindByName(Document hostDoc, string name)
            => GetAvailableLineStyles(hostDoc).FirstOrDefault(s => s.Name == name);
    }
}
