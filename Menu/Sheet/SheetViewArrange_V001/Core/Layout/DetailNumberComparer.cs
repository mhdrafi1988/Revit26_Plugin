using System;
using System.Collections.Generic;
using System.Numerics;

namespace Revit26_Plugin.SheetViewArrange.V001.Core.Layout
{
    /// <summary>
    /// Orders detail numbers the way a person counts: digit runs compare as numbers and text
    /// runs compare case-insensitively, so 1, 2, 10 (not 1, 10, 2) and A1, A2, A10, B1.
    /// Blank numbers sort last.
    /// </summary>
    public sealed class DetailNumberComparer : IComparer<string>
    {
        /// <summary>Shared instance.</summary>
        public static DetailNumberComparer Instance { get; } = new();

        /// <summary>Compares two detail numbers in natural order.</summary>
        public int Compare(string x, string y)
        {
            bool xBlank = string.IsNullOrWhiteSpace(x);
            bool yBlank = string.IsNullOrWhiteSpace(y);
            if (xBlank || yBlank)
                return xBlank == yBlank ? 0 : (xBlank ? 1 : -1);

            x = x.Trim();
            y = y.Trim();
            int i = 0, j = 0;
            while (i < x.Length && j < y.Length)
            {
                bool xDigit = char.IsAsciiDigit(x[i]);
                bool yDigit = char.IsAsciiDigit(y[j]);

                // A number sorts before text at the same position ("1" before "A").
                if (xDigit != yDigit)
                    return xDigit ? -1 : 1;

                int iEnd = Run(x, i, xDigit);
                int jEnd = Run(y, j, yDigit);
                string xs = x.Substring(i, iEnd - i);
                string ys = y.Substring(j, jEnd - j);

                int cmp = xDigit
                    ? BigInteger.Parse(xs).CompareTo(BigInteger.Parse(ys))
                    : string.Compare(xs, ys, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                    return cmp;

                i = iEnd;
                j = jEnd;
            }

            int lengthCmp = (x.Length - i).CompareTo(y.Length - j);
            return lengthCmp != 0 ? lengthCmp : string.CompareOrdinal(x, y);
        }

        private static int Run(string s, int start, bool digits)
        {
            int k = start;
            while (k < s.Length && char.IsAsciiDigit(s[k]) == digits)
                k++;
            return k;
        }
    }
}
