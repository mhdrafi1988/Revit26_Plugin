using Autodesk.Revit.UI;
using Revit26_Plugin.SectionManager_V07.Views;

namespace Revit26_Plugin.SectionManager_V07.Docking
{
    public class SectionManagerDockablePane : IDockablePaneProvider
    {
        private SectionManagerView _view;

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            _view = new SectionManagerView();

            data.FrameworkElement = _view;
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right
            };
        }

        /// <summary>
        /// Must be called from App.OnStartup AFTER registration
        /// </summary>
        public void Initialize(UIApplication uiApp)
        {
            _view?.Initialize(uiApp);
        }
    }
}
