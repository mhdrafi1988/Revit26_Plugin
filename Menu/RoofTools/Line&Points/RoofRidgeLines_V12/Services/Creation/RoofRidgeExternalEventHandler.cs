using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Workflow;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Models;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Commands
{
    /// <summary>
    /// Executes roof ridge workflow safely on the Revit API thread.
    /// </summary>
    public class RoofRidgeExternalEventHandler : IExternalEventHandler
    {
        private readonly RoofRidgeWorkflow _workflow;

        public RoofRidgeContext Context { get; set; }
        public RoofRidgeResult Result { get; private set; }

        public RoofRidgeExternalEventHandler(RoofRidgeWorkflow workflow)
        {
            _workflow = workflow;
        }

        public void Execute(UIApplication app)
        {
            if (Context == null) return;

            Document doc = Context.Document;

            using (TransactionGroup tg =
                   new TransactionGroup(doc, "Roof Ridge Lines"))
            {
                tg.Start();

                using (Transaction tx =
                       new Transaction(doc, "Generate Ridge Geometry"))
                {
                    tx.Start();
                    Result = _workflow.Execute(Context);
                    tx.Commit();
                }

                tg.Assimilate();
            }
        }

        public string GetName() => "Roof Ridge Lines External Event";
    }
}
