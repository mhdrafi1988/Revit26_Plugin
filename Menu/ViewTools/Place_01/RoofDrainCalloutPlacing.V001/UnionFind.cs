using System.Collections.Generic;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V001.Helpers
{
    /// <summary>
    /// Standard union-find (disjoint set) with path compression and union by rank.
    /// Used to cluster zero-offset points by proximity — same approach as
    /// RoofDetailLineIntersect / CreaserAdvanced.
    /// </summary>
    public class UnionFind
    {
        private readonly int[] _parent;
        private readonly int[] _rank;

        public UnionFind(int size)
        {
            _parent = new int[size];
            _rank = new int[size];
            for (int i = 0; i < size; i++)
                _parent[i] = i;
        }

        public int Find(int i)
        {
            if (_parent[i] != i)
                _parent[i] = Find(_parent[i]); // path compression
            return _parent[i];
        }

        public void Union(int a, int b)
        {
            int rootA = Find(a);
            int rootB = Find(b);
            if (rootA == rootB) return;

            if (_rank[rootA] < _rank[rootB])
            {
                _parent[rootA] = rootB;
            }
            else if (_rank[rootA] > _rank[rootB])
            {
                _parent[rootB] = rootA;
            }
            else
            {
                _parent[rootB] = rootA;
                _rank[rootA]++;
            }
        }

        /// <summary>Groups indices [0, size) into clusters based on their current root.</summary>
        public List<List<int>> GetGroups(int size)
        {
            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < size; i++)
            {
                int root = Find(i);
                if (!groups.TryGetValue(root, out var list))
                {
                    list = new List<int>();
                    groups[root] = list;
                }
                list.Add(i);
            }
            return new List<List<int>>(groups.Values);
        }
    }
}
