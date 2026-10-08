using Autodesk.Revit.DB;
using Revit26_Plugin.BulkRename.V001.Core.Models;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.BulkRename.V001.Core.Sources
{
    /// <summary>
    /// One kind of project item Bulk Rename can rename. A source only lists its items; the rename itself
    /// is the same for every kind (set the element's <c>Name</c>), so adding a new kind means adding one
    /// small subclass and listing it in the view model.
    /// </summary>
    public abstract class RenameSource
    {
        /// <summary>Stable id, used to cache the loaded rows.</summary>
        public abstract string Key { get; }

        /// <summary>Name shown in the "What to rename" list.</summary>
        public abstract string DisplayName { get; }

        /// <summary>One-line explanation shown under the list.</summary>
        public abstract string Description { get; }

        /// <summary>
        /// Lists every item of this kind in <paramref name="doc"/>, sorted by name, with built-in and
        /// other-user-owned items marked locked. Must run in a Revit API context.
        /// </summary>
        public abstract List<RenameItem> Load(Document doc, Action<LogEntry> log);

        /// <inheritdoc/>
        public override string ToString() => DisplayName;

        /// <summary>Sorts rows by current name, ignoring case.</summary>
        protected static void SortByName(List<RenameItem> items)
            => items.Sort((a, b) => string.Compare(a.CurrentName, b.CurrentName, StringComparison.CurrentCultureIgnoreCase));
    }
}
