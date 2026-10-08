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
    /// Fill patterns: drafting and model hatch patterns (Manage &gt; Additional Settings &gt; Fill Patterns).
    /// The built-in solid fill cannot be renamed.
    /// </summary>
    public sealed class FillPatternsSource : RenameSource
    {
        /// <inheritdoc/>
        public override string Key => "FillPatterns";

        /// <inheritdoc/>
        public override string DisplayName => "Fill patterns";

        /// <inheritdoc/>
        public override string Description
            => "Drafting and model hatch patterns (Additional Settings > Fill Patterns). The solid fill cannot be renamed.";

        /// <inheritdoc/>
        public override List<RenameItem> Load(Document doc, Action<LogEntry> log)
        {
            var items = new List<RenameItem>();

            foreach (var element in new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>())
            {
                string name = element.Name;
                if (string.IsNullOrWhiteSpace(name)) continue;

                var pattern = element.GetFillPattern();
                string kind = pattern == null ? string.Empty
                    : pattern.Target == FillPatternTarget.Model ? "Model" : "Drafting";
                string lockReason = pattern != null && pattern.IsSolidFill
                    ? "Built-in solid fill."
                    : WorksharingGuard.OwnerLock(doc, element.Id);

                items.Add(new RenameItem(element.Id, name, kind, null, lockReason));
            }

            SortByName(items);
            log(new LogEntry(LogLevel.Info,
                $"Fill patterns: {items.Count} found, {items.Count(i => !i.IsLocked)} can be renamed."));
            return items;
        }
    }
}
