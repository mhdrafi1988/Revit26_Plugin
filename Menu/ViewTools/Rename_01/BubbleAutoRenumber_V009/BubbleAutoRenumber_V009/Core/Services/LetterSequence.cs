using System.Text;

namespace Revit26_Plugin.BubbleAutoRenumber.V009.Core.Services
{
    /// <summary>
    /// Converts between sequence positions (1, 2, 3…) and drawing letters
    /// (A, B, C… Z, AA, AB…). I and O are never used, so they cannot be read
    /// as 1 and 0: H is followed by J, N by P, and Z by AA. No Revit API.
    /// </summary>
    public static class LetterSequence
    {
        /// <summary>The 24 letters used, in order — the alphabet without I and O.</summary>
        public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ";

        /// <summary>
        /// Letters for a 1-based position: 1 → A, 24 → Z, 25 → AA, 48 → AZ, 49 → BA.
        /// Returns an empty string for a position below 1.
        /// </summary>
        public static string ToLetters(int position)
        {
            if (position < 1) return string.Empty;

            var sb = new StringBuilder();
            int n = position;

            while (n > 0)
            {
                n--;
                sb.Insert(0, Alphabet[n % Alphabet.Length]);
                n /= Alphabet.Length;
            }

            return sb.ToString();
        }

        /// <summary>
        /// 1-based position for letters such as "A", "c" or "AB" (case-insensitive).
        /// Returns false for empty text, I, O, digits or any other character, or a
        /// value too large to count.
        /// </summary>
        public static bool TryParse(string text, out int position)
        {
            position = 0;

            if (string.IsNullOrWhiteSpace(text)) return false;

            long value = 0;

            foreach (char c in text.Trim().ToUpperInvariant())
            {
                int digit = Alphabet.IndexOf(c);
                if (digit < 0) return false;

                value = value * Alphabet.Length + digit + 1;
                if (value > int.MaxValue) return false;
            }

            position = (int)value;
            return true;
        }
    }
}
