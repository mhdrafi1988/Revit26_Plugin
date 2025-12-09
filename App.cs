using System.Reflection;
using Autodesk.Revit.UI;
using Revit26_Plugin.Ribbon;

namespace Revit26_Plugin
{
    /// <summary>
    /// Entry point for the Revit add-in.
    /// </summary>
    public class App : IExternalApplication
    {
        private const string RibbonTabName = "Rf_2026_REV_03_dec12_v2";

        public Result OnStartup(UIControlledApplication application)
        {
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
                // Tab likely already exists, ignore safely.
            }
        }

        private void InitializeRibbonPanels(UIControlledApplication application)
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            RoofToolsRibbon.Build(application, RibbonTabName, assemblyPath);
            //ViewToolsRibbon.Build(application, RibbonTabName, assemblyPath);
            //SetupRibbon.Build(application, RibbonTabName, assemblyPath);
        }
    }
}
