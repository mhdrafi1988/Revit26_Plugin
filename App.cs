using System.Reflection;
using Autodesk.Revit.UI;
using Revit26_Plugin.Menu.Ribbon;

namespace Revit26_Plugin
{
    /// <summary>
    //var names = Assembly.GetExecutingAssembly().GetManifestResourceNames();
    


    /// Entry point for the Revit add-in.
    /// </summary>
    public partial class App : IExternalApplication
    {
        private const string RibbonTabName = "Rf_26_22_0701";
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
            ViewToolsRibbon.Build(application, RibbonTabName, assemblyPath);
            DimensionsRibbon.Build(application, RibbonTabName, assemblyPath);
            SetupRibbon.Build(application, RibbonTabName, assemblyPath);    
        }
    }
}
