using System.Reflection;
using Autodesk.Revit.UI;
using Revit26_Plugin.Menu.Ribbon;

namespace Revit26_Plugin
{
    /// <summary>
    /// Entry point for the Revit add-in.
    /// </summary>
    public partial class App : IExternalApplication
    {
        private const string RibbonTabName = "Rf_2026_JAN_26_003";

        public Result OnStartup(UIControlledApplication application)
        {
            // DEBUG: list embedded resources (TEMPORARY)
            
            EnsureRibbonTabExists(application);
            InitializeRibbonPanels(application);
            return Result.Succeeded;
        }

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
            RoofToolsRibbon.Build(application, RibbonTabName, assemblyPath);
            ViewToolsRibbon.Build(application, RibbonTabName, assemblyPath);
            DimensionsRibbon.Build(application, RibbonTabName, assemblyPath);
            SetupRibbon.Build(application, RibbonTabName, assemblyPath);
        }
    }
}
