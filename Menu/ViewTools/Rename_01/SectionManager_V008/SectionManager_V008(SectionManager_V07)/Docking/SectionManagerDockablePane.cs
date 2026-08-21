using Autodesk.Revit.UI;
using Revit26_Plugin.SectionManager.V008.Views;

namespace Revit26_Plugin.SectionManager.V008.Docking
{
    public class SectionManagerDockablePane : IDockablePaneProvider
    {
        // ?? SINGLE INSTANCE
        public static SectionManagerDockablePane Instance { get; }
            = new SectionManagerDockablePane();

        private SectionManagerView _view;

        private SectionManagerDockablePane() { }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            _view = new SectionManagerView();

            data.FrameworkElement = _view;
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right
            };
        }

        public void Initialize(UIApplication uiApp)
        {
            _view?.Initialize(uiApp);
        }
    }
}
