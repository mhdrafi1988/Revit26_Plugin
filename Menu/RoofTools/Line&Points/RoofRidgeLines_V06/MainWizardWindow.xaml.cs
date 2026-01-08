// File: MainWizardWindow.xaml.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Views

using System.Windows;

using Revit26_Plugin.RoofRidgeLines_V06.Logging;
using Revit26_Plugin.RoofRidgeLines_V06.Services.Context;
using Revit26_Plugin.RoofRidgeLines_V06.Services.Selection;
using Revit26_Plugin.RoofRidgeLines_V06.Services.Geometry;
using Revit26_Plugin.RoofRidgeLines_V06.Services.Transactions;
using Revit26_Plugin.RoofRidgeLines_V06.Services.Execution;
using Revit26_Plugin.RoofRidgeLines_V06.ViewModels;
using Revit26_Plugin.RoofRidgeLines_V06.Views.Adapters;

namespace Revit26_Plugin.RoofRidgeLines_V06.Views
{
    public partial class MainWizardWindow : Window
    {
        public MainWizardWindow(RevitContextService context)
        {
            InitializeComponent();

            var vm = new MainWizardViewModel();
            DataContext = vm;

            // Services
            var logSink = new UILogSink(vm.LogEntries);
            var roofSelectService = new RoofSelectionService(context);

            var execService = new WizardExecutionService(
                context,
                new RoofBoundaryService(context),
                new PerpendicularLineService(),
                new IntersectionService(),
                new DetailLineTransactionService(context),
                new ShapeEditTransactionService(context),
                logSink);

            // Adapters can now be used by step views (via code-behind hooks)
        }
    }
}
