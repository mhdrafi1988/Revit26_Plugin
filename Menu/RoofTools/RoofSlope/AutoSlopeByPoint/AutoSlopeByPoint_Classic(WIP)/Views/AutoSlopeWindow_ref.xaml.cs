using Revit26_Plugin.AutoSlopeByPoint_WIP2.ViewModels;
using Revit26_Plugin.AutoSlopeByPoint_WIP2.Services;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Revit26_Plugin.AutoSlopeByPoint_WIP2.Views
{
    public partial class AutoSlopeWindow : Window
    {
        public AutoSlopeWindow(
            UIDocument uidoc,
            UIApplication app,
            ElementId roofId,
            List<XYZ> drainPoints)
        {
            InitializeComponent();

            // Initialize services
            ILogService logService = new LogService();

            // Create and set ViewModel
            var viewModel = new AutoSlopeViewModel(
                uidoc,
                app,
                roofId,
                drainPoints,
                logService);

            DataContext = viewModel;

            // Window settings
            Title = $"Auto Slope Engine - {DateTime.Now:yyyy-MM-dd}";
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

}
