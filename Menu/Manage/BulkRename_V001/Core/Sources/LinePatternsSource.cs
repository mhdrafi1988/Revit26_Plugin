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
    /// Line patterns: the dash and dot patterns (Manage &gt; Additional Settings &gt; Line Patterns), which
    /// CAD users call line types. Includes patterns that came in with imported DWG files.
    /// </summary>
    public sealed class LinePatternsSource : RenameSource
    {
        /// <inheritdoc/>
        public override string Key => "LinePatterns";

        /// <inheritdoc/>
        public override string DisplayName => "Line patterns";

        /// <inheritdoc/>
        public override string Description
            => "Dash and dot patterns (Additional Settings > Line Patterns), including patterns that came in with DWG imports.";

        /// <inheritdoc/>
        public override List<RenameItem> Load(Document doc, Action<LogEntry> log)
        {
            var items = new List<RenameItem>();

            foreach (var pattern in new FilteredElementCollector(doc).OfClass(typeof(LinePatternElement)).Cast<LinePatternElement>())
            {
                string name = pattern.Name;
                if (string.IsNullOrWhiteSpace(name)) continue;

                items.Add(new RenameItem(pattern.Id, name, string.Empty, null,
                    WorksharingGuard.OwnerLock(doc, pattern.Id)));
            }

            SortByName(items);
            log(new LogEntry(LogLevel.Info,
                $"Line patterns: {items.Count} found, {items.Count(i => !i.IsLocked)} can be renamed."));
            return items;
        }
    }
}
