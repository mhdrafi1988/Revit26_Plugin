using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V003.Services
{
    public class RoofTopFaceService
    {
        public IList<Face> GetTopFaces(Solid solid)
        {
            var faces = new List<Face>();
            if (solid == null) return faces;

            foreach (Face f in solid.Faces)
            {
                if (f is PlanarFace pf && pf.FaceNormal.Z > 0.9)
                    faces.Add(f);
            }
            return faces;
        }
    }
}
