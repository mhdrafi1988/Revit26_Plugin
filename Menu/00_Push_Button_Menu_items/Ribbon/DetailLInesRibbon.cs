using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class DetailLInesRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "DetailLInes");

            //Create Sections Menu
            PulldownButton DeatailLInesCreate = panel.AddItem(new PulldownButtonData("Create", "Create")) as PulldownButton;
            DeatailLInesCreate.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.Addlines_32.png");
            DeatailLInesCreate.AddPushButton(new PushButtonData("Btn_ DeatailLInes VA001", "Create  Deatail LInes From Linked Files VA001 #", assemblyPath, "Revit26_Plugin.LinkedDetailLineGenerator.VA001.Commands.OpenLinkedDetailLineGeneratorCommand"));
            DeatailLInesCreate.AddPushButton(new PushButtonData("Btn_ DeatailLInes VA002", "Create  Deatail LInes From Linked Files VA002 #", assemblyPath, "Revit26_Plugin.LinkedDetailLineGenerator.VA002.Commands.OpenLinkedDetailLineGeneratorCommand"));
            DeatailLInesCreate.AddPushButton(new PushButtonData("Btn_ DeatailLInes VA003", "Create  Deatail LInes From Linked Files VA003 #", assemblyPath, "Revit26_Plugin.LinkedDetailLineGenerator.VA003.Commands.OpenLinkedDetailLineGeneratorCommand"));

            //Process Menu
            PulldownButton DeatailLInesProcess = panel.AddItem(new PulldownButtonData("Process", "Process")) as PulldownButton;
            DeatailLInesProcess.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.Looper_32.png");
            PushButtonData closedLoopButtonData = new PushButtonData("Btn_DetailLineClosedLoop_V001", "Detail Line Closed Loop V001", assemblyPath, "Revit26_Plugin.DetailLineClosedLoop.V001.Commands.DetailLineClosedLoopCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.ClosedLoop_32.png")
            };
            DeatailLInesProcess.AddPushButton(closedLoopButtonData);

        }
    }
}
