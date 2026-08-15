using System;
using System.Collections.Generic;
using System.Text;

namespace Revit26_Plugin.PlanFromScopeBox.V003.Core.Services
{
    /// <summary>
    /// Expands token-based naming patterns into concrete sheet/view numbers and names.
    /// Supported tokens: {ScopeBoxName}, {Level}, {Increment}, {Date}.
    ///
    /// Sheet numbers and view numbers use INDEPENDENT counters (C1, option a): sheets get
    /// AP-001, AP-002...; views separately get 001, 002.... Each counter is seeded with values
    /// already present in the document so {Increment} skips existing sheet numbers / view
    /// names and never collides.
    ///
    /// Name patterns (sheet name, view name) are resolved with the matching counter's CURRENT
    /// value so a name containing {Increment} lines up with the number it was paired with.
    /// </summary>
    public sealed class NamingResolver
    {
        private sealed class Counter
        {
            private readonly HashSet<string> _used = new(StringComparer.OrdinalIgnoreCase);
            private int _value;

            public int Current => _value;

            public void ReserveExisting(IEnumerable<string> existing)
            {
                if (existing == null) return;
                foreach (string s in existing)
                    if (!string.IsNullOrWhiteSpace(s))
                        _used.Add(s.Trim());
            }

            /// <summary>Advances to the next increment whose expanded string is unused, and
            /// registers it. Returns the produced unique string.</summary>
            public string Next(Func<int, string> expand)
            {
                while (true)
                {
                    _value++;
                    string candidate = expand(_value);
                    if (_used.Add(candidate))
                        return candidate;
                }
            }
        }

        private readonly Counter _sheet = new();
        private readonly Counter _view = new();

        /// <summary>Zero-padding width for {Increment} (e.g. 3 -> "001"). Default 3.</summary>
        public int Padding { get; set; } = 3;

        /// <summary>Seed the sheet-number counter with existing document sheet numbers.</summary>
        public void ReserveExistingSheets(IEnumerable<string> existingSheetNumbers)
            => _sheet.ReserveExisting(existingSheetNumbers);

        /// <summary>Seed the view-number counter with existing document view names/numbers.</summary>
        public void ReserveExistingViews(IEnumerable<string> existingViewNames)
            => _view.ReserveExisting(existingViewNames);

        /// <summary>Resolve a unique sheet number, advancing the sheet counter.</summary>
        public string ResolveSheetNumber(string pattern, string scopeBoxName, string level)
            => _sheet.Next(i => Expand(pattern, scopeBoxName, level, i));

        /// <summary>Resolve a sheet name using the sheet counter's current value for {Increment}.</summary>
        public string ResolveSheetName(string pattern, string scopeBoxName, string level)
            => Expand(pattern, scopeBoxName, level, _sheet.Current);

        /// <summary>Resolve a unique view number, advancing the view counter.</summary>
        public string ResolveViewNumber(string pattern, string scopeBoxName, string level)
            => _view.Next(i => Expand(pattern, scopeBoxName, level, i));

        /// <summary>Resolve a view name using the view counter's current value for {Increment}.</summary>
        public string ResolveViewName(string pattern, string scopeBoxName, string level)
            => Expand(pattern, scopeBoxName, level, _view.Current);

        private string Expand(string pattern, string scopeBoxName, string level, int increment)
        {
            if (string.IsNullOrEmpty(pattern)) return string.Empty;

            var sb = new StringBuilder(pattern);
            sb.Replace("{ScopeBoxName}", scopeBoxName ?? string.Empty);
            sb.Replace("{Level}", level ?? string.Empty);
            sb.Replace("{Increment}", increment.ToString().PadLeft(Padding, '0'));
            sb.Replace("{Date}", DateTime.Now.ToString("yyyy-MM-dd"));
            return sb.ToString();
        }
    }
}
