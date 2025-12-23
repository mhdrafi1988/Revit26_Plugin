namespace Revit26_Plugin.Creaser_V08.Commands.Models
{
    public class CreaserInput
    {
        public double DrainClusterRadiusMm { get; }
        public double ToleranceMm { get; }

        public CreaserInput(double drainClusterRadiusMm, double toleranceMm)
        {
            DrainClusterRadiusMm = drainClusterRadiusMm;
            ToleranceMm = toleranceMm;
        }
    }
}
