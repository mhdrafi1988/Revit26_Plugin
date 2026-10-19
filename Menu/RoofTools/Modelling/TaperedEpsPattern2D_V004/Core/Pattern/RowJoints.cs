using System;
using System.Collections.Generic;

namespace Revit26_Plugin.TaperedEpsPattern2D.V004.Core.Pattern
{
    /// <summary>
    /// Joint positions along one row, measured in the row direction from hip (a0) to hip (b0) on the
    /// row's eave-side line. Both ends are always included. One method per row-start option.
    /// </summary>
    public static class RowJoints
    {
        private const double Eps = 1e-9;

        public static List<double> For(RowStartMode mode, MiddleStartVariant variant, double a0, double b0, double length, double minPiece) =>
            b0 - a0 <= length + Eps
                ? new List<double> { a0, b0 } // a row no longer than one board stays one piece
                : mode switch
            {
                RowStartMode.HipsSplitLeftover => SplitAtHips(a0, b0, length, minPiece),
                RowStartMode.OneHip => FromOneHip(a0, b0, length, minPiece),
                RowStartMode.FromMiddle => variant == MiddleStartVariant.BoardCentred
                    ? FromMiddle(a0, b0, length, boardCentred: true)
                    : FromMiddle(a0, b0, length, boardCentred: false),
                _ => MiddleFiller(a0, b0, length, minPiece)
            };

        /// <summary>
        /// A — full boards from both hips inward, one filler in the middle (V003). V004 change: a filler
        /// under the minimum piece size becomes a centred full board with two equal cuts beside it.
        /// </summary>
        public static List<double> MiddleFiller(double a0, double b0, double length, double minPiece)
        {
            double mid = (a0 + b0) / 2;
            var all = new List<double> { a0 };
            for (int k = 1; a0 + k * length <= mid + Eps; k++)
            {
                all.Add(a0 + k * length);
            }

            var right = new List<double> { b0 };
            for (int k = 1; b0 - k * length >= mid - Eps; k++)
            {
                right.Add(b0 - k * length);
            }

            double gap = right[right.Count - 1] - all[all.Count - 1];
            if (gap > Eps && gap < minPiece - Eps && all.Count >= 2 && right.Count >= 2)
            {
                // Filler would be a sliver: take out the board on each side of it, centre one full
                // board and leave two equal cuts of (gap + L) / 2.
                all.RemoveAt(all.Count - 1);
                right.RemoveAt(right.Count - 1);
                all.Add(mid - length / 2);
                all.Add(mid + length / 2);
            }
            else if (gap > length + Eps)
            {
                double slip = (gap - length) / 2;
                if (slip >= minPiece - Eps)
                {
                    all.Add(mid - length / 2);
                    all.Add(mid + length / 2);
                }
                else
                {
                    all.Add(mid);
                }
            }

            all.AddRange(right);
            return Clean(all);
        }

        /// <summary>
        /// B — no middle cut: n full boards centred in the row, leftover r split into two equal end
        /// pieces. If r/2 is under the minimum, one board comes out so the end pieces become (r + L)/2.
        /// </summary>
        public static List<double> SplitAtHips(double a0, double b0, double length, double minPiece)
        {
            double e = b0 - a0;
            int n = (int)Math.Floor(e / length + Eps);
            double r = e - n * length;
            if (r / 2 > Eps && r / 2 < minPiece - Eps && n >= 1)
            {
                n--;
                r += length;
            }

            var all = new List<double> { a0, b0 };
            for (int k = 0; k <= n; k++)
            {
                all.Add(a0 + r / 2 + k * length);
            }

            return Clean(all);
        }

        /// <summary>
        /// C — no middle cut: full boards from the left hip; the leftover r sits at the right-hand hip.
        /// If r is under the minimum, the last two pieces become two equal pieces of (L + r)/2.
        /// </summary>
        public static List<double> FromOneHip(double a0, double b0, double length, double minPiece)
        {
            var all = new List<double>();
            for (int k = 0; a0 + k * length < b0 - Eps; k++)
            {
                all.Add(a0 + k * length);
            }

            all.Add(b0);
            double r = all[all.Count - 1] - all[all.Count - 2];
            if (r > Eps && r < minPiece - Eps && all.Count >= 3)
            {
                all[all.Count - 2] = b0 - (length + r) / 2;
            }

            return Clean(all);
        }

        /// <summary>D — start at the exact middle: D1 a joint on the centre line, D2 a board centred on it.</summary>
        public static List<double> FromMiddle(double a0, double b0, double length, bool boardCentred)
        {
            double mid = (a0 + b0) / 2;
            double start = boardCentred ? mid - length / 2 : mid;
            var all = new List<double> { a0, b0 };
            int kMin = (int)Math.Floor((a0 - start) / length) - 1;
            int kMax = (int)Math.Ceiling((b0 - start) / length) + 1;
            for (int k = kMin; k <= kMax; k++)
            {
                double j = start + k * length;
                if (j > a0 + Eps && j < b0 - Eps)
                {
                    all.Add(j);
                }
            }

            return Clean(all);
        }

        private static List<double> Clean(List<double> joints)
        {
            joints.Sort();
            var unique = new List<double>();
            foreach (double j in joints)
            {
                if (unique.Count == 0 || j - unique[unique.Count - 1] > GeometryTolerance.Vertex)
                {
                    unique.Add(j);
                }
            }

            return unique;
        }
    }
}
