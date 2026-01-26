using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using BatchDwgFamilyLinker.Models;
using BatchDwgFamilyLinker.ViewModels;
using BatchDwgFamilyLinker.Services;
using BatchDwgFamilyLinker.Logging;
using System.IO;

namespace BatchDwgFamilyLinker
{
    public class FamilyBatchProcessor : IExternalEventHandler
    {
        public BatchOptions Options { get; set; }
        private readonly BatchLinkViewModel _vm;

        public FamilyBatchProcessor(BatchLinkViewModel vm)
        {
            _vm = vm;
        }

        public void Execute(UIApplication app)
        {
            var families = Directory.GetFiles(
                Options.FamilyFolderPath, "*.rfa");

            _vm.TotalFamilies = families.Length;

            foreach (var famPath in families)
            {
                Document famDoc = null;

                try
                {
                    famDoc = app.Application.OpenDocumentFile(famPath);

                    string dwgPath = Path.Combine(
                        Options.DwgFolderPath,
                        Path.GetFileNameWithoutExtension(famPath) + ".dwg");

                    if (!File.Exists(dwgPath))
                    {
                        BatchLogger.Warn(_vm, "DWG not found: " + famPath);
                        _vm.FailedCount++;
                        continue;
                    }

                    var planView = FamilyViewResolver.GetPlanView(famDoc);
                    if (planView == null)
                    {
                        BatchLogger.Error(_vm, "No plan view: " + famPath);
                        _vm.FailedCount++;
                        continue;
                    }

                    using (var tx = new Transaction(famDoc, "Load DWG"))
                    {
                        tx.Start();

                        DwgLinkService.LoadDwg(
                            famDoc,
                            planView,
                            dwgPath,
                            Options.PlacementMode,
                            Options.LoadMode);

                        tx.Commit();
                    }

                    FamilySaveService.Save(famDoc);
                    _vm.ProcessedCount++;
                }
                catch (System.Exception ex)
                {
                    _vm.FailedCount++;
                    BatchLogger.Error(_vm, ex.Message);
                }
                finally
                {
                    famDoc?.Close(false);
                }
            }
        }

        public string GetName()
            => "Batch DWG Loader (Link / Import)";
    }
}
