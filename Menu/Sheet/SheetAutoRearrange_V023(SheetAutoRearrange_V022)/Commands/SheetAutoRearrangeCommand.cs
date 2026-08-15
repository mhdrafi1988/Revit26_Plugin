using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.SheetAutoRearrange.V023.UI.ViewModels;
using Revit26_Plugin.SheetAutoRearrange.V023.UI.Views;
using System;
using System.Windows.Interop;

namespace Revit26_Plugin.SheetAutoRearrange.V023.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SheetAutoRearrangeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiDoc = commandData.Application.ActiveUIDocument;

                var viewModel = new SheetAutoRearrangeViewModel(uiDoc);
                var window = new SheetAutoRearrangeWindow(viewModel);

                new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;

                window.Show(); // modeless — never ShowDialog()
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
