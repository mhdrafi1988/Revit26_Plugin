using Autodesk.Revit.UI;

namespace Revit26_Plugin.SectionManagerRefactor.V002
{
    public static class SectionManagerEventManagerRefactored
    {
        public static RenameSectionHandlerRefactored RenameHandler { get; private set; }
        public static ExternalEvent RenameEvent { get; private set; }

        public static void Initialize()
        {
            if (RenameHandler == null)
            {
                RenameHandler = new RenameSectionHandlerRefactored();
                RenameEvent = ExternalEvent.Create(RenameHandler);
            }
        }
    }
}
