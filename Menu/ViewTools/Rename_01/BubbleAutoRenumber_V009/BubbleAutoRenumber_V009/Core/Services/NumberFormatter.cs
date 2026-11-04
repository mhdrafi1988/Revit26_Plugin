using System.Text.RegularExpressions;
using Revit26_Plugin.BubbleAutoRenumber.V009.Core.Models;

namespace Revit26_Plugin.BubbleAutoRenumber.V009.Core.Services
{
    /// <summary>
    /// Writes and reads detail numbers in each <see cref="NumberFormat"/>. Every format
    /// works on a 1-based position (A = 1, 01 = 1, A1 = 1), so the planner only counts.
    /// No Revit API.
    /// </summary>
    public static class NumberFormatter
    {
        /// <summary>Prefix used by <see cref="NumberFormat.PrefixNumber"/> when none is given.</summary>
        public const string DefaultPrefix = "A";

        // "7 (D)", "7 (D2)" — the clash suffix DetailNumberAllocator adds.
        private static readonly Regex DuplicateSuffix = new(@"\s*\(D\d*\)\s*$", RegexOptions.Compiled);

        /// <summary>The text for <paramref name="position"/> in <paramref name="format"/>.</summary>
        public static string Format(NumberFormat format, int position, string prefix = null)
        {
            return format switch
            {
                NumberFormat.ZeroPadded => position.ToString("00"),
                NumberFormat.UpperLetters => LetterSequence.ToLetters(position),
                NumberFormat.LowerLetters => LetterSequence.ToLetters(position).ToLowerInvariant(),
                NumberFormat.PrefixNumber => (prefix ?? DefaultPrefix) + position,
                _ => position.ToString()
            };
        }

        /// <summary>
        /// Parses what the user typed in the start box for <paramref name="format"/>:
        /// a whole number above zero, or letters for the letter formats (either case).
        /// </summary>
        public static bool TryParseStart(NumberFormat format, string text, out int position)
        {
            position = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            if (IsLetters(format)) return LetterSequence.TryParse(text, out position);

            return int.TryParse(text.Trim(), out position) && position > 0;
        }

        /// <summary>
        /// Reads an existing detail number as a position, whatever format it was written
        /// in, so a sheet numbered 1–7 continues at 8, H or A8. Accepts plain or
        /// zero-padded numbers, one to three letters (I and O excluded), and
        /// <paramref name="prefix"/> + number. A "(D)" clash suffix is ignored.
        /// Returns false for anything else (e.g. "5.1", "Detail 3").
        /// </summary>
        public static bool TryReadPosition(string existing, string prefix, out int position)
        {
            position = 0;
            if (string.IsNullOrWhiteSpace(existing)) return false;

            string text = DuplicateSuffix.Replace(existing.Trim(), string.Empty);

            if (int.TryParse(text, out position) && position > 0) return true;

            if (!string.IsNullOrEmpty(prefix)
                && text.Length > prefix.Length
                && text.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)
                && int.TryParse(text.Substring(prefix.Length), out position)
                && position > 0)
                return true;

            // Letters only up to three long (ZZZ), so a word such as "SECT" is not
            // mistaken for a huge position.
            return text.Length <= 3 && LetterSequence.TryParse(text, out position);
        }

        /// <summary>True for the two letter formats.</summary>
        public static bool IsLetters(NumberFormat format) =>
            format == NumberFormat.UpperLetters || format == NumberFormat.LowerLetters;
    }
}
