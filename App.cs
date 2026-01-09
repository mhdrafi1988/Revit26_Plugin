using System.Reflection;
using Autodesk.Revit.UI;
using Revit26_Plugin.Menu.Ribbon;
using Revit26_Plugin.SectionManager_V07.Docking;

namespace Revit26_Plugin
{
    /// <summary>
    /// Entry point for the Revit add-in.
    /// Registers ribbon + dockable panes.
    /// </summary>
    public partial class App : IExternalApplication
    {
        private const string RibbonTabName = "Rf_2026_JAN_09_003";

        public Result OnStartup(UIControlledApplication application)
        {
            EnsureRibbonTabExists(application);

            // ✅ Register dockable pane ONCE using singleton instance
            RegisterDockablePanes(application);

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
                // Tab already exists – safe to ignore
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

        private void RegisterDockablePanes(UIControlledApplication application)
        {
            try
            {
                application.RegisterDockablePane(
                    DockablePaneIds.SectionManagerPaneId,
                    "Section Manager",
                    SectionManagerDockablePane.Instance   // 🔴 IMPORTANT
                );
            }
            catch
            {
                // Pane already registered (debug reload / restart scenario)
            }
        }
    }
}
