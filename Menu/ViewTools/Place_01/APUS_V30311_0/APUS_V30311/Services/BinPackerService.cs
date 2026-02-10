// File: BinPackerService.cs
using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V311.Services
{
    /// <summary>
    /// Simple bounded guillotine bin packer.
    /// Coordinates are relative to (0,0) top-left.
    /// </summary>
    internal class BinPackerService
    {
        private readonly List<Rect> _free = new();

        private record Rect(double X, double Y, double W, double H);

        public BinPackerService(double width, double height)
        {
            _free.Add(new Rect(0, 0, width, height));
        }

        public bool TryPlace(double w, double h, out double x, out double y)
        {
            for (int i = 0; i < _free.Count; i++)
            {
                var r = _free[i];

                if (w > r.W || h > r.H)
                    continue;

                x = r.X;
                y = r.Y;

                _free.RemoveAt(i);

                if (r.W - w > 0)
                    _free.Add(new Rect(r.X + w, r.Y, r.W - w, h));

                if (r.H - h > 0)
                    _free.Add(new Rect(r.X, r.Y + h, r.W, r.H - h));

                return true;
            }

            x = y = 0;
            return false;
        }
    }
}
