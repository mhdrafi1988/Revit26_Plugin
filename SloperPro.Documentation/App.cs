using System.Reflection;
using Autodesk.Revit.UI;
using Revit26_Plugin.Menu.Ribbon;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin
{
    /// <summary>
    /// Entry point of the SloperPro Documentation add-in: views, sections, sheets, annotation and output tools.
    /// </summary>
    public partial class App : IExternalApplication
    {
        private const string RibbonTabName = "SloperPro Documentation";

        /// <summary>Installs the error guard and builds the SloperPro Documentation ribbon tab.</summary>
        public Result OnStartup(UIControlledApplication application)
        {
            ToolGuard.Install(typeof(App).Assembly);
            EnsureRibbonTabExists(application);
            InitializeRibbonPanels(application);
            return Result.Succeeded;
        }

        /// <summary>Nothing to clean up.</summary>
        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        private void EnsureRibbonTabExists(UIControlledApplication application)
        {
            try
            {
                application.CreateRibbonTab(RibbonTabName);
            }
            catch
            {
                // Tab already exists – ignore
            }
        }

        private void InitializeRibbonPanels(UIControlledApplication application)
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            ViewToolsRibbon.Build(application, RibbonTabName, assemblyPath);
            SheetToolsRibbon.Build(application, RibbonTabName, assemblyPath);
            AnnotateOutputRibbon.Build(application, RibbonTabName, assemblyPath);
        }
    }
}
