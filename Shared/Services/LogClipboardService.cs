using System;
using System.Collections;
using System.Linq;
using System.Windows;

namespace Revit26_Plugin.Shared.Services
{
    /// <summary>
    /// Copy All / Copy Selected for a tool's log panel (UI standard §5.1).
    /// Items are written with their own ToString() — for <see cref="Models.LogEntry"/>
    /// that is "HH:mm:ss  Level  Message". Used from window code-behind so the
    /// footer log gets its copy buttons without touching tool ViewModels.
    /// </summary>
    public static class LogClipboardService
    {
        /// <summary>Copies every item in <paramref name="items"/>, one per line.</summary>
        public static void CopyAll(IEnumerable items) => Copy(items);

        /// <summary>Copies the selected items (e.g. ListBox.SelectedItems), one per line.</summary>
        public static void CopySelected(IList selectedItems) => Copy(selectedItems);

        private static void Copy(IEnumerable items)
        {
            if (items == null) return;
            string text = string.Join(Environment.NewLine, items.Cast<object>().Select(i => i?.ToString()));
            if (string.IsNullOrEmpty(text)) return;
            try { Clipboard.SetText(text); }
            catch (Exception) { /* clipboard busy (another app holds it) — nothing to recover */ }
        }
    }
}
