using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint_30_07.ViewModels;
using System.Collections.Generic;
using System.Windows;

namespace Revit26_Plugin.AutoSlopeByPoint_30_07.Views
{
    public partial class AutoSlopeWindow : Window
    {
        public AutoSlopeWindow(
            UIDocument uidoc,
            UIApplication app,
            ElementId roofId,
            List<XYZ> drains)
        {
            InitializeComponent();
            DataContext = new AutoSlopeViewModel(
                uidoc, app, roofId, drains, AddLog);
        }

        private void AddLog(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                LogBox.AppendText(msg + "\n");
                LogBox.ScrollToEnd();
            });
        }
    }
}
