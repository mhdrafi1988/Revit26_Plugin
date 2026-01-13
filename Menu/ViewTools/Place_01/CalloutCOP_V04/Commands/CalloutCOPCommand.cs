using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.copv3.Views;
using Revit26_Plugin.CalloutCOP_V04.Services;
using Revit26_Plugin.CalloutCOP_V04.Views;
using System;

namespace Revit26_Plugin.CalloutCOP_V04.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CalloutCOPCommand : IExternalCommand
    {
        private static CalloutCOPWindow _window;

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            try
            {
                var context = RevitContextService.Create(data.Application);
                if (!context.IsValid)
                {
                    TaskDialog.Show("CalloutCOP", context.ErrorMessage);
                    return Result.Failed;
                }

                if (_window == null || !_window.IsVisible)
                {
                    _window = new CalloutCOPWindow(context);
                    _window.Show();
                }
                else
                {
                    _window.Activate();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("CalloutCOP Error", ex.ToString());
                return Result.Failed;
            }
        }
    }
}
