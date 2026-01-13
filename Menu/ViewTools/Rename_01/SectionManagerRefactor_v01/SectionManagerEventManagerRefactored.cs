using Autodesk.Revit.UI;

namespace Revit22_Plugin.SectionManagerMVVM_Refactored
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
