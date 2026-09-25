using System.Collections.Generic;

namespace Revit26_Plugin.WorksetsElementsBrowser.WSEB002.Core.Models
{
    /// <summary>Root of the tree: one user workset, holding its Categories.</summary>
    public class WorksetNodeData
    {
        public string Name { get; }
        public int WorksetId { get; }
        public bool IsEditable { get; }
        public string Owner { get; }
        public List<CategoryNodeData> Categories { get; }
        public bool IsEmpty => Categories.Count == 0;

        public WorksetNodeData(string name, int worksetId, bool isEditable, string owner, List<CategoryNodeData> categories)
        {
            Name = name;
            WorksetId = worksetId;
            IsEditable = isEditable;
            Owner = owner;
            Categories = categories;
        }
    }
}
