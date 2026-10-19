using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.DwgManager.V002.Core.Models
{
    /// <summary>
    /// What to delete for one list row: its instances, and whether to purge the CAD type afterwards.
    /// Built on the UI thread from plain values so the Revit-side code never touches view-model objects.
    /// </summary>
    public sealed class DwgDeleteRequest
    {
        /// <summary>Creates a request for the instances of one CAD file in one view.</summary>
        public DwgDeleteRequest(ElementId typeId, string fileName, string viewName,
            IReadOnlyList<ElementId> instanceIds, bool purgeType)
        {
            TypeId = typeId;
            FileName = fileName;
            ViewName = viewName;
            InstanceIds = instanceIds;
            PurgeType = purgeType;
        }

        /// <summary>The CAD link or import type.</summary>
        public ElementId TypeId { get; }

        /// <summary>File name, for the log.</summary>
        public string FileName { get; }

        /// <summary>View name, for the log.</summary>
        public string ViewName { get; }

        /// <summary>Instances to delete.</summary>
        public IReadOnlyList<ElementId> InstanceIds { get; }

        /// <summary>
        /// True → after the instances are deleted, delete the CAD type too if no instance of it is left
        /// anywhere in the model (removes the link from Manage Links / purges the import).
        /// </summary>
        public bool PurgeType { get; }
    }
}
