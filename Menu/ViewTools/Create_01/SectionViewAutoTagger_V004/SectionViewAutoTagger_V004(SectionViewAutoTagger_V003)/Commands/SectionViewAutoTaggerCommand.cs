using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.SectionViewAutoTagger.V004
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SectionViewAutoTaggerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;

            var viewModel = new SectionViewAutoTaggerViewModel(uiApp);
            var window = new SectionViewAutoTaggerWindow(viewModel);

            var helper = new System.Windows.Interop.WindowInteropHelper(window)
            {
                Owner = uiApp.MainWindowHandle
            };

            window.Show();

            return Result.Succeeded;
        }
    }
}
