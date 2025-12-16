using Autodesk.Revit.UI;


//using Revit22_Plugin.Utils;
using System.Windows.Media.Imaging;
//using static System.Windows.Forms.VisualStyles.VisualStyleElement.Button;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class ViewToolsRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "View Tools");

            PulldownButton ViewRename = panel.AddItem(new PulldownButtonData("ViewRename", "View Rename")) as PulldownButton;
            //ViewRename.LargeImage = IconManager.ToBitmapSource(Properties.Resources.rename);                        
            ViewRename.AddPushButton(new PushButtonData("Btn_BubbleRenumberCommandV3", "Bubble Renumber Command V3", assemblyPath, "Revit22_Plugin.SDRV3.BubbleRenumberCommandV3"));
            ViewRename.AddPushButton(new PushButtonData("Btn_BubbleRenumberCommandV4", "Bubble Renumber Command V4", assemblyPath, "Revit26_Plugin.SDRV4.commands.BubbleRenumberCommandV4"));
            ViewRename.AddPushButton(new PushButtonData("Btn_SectionManagerRefactored", "Section Renamer 4.0 #4", assemblyPath, "Revit22_Plugin.SectionManagerMVVM_Refactored.SectionManagerCommandRefactored"));
            ViewRename.AddPushButton(new PushButtonData("Btn_SectionManagerRefactoredV2", "Section Renamer V2 4.0 #4", assemblyPath, "Revit26_Plugin.SectionRenamer_V02.SectionManagerEventManager"));
            ViewRename.AddPushButton(new PushButtonData("Btn_SectionManagerRefactoredV6", "Section Renamer V6 1.0 #6", assemblyPath, "Revit26_Plugin.SARV6.Commands.OpenSectionManagerCommand"));





        }
    }
}
