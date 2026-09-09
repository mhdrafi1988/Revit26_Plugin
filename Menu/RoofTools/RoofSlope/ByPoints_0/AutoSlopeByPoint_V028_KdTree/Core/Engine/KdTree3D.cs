using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPointKdTree.VKD01.Core.Engine
{
    /// <summary>
    /// Minimal 3D KD-tree over vertex positions. Replaces the O(n^2) all-pairs
    /// scan in DijkstraPathEngine.BuildGraph with an O(n log n) build and
    /// O(log n + k) radius queries, where k is the number of vertices actually
    /// within the candidate-edge threshold of a given point.
    /// </summary>
    internal sealed class KdTree3D
    {
        private sealed class Node
        {
            public int Index;
            public XYZ Point;
            public Node Left;
            public Node Right;
            public int Axis;
        }

        private readonly Node _root;

        public KdTree3D(IReadOnlyList<XYZ> points)
        {
            var items = new (XYZ pt, int idx)[points.Count];
            for (int i = 0; i < points.Count; i++) items[i] = (points[i], i);
            _root = Build(items, 0, items.Length, 0);
        }

        private static Node Build((XYZ pt, int idx)[] items, int start, int end, int depth)
        {
            if (start >= end) return null;

            int axis = depth % 3;
            int mid = start + (end - start) / 2;
            SelectMedian(items, start, end, mid, axis);

            return new Node
            {
                Point = items[mid].pt,
                Index = items[mid].idx,
                Axis = axis,
                Left = Build(items, start, mid, depth + 1),
                Right = Build(items, mid + 1, end, depth + 1)
            };
        }

        // Quickselect: partitions items[start,end) in place so the element at
        // `mid` is the one that would be there in fully sorted order along `axis`.
        private static void SelectMedian((XYZ pt, int idx)[] items, int start, int end, int mid, int axis)
        {
            while (end - start > 1)
            {
                int pivotIndex = Partition(items, start, end, axis);
                if (pivotIndex == mid) return;
                if (mid < pivotIndex) end = pivotIndex;
                else start = pivotIndex + 1;
            }
        }

        private static int Partition((XYZ pt, int idx)[] items, int start, int end, int axis)
        {
            int pivot = start + (end - start) / 2;
            double pivotVal = Coord(items[pivot].pt, axis);
            Swap(items, pivot, end - 1);

            int store = start;
            for (int i = start; i < end - 1; i++)
            {
                if (Coord(items[i].pt, axis) < pivotVal)
                {
                    Swap(items, i, store);
                    store++;
                }
            }
            Swap(items, store, end - 1);
            return store;
        }

        private static void Swap((XYZ pt, int idx)[] items, int a, int b)
        {
            (items[a], items[b]) = (items[b], items[a]);
        }

        private static double Coord(XYZ p, int axis) => axis == 0 ? p.X : axis == 1 ? p.Y : p.Z;

        /// <summary>
        /// Appends the indices of every point within `radius` of `query` to
        /// `results` (including, if present, the query point's own index —
        /// callers filter self-matches themselves, same as the old i&lt;j loop).
        /// </summary>
        public void RangeSearch(XYZ query, double radius, List<int> results)
        {
            SearchNode(_root, query, radius * radius, radius, results);
        }

        private void SearchNode(Node node, XYZ query, double r2, double radius, List<int> results)
        {
            if (node == null) return;

            double dx = node.Point.X - query.X;
            double dy = node.Point.Y - query.Y;
            double dz = node.Point.Z - query.Z;
            double d2 = dx * dx + dy * dy + dz * dz;
            if (d2 <= r2) results.Add(node.Index);

            double diff = Coord(query, node.Axis) - Coord(node.Point, node.Axis);
            Node near = diff <= 0 ? node.Left : node.Right;
            Node far = diff <= 0 ? node.Right : node.Left;

            SearchNode(near, query, r2, radius, results);
            // Only descend into the far side if the splitting plane itself is
            // within range — this is what keeps the query sub-linear.
            if (Math.Abs(diff) <= radius)
                SearchNode(far, query, r2, radius, results);
        }
    }
}
