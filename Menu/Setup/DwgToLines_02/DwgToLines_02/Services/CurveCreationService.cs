using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.DwgSymbolicConverter_V02.Services
{
    public enum LineCreationMode
    {
        Model,
        Symbolic
    }

    /// <summary>
    /// Creates ModelLines or SymbolicLines from curves.
    /// </summary>
    public static class CurveCreationService
    {
        public static void Create(
            UIDocument uiDoc,
            IList<Curve> curves,
            LineCreationMode mode)
        {
            Document doc = uiDoc.Document;

            View view = doc.ActiveView;
            if (view == null)
                throw new InvalidOperationException("No active view.");

            bool isFamily = doc.IsFamilyDocument;

            if (mode == LineCreationMode.Symbolic && !isFamily)
                throw new InvalidOperationException("Symbolic lines require a family document.");

            using (Transaction tx = new Transaction(doc, "DWG ? Lines"))
            {
                tx.Start();

                Plane plane = Plane.CreateByNormalAndOrigin(
                    view.ViewDirection,
                    view.Origin);

                SketchPlane sketchPlane =
                    SketchPlane.Create(doc, plane);

                foreach (Curve curve in curves)
                {
                    if (mode == LineCreationMode.Model)
                    {
                        doc.Create.NewModelCurve(curve, sketchPlane);
                    }
                    else
                    {
                        doc.FamilyCreate.NewSymbolicCurve(curve, sketchPlane);
                    }
                }

                tx.Commit();
            }
        }
    }
}
