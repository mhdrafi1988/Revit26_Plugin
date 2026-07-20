using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.Shared.Models;
using System;

namespace Revit26_Plugin.RoofCreateTest.V001
{
    public class RoofCreateHandler : IExternalEventHandler
    {
        private readonly RoofCreateTestViewModel _vm;

        public RoofCreateHandler(RoofCreateTestViewModel vm) => _vm = vm;

        public string GetName() => "RoofCreateTest.V001.RoofCreateHandler";

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument?.Document;
            int created = 0, failed = 0;

            try
            {
                if (doc == null) { _vm.Log(LogLevel.Error, "[Legacy] No active document."); failed = 1; return; }

                Level level = _vm.SelectedLevel;
                RoofType roofType = _vm.SelectedRoofType;
                if (level == null || roofType == null) { _vm.Log(LogLevel.Error, "[Legacy] Level or roof type not selected."); failed = 1; return; }

                CurveArray curveArray = RoofTestGeometry.BuildCurveArray();

                using (Transaction tx = new Transaction(doc, "RoofCreateTest — Legacy API"))
                {
                    tx.Start();
                    try
                    {
                        ModelCurveArray mapping;
                        FootPrintRoof roof = doc.Create.NewFootPrintRoof(curveArray, level, roofType, out mapping);

                        if (roof == null) { _vm.Log(LogLevel.Error, "[Legacy] NewFootPrintRoof returned null."); tx.RollBack(); failed = 1; return; }

                        _vm.Log(LogLevel.Info, $"[Legacy] Roof Id {roof.Id.Value}, mapping curves: {mapping?.Size ?? 0}");
                        tx.Commit();
                        _vm.Log(LogLevel.Success, $"[Legacy] Roof created — Id {roof.Id.Value}");
                        created = 1;
                    }
                    catch (Exception ex)
                    {
                        failed = 1;
                        _vm.Log(LogLevel.Error, $"[Legacy] Exception: {ex.Message}");
                        if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack();
                    }
                }
            }
            catch (Exception ex)
            {
                failed = 1;
                _vm.Log(LogLevel.Error, $"[Legacy] Unhandled: {ex.Message}");
            }
            finally
            {
                _vm.Log(LogLevel.Info, $"[Legacy] Done: {created} created | {failed} failed");
                _vm.OnOperationCompleted();
            }
        }
    }
}