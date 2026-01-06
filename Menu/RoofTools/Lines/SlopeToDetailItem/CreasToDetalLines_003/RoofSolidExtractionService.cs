using Autodesk.Revit.DB;

namespace Revit26_Plugin.CreaserAdv_V003.Services
{
    public class RoofSolidExtractionService
    {
        public Solid ExtractSolid(Element roof)
        {
            var geo = roof.get_Geometry(new Options());
            foreach (var obj in geo)
            {
                if (obj is Solid solid && solid.Volume > 0)
                    return solid;
            }
            return null;
        }
    }
}
