using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.Creaser_V100.Services
{
    public class RevitContextService
    {
        private readonly UIApplication _uiApp;
        private readonly ILogService _log;

        public UIDocument UiDocument { get; private set; }
        public Document Document { get; private set; }
        public View ActiveView { get; private set; }

        public RevitContextService(
            UIApplication uiApp,
            ILogService log)
        {
            _uiApp = uiApp;
            _log = log;
        }

        public bool Validate()
        {
            using (_log.Scope(nameof(RevitContextService), "Validate"))
            {
                UiDocument = _uiApp.ActiveUIDocument;
                if (UiDocument == null)
                {
                    _log.Error(nameof(RevitContextService), "UIDocument is null.");
                    return false;
                }

                Document = UiDocument.Document;
                if (Document == null)
                {
                    _log.Error(nameof(RevitContextService), "Document is null.");
                    return false;
                }

                ActiveView = Document.ActiveView;
                if (ActiveView == null)
                {
                    _log.Error(nameof(RevitContextService), "ActiveView is null.");
                    return false;
                }

                if (!(ActiveView is ViewPlan))
                {
                    _log.Error(nameof(RevitContextService),
                        $"Invalid view type: {ActiveView.ViewType}");
                    return false;
                }

                _log.Info(nameof(RevitContextService),
                    $"Validated Plan View: {ActiveView.Name}");

                return true;
            }
        }
    }
}
