using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_301.Services
{
    public class BinPacker
    {
        public class Rect { public double X, Y, W, H; }

        private readonly List<Rect> _free = new();

        public BinPacker(double w, double h)
        {
            _free.Add(new Rect { X = 0, Y = 0, W = w, H = h });
        }

        public Rect Insert(double w, double h)
        {
            var fit = _free.FirstOrDefault(r => r.W >= w && r.H >= h);
            if (fit == null) return null;

            _free.Remove(fit);
            _free.Add(new Rect { X = fit.X + w, Y = fit.Y, W = fit.W - w, H = h });
            _free.Add(new Rect { X = fit.X, Y = fit.Y + h, W = fit.W, H = fit.H - h });

            return new Rect { X = fit.X, Y = fit.Y, W = w, H = h };
        }
    }
}
