using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.DetailLineClosedLoop.V001.UI.ViewModels;
using Revit26_Plugin.DetailLineClosedLoop.V001.UI.Views;

namespace Revit26_Plugin.DetailLineClosedLoop.V001.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class DetailLineClosedLoopCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                Document doc = uiApp.ActiveUIDocument?.Document;

                if (doc == null)
                {
                    TaskDialog.Show("Detail Line Closed Loop", "No active document.");
                    return Result.Failed;
                }

                if (doc.IsReadOnly)
                {
                    TaskDialog.Show("Detail Line Closed Loop", "Document is read-only.");
                    return Result.Failed;
                }

                var vm = new DetailLineClosedLoopViewModel(uiApp);
                var window = new DetailLineClosedLoopWindow { DataContext = vm };

                new WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;
                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.ToString();
                return Result.Failed;
            }
        }
    }
}
