using Autodesk.Revit.DB;
using Revit26_Plugin.BulkRename.V001.Core.Models;
using Revit26_Plugin.BulkRename.V001.Core.Services;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.BulkRename.V001.Core.Sources
{
    /// <summary>
    /// Arrowheads (Manage &gt; Additional Settings &gt; Arrowheads). The Revit API has no arrowhead category,
    /// so they are found two ways and merged: element types that carry the "Arrow Style" parameter
    /// (works in every Revit language), and the arrowhead types that dimension, text and tag types
    /// point at through their leader-arrowhead parameters.
    /// </summary>
    public sealed class ArrowheadsSource : RenameSource
    {
        private static readonly BuiltInParameter[] ReferenceParameters =
        {
            BuiltInParameter.DIM_LEADER_ARROWHEAD,
            BuiltInParameter.LEADER_ARROWHEAD,
            BuiltInParameter.SPOT_ELEV_LEADER_ARROWHEAD
        };

        /// <inheritdoc/>
        public override string Key => "Arrowheads";

        /// <inheritdoc/>
        public override string DisplayName => "Arrowheads";

        /// <inheritdoc/>
        public override string Description
            => "Arrowhead types (Additional Settings > Arrowheads) used by dimensions, leaders and tags.";

        /// <inheritdoc/>
        public override List<RenameItem> Load(Document doc, Action<LogEntry> log)
        {
            var found = new Dictionary<ElementId, ElementType>();
            int byParameter = 0, byReference = 0;

            foreach (var type in new FilteredElementCollector(doc).WhereElementIsElementType().OfType<ElementType>())
            {
                if (!IsOtherKind(type) && type.get_Parameter(BuiltInParameter.ARROW_TYPE) != null
                    && found.TryAdd(type.Id, type))
                    byParameter++;

                foreach (var bip in ReferenceParameters)
                {
                    var parameter = type.get_Parameter(bip);
                    if (parameter == null || parameter.StorageType != StorageType.ElementId) continue;

                    var id = parameter.AsElementId();
                    if (id == null || id.Value <= 0 || found.ContainsKey(id)) continue;

                    if (doc.GetElement(id) is ElementType arrow && !IsOtherKind(arrow))
                    {
                        found[id] = arrow;
                        byReference++;
                    }
                }
            }

            var items = new List<RenameItem>();
            foreach (var arrow in found.Values)
            {
                string name = arrow.Name;
                if (string.IsNullOrWhiteSpace(name)) continue;

                string style = arrow.get_Parameter(BuiltInParameter.ARROW_TYPE)?.AsValueString();
                items.Add(new RenameItem(arrow.Id, name, style, null, WorksharingGuard.OwnerLock(doc, arrow.Id)));
            }

            SortByName(items);
            log(new LogEntry(LogLevel.Info,
                $"Arrowheads: {items.Count} found ({byParameter} by the Arrow Style parameter, " +
                $"{byReference} more through dimension, text and tag types), {items.Count(i => !i.IsLocked)} can be renamed."));
            if (items.Count == 0)
                log(new LogEntry(LogLevel.Warning, "No arrowheads were found in this model."));
            return items;
        }

        // Dimension, text and family types are never arrowheads; excluding them keeps a stray
        // parameter match from putting one in the list.
        private static bool IsOtherKind(ElementType type)
            => type is DimensionType || type is TextElementType || type is FamilySymbol;
    }
}
