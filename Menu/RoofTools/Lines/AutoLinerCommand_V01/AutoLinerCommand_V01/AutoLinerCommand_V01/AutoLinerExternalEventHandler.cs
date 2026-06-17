using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoLiner_V01.Services;
using Revit26_Plugin.AutoLiner_V01.ViewModels;

namespace Revit26_Plugin.AutoLiner_V01.ExternalEvents
{
    public class AutoLinerExternalEventHandler : IExternalEventHandler
    {
        public AutoLinerViewModel ViewModel { get; set; }

        public void Execute(UIApplication app)
        {
            if (ViewModel == null)
                return;

            // Call the public RunCommand if available
            if (ViewModel.RunCommand != null && ViewModel.RunCommand.CanExecute(null))
            {
                ViewModel.RunCommand.Execute(null);
            }
        }

        public string GetName()
        {
            return "AutoLiner External Event";
        }
    }
}
