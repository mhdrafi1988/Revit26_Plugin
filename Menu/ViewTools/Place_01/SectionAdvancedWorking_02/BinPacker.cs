using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.SectionPlacer.Services
{
    /// <summary>
    /// A simple 2D MaxRects-style bin-packing algorithm for placing section views on sheets.
    /// Works in sheet space (Revit internal units - feet).
    /// No rotation or scaling performed here.
    /// </summary>
    public class BinPacker
    {
        public class Rect
        {
            public double X, Y, W, H;
            public Rect(double x, double y, double w, double h)
            {
                X = x;
                Y = y;
                W = w;
                H = h;
            }
        }

        private readonly List<Rect> freeRects;
        private readonly double sheetWidth;
        private readonly double sheetHeight;

        public BinPacker(double usableWidth, double usableHeight, double margin, double reservedRight, double bottomMargin)
        {
            sheetWidth = usableWidth;
            sheetHeight = usableHeight;

            // Initialize available area — origin bottom-left (0,0)
            freeRects = new List<Rect>
            {
                new Rect(0, 0, usableWidth, usableHeight)
            };
        }

        /// <summary>
        /// Inserts a rectangle (section view) into the best available space.
        /// Returns null if it cannot fit.
        /// </summary>
        public Rect Insert(double width, double height)
        {
            Rect bestNode = null;
            double bestShortSide = double.MaxValue;

            foreach (var free in freeRects.ToList())
            {
                if (width <= free.W && height <= free.H)
                {
                    double leftoverH = Math.Abs(free.H - height);
                    if (leftoverH < bestShortSide)
                    {
                        bestNode = new Rect(free.X, free.Y, width, height);
                        bestShortSide = leftoverH;
                    }
                }
            }

            if (bestNode == null)
                return null;

            SplitFreeRects(bestNode);
            return bestNode;
        }

        private void SplitFreeRects(Rect used)
        {
            var newRects = new List<Rect>();

            foreach (var free in freeRects.ToList())
            {
                if (!IsOverlapping(used, free))
                    continue;

                // Horizontal split
                if (used.X < free.X + free.W && used.X + used.W > free.X)
                {
                    if (used.Y > free.Y && used.Y < free.Y + free.H)
                        newRects.Add(new Rect(free.X, free.Y, free.W, used.Y - free.Y));

                    if (used.Y + used.H < free.Y + free.H)
                        newRects.Add(new Rect(free.X, used.Y + used.H, free.W, free.Y + free.H - (used.Y + used.H)));
                }

                // Vertical split
                if (used.Y < free.Y + free.H && used.Y + used.H > free.Y)
                {
                    if (used.X > free.X && used.X < free.X + free.W)
                        newRects.Add(new Rect(free.X, free.Y, used.X - free.X, free.H));

                    if (used.X + used.W < free.X + free.W)
                        newRects.Add(new Rect(used.X + used.W, free.Y, free.X + free.W - (used.X + used.W), free.H));
                }

                freeRects.Remove(free);
            }

            freeRects.AddRange(newRects);
            PruneFreeList();
        }

        private void PruneFreeList()
        {
            for (int i = 0; i < freeRects.Count; i++)
            {
                for (int j = i + 1; j < freeRects.Count; j++)
                {
                    if (IsContainedIn(freeRects[i], freeRects[j]))
                    {
                        freeRects.RemoveAt(i);
                        i--;
                        break;
                    }
                    if (IsContainedIn(freeRects[j], freeRects[i]))
                    {
                        freeRects.RemoveAt(j);
                        j--;
                    }
                }
            }
        }

        private bool IsOverlapping(Rect a, Rect b)
        {
            return !(a.X + a.W <= b.X || a.X >= b.X + b.W ||
                     a.Y + a.H <= b.Y || a.Y >= b.Y + b.H);
        }

        private bool IsContainedIn(Rect a, Rect b)
        {
            return a.X >= b.X && a.Y >= b.Y &&
                   a.X + a.W <= b.X + b.W &&
                   a.Y + a.H <= b.Y + b.H;
        }
    }
}
