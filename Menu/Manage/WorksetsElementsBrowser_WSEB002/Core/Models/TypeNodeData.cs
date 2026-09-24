using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.WorksetsElementsBrowser.WSEB002.Core.Models
{
    /// <summary>
    /// Leaf of the tree: one element Type (family+type, or system-family type)
    /// within one Category within one Workset, plus every instance's ElementId.
    /// A given real-world type can appear under more than one workset/category
    /// combination if its instances are split across worksets — that is
    /// intentional, each combination is its own row.
    /// </summary>
    public class TypeNodeData
    {
        public string Name { get; }
        public List<ElementId> ElementIds { get; }

        public TypeNodeData(string name, List<ElementId> elementIds)
        {
            Name = name;
            ElementIds = elementIds;
        }
    }
}
