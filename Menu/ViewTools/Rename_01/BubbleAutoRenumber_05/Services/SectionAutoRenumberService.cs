using Autodesk.Revit.DB;
using Revit26_Plugin.SectionAutoRenumber.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SectionAutoRenumber.Services
{
    public static class SectionAutoRenumberService
    {
        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC ENTRY POINT
        // ─────────────────────────────────────────────────────────────────────

        public static RenumberSummary Run(
            Document  doc,
            ViewSheet sheet,
            int       startNum,
            double    thresholdFt)
        {
            var summary = new RenumberSummary
            {
                SheetNumber = sheet.SheetNumber,
                SheetName   = sheet.Name
            };

            var entries = CollectSections(doc, sheet);

            if (!entries.Any())
            {
                summary.LogLines.Add("No section views found on sheet.");
                return summary;
            }

            var ordered = OrderByRows(entries, thresholdFt);
            var used    = CollectExistingNumbers(doc, sheet);

            summary.Total = ordered.Count;

            using var t = new Transaction(doc, "Section Auto Renumber");
            t.Start();

            int current = startNum;

            foreach (var e in ordered)
            {
                string baseNum = current.ToString();
                string unique  = MakeUnique(baseNum, used);

                try
                {
                    AssignDetailNumber(e.View, unique);
                    summary.Success++;
                    summary.LogLines.Add($"{e.View.Name} → {unique}");
                }
                catch (Exception ex)
                {
                    string msg = $"{e.View.Name}: {ex.Message}";
                    summary.Failed.Add(msg);
                    summary.LogLines.Add($"[skipped] {msg}");
                }

                current++;
            }

            t.Commit();
            return summary;
        }

        // ─────────────────────────────────────────────────────────────────────
        // COLLECT SECTIONS FOR DISPLAY  (read-only — no transaction needed)
        // ─────────────────────────────────────────────────────────────────────

        public static List<SectionRowViewModel> GetDisplayRows(
            Document  doc,
            ViewSheet sheet,
            double    thresholdFt)
        {
            var entries = CollectSections(doc, sheet);
            var ordered = OrderByRows(entries, thresholdFt);

            return ordered.Select(e =>
            {
                var p         = e.View.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                bool readOnly = p == null || p.IsReadOnly;
                string curr   = readOnly ? "—" : (p.AsString() ?? "—");

                return new SectionRowViewModel
                {
                    CurrentNumber = curr,
                    ViewName      = $"{sheet.SheetNumber} · {e.View.Name}",
                    IsReadOnly    = readOnly
                };
            }).ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        // SECTION COLLECTION
        // ─────────────────────────────────────────────────────────────────────

        private static List<SectionEntry> CollectSections(Document doc, ViewSheet sheet)
        {
            return new FilteredElementCollector(doc, sheet.Id)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .Select(vp => new SectionEntry
                {
                    Vp     = vp,
                    View   = doc.GetElement(vp.ViewId) as ViewSection,
                    Center = GetCenter(vp),
                    Box    = vp.GetBoxOutline()
                })
                .Where(e => e.View != null && e.View.ViewType == ViewType.Section)
                .ToList();
        }

        private static XYZ GetCenter(Viewport vp)
        {
            var box = vp.GetBoxOutline();
            return (box.MinimumPoint + box.MaximumPoint) / 2.0;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ROW ORDERING  — bottom-alignment Y-bucket, then left→right X
        // ─────────────────────────────────────────────────────────────────────

        private static List<SectionEntry> OrderByRows(
            List<SectionEntry> items,
            double             thresholdFt)
        {
            var rows     = new List<List<SectionEntry>>();
            var rowBands = new List<double>();

            foreach (var e in items)
            {
                double bottomY = e.Box.MinimumPoint.Y;
                bool placed    = false;

                for (int i = 0; i < rows.Count; i++)
                {
                    if (Math.Abs(bottomY - rowBands[i]) <= thresholdFt)
                    {
                        rows[i].Add(e);
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    rows.Add(new List<SectionEntry> { e });
                    rowBands.Add(bottomY);
                }
            }

            // top → bottom visually = descending Y
            rows = rows
                .OrderByDescending(r => r.Average(x => x.Box.MinimumPoint.Y))
                .ToList();

            foreach (var r in rows)
                r.Sort((a, b) => a.Center.X.CompareTo(b.Center.X));

            return rows.SelectMany(r => r).ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        // EXISTING NUMBER COLLECTION
        // ─────────────────────────────────────────────────────────────────────

        private static HashSet<string> CollectExistingNumbers(Document doc, ViewSheet sheet)
        {
            var set = new HashSet<string>();

            new FilteredElementCollector(doc, sheet.Id)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .ToList()
                .ForEach(vp =>
                {
                    if (doc.GetElement(vp.ViewId) is not View v) return;
                    var p = v.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                    if (p != null && !string.IsNullOrWhiteSpace(p.AsString()))
                        set.Add(p.AsString());
                });

            return set;
        }

        // ─────────────────────────────────────────────────────────────────────
        // UNIQUE NUMBER — base, (D), (D2), (D3) …
        // ─────────────────────────────────────────────────────────────────────

        private static string MakeUnique(string baseNum, HashSet<string> used)
        {
            if (!used.Contains(baseNum))   { used.Add(baseNum); return baseNum; }

            string d1 = $"{baseNum} (D)";
            if (!used.Contains(d1))        { used.Add(d1); return d1; }

            int i = 2;
            while (true)
            {
                string attempt = $"{baseNum} (D{i})";
                if (!used.Contains(attempt)) { used.Add(attempt); return attempt; }
                i++;
            }
        }

        private static void AssignDetailNumber(ViewSection view, string number)
        {
            var p = view.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
            if (p == null || p.IsReadOnly)
                throw new Exception("Detail number is read-only.");
            p.Set(number);
        }
    }
}
