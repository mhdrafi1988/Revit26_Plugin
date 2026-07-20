using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofCreateTest.V001
{
    public class RoofCreateStaticHandler : IExternalEventHandler
    {
        private readonly RoofCreateTestViewModel _vm;

        public RoofCreateStaticHandler(RoofCreateTestViewModel vm) => _vm = vm;

        public string GetName() => "RoofCreateTest.V001.RoofCreateStaticHandler";

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument?.Document;
            int created = 0, failed = 0;

            try
            {
                if (doc == null) { _vm.Log(LogLevel.Error, "[Static] No active document."); failed = 1; return; }

                Level level = _vm.SelectedLevel;
                RoofType roofType = _vm.SelectedRoofType;
                if (level == null || roofType == null) { _vm.Log(LogLevel.Error, "[Static] Level or roof type not selected."); failed = 1; return; }

                CurveLoop loop = RoofTestGeometry.BuildCurveLoop();
                CurveArray curveArray = new CurveArray();
                foreach (Curve c in loop)
                {
                    curveArray.Append(c);
                }
                List<CurveArray> profileArrays = new List<CurveArray> { curveArray };

                using (Transaction tx = new Transaction(doc, "RoofCreateTest — Static API"))
                {
                    tx.Start();
                    try
                    {
                        ModelCurveArray mapping = new ModelCurveArray();
                        FootPrintRoof roof = doc.Create.NewFootPrintRoof(
                            profileArrays[0],
                            level,
                            roofType,
                            out mapping);

                        if (roof == null) { _vm.Log(LogLevel.Error, "[Static] Create returned null."); tx.RollBack(); failed = 1; return; }

                        _vm.Log(LogLevel.Info, $"[Static] Roof Id {roof.Id.Value}, mapping curves: {mapping?.Size ?? 0}");
                        tx.Commit();
                        _vm.Log(LogLevel.Success, $"[Static] Roof created — Id {roof.Id.Value}");
                        created = 1;
                    }
                    catch (Exception ex)
                    {
                        failed = 1;
                        _vm.Log(LogLevel.Error, $"[Static] Exception: {ex.Message}");
                        if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack();
                    }
                }
            }
            catch (Exception ex)
            {
                failed = 1;
                _vm.Log(LogLevel.Error, $"[Static] Unhandled: {ex.Message}");
            }
            finally
            {
                _vm.Log(LogLevel.Info, $"[Static] Done: {created} created | {failed} failed");
                _vm.OnOperationCompleted();
            }
        }
    }
}