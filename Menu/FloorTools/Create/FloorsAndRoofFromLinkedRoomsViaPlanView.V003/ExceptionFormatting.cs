using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V003
{
    /// <summary>Shared helper so every catch block in this tool logs the full exception
    /// chain (type + message per level), not just the top-level .Message.</summary>
    public static class ExceptionFormatting
    {
        public static string Full(Exception ex)
        {
            var parts = new List<string>();
            var current = ex;
            while (current != null)
            {
                string paramNote = (current is ArgumentException argEx && !string.IsNullOrEmpty(argEx.ParamName))
                    ? $" (param: '{argEx.ParamName}')"
                    : "";
                parts.Add($"{current.GetType().Name}: {current.Message}{paramNote}");
                current = current.InnerException;
            }

            string chain = string.Join(" -> ", parts);

            // Top few stack frames name the exact throwing method — the single most useful
            // thing when the message alone (e.g. bare "Value cannot be null.") is ambiguous.
            var frames = (ex.StackTrace ?? "")
                .Split('\n')
                .Select(f => f.Trim())
                .Where(f => f.Length > 0)
                .Take(3)
                .ToList();

            if (frames.Count > 0)
                chain += "  |  at: " + string.Join("  /  ", frames);

            return chain;
        }
    }
}
