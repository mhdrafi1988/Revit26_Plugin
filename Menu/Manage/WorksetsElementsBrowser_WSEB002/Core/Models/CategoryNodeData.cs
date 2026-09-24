using System.Collections.Generic;

namespace Revit26_Plugin.WorksetsElementsBrowser.WSEB002.Core.Models
{
    /// <summary>One category (e.g. "Structural Framing") within one workset, holding its Types.</summary>
    public class CategoryNodeData
    {
        public string Name { get; }
        public List<TypeNodeData> Types { get; }

        public CategoryNodeData(string name, List<TypeNodeData> types)
        {
            Name = name;
            Types = types;
        }
    }
}
