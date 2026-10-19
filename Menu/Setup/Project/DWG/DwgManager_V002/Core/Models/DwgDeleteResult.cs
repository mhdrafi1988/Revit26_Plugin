namespace Revit26_Plugin_Revised.DwgManager.V002.Core.Models
{
    /// <summary>Counts from one delete run, shown as the completion summary line.</summary>
    public sealed class DwgDeleteResult
    {
        /// <summary>CAD instances deleted.</summary>
        public int Deleted { get; set; }

        /// <summary>Instances left alone (already gone, or owned by another user).</summary>
        public int Skipped { get; set; }

        /// <summary>Instances or types Revit refused to delete.</summary>
        public int Failed { get; set; }

        /// <summary>CAD types purged because no instance of them was left.</summary>
        public int TypesPurged { get; set; }

        /// <summary>CAD types kept because the file is still placed in another view.</summary>
        public int TypesKept { get; set; }

        /// <summary>True when Revit rolled the whole transaction back; nothing was changed.</summary>
        public bool RolledBack { get; set; }

        /// <summary>"X deleted | Y skipped | Z failed | N type(s) purged".</summary>
        public string Summary
        {
            get
            {
                if (RolledBack) return "Rolled back — nothing was deleted.";
                string text = $"{Deleted} deleted | {Skipped} skipped | {Failed} failed";
                if (TypesPurged > 0 || TypesKept > 0)
                    text += $" | {TypesPurged} type(s) purged";
                if (TypesKept > 0)
                    text += $", {TypesKept} kept (still placed elsewhere)";
                return text;
            }
        }
    }
}
