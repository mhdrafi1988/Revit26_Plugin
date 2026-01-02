using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class RoofTopFaceService
    {
        public IList<Face> GetTopFaces(Solid solid)
        {
            var result = new List<Face>();

            foreach (Face face in solid.Faces)
            {
                if (face is PlanarFace pf)
                {
                    if (pf.FaceNormal.Z > 0.01)
                        result.Add(face);
                }
            }

            return result;
        }
    }
}
