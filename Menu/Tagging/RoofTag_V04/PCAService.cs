using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.RoofTagV4.Services
{
    /// <summary>
    /// Principal Component Analysis (PCA) for extracting dominant axis.
    /// Used when longest-edge method is not reliable.
    /// </summary>
    public static class PCAService
    {
        // =======================================================================
        // MAIN ENTRY
        // =======================================================================
        public static XYZ ComputePrincipalAxis(List<XYZ> pts)
        {
            if (pts == null || pts.Count < 2)
                return XYZ.BasisX;

            // Convert to XY for PCA
            var xyPts = pts.Select(p => new double[] { p.X, p.Y }).ToList();

            // Compute mean
            double meanX = xyPts.Average(p => p[0]);
            double meanY = xyPts.Average(p => p[1]);

            // Build covariance matrix
            double covXX = 0, covXY = 0, covYY = 0;

            foreach (var p in xyPts)
            {
                double dx = p[0] - meanX;
                double dy = p[1] - meanY;

                covXX += dx * dx;
                covXY += dx * dy;
                covYY += dy * dy;
            }

            double[,] cov = new double[,]
            {
                { covXX, covXY },
                { covXY, covYY }
            };

            // Compute eigenvector of largest eigenvalue
            XYZ eigen = ComputeDominantEigenVector(cov);

            if (eigen.IsZeroLength())
                return XYZ.BasisX;

            return eigen.Normalize();
        }

        // =======================================================================
        // EIGENVALUE SOLVER FOR 2x2 MATRIX (closed form)
        // =======================================================================
        private static XYZ ComputeDominantEigenVector(double[,] m)
        {
            double a = m[0, 0];
            double b = m[0, 1];
            double d = m[1, 1];

            // Eigenvalues: λ = (a+d ± sqrt((a-d)^2 + 4b^2)) / 2
            double trace = a + d;
            double det = a * d - b * b;
            double disc = Math.Sqrt((a - d) * (a - d) + 4 * b * b);

            double lambda1 = (trace + disc) / 2.0;
            // double lambda2 = (trace - disc) / 2.0; // not needed

            // Eigenvector for λ1: (b, λ1 - a)
            double x = b;
            double y = (lambda1 - a);

            if (Math.Abs(x) < 1e-9 && Math.Abs(y) < 1e-9)
            {
                // Special fallback
                return new XYZ(1, 0, 0);
            }

            return new XYZ(x, y, 0);
        }
    }
}
