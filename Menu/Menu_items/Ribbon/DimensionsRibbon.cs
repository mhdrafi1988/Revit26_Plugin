using Autodesk.Revit.UI;
//using Revit22_Plugin.Utils;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class DimensionsRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "Dimensions");
            PulldownButton DimMenu = panel.AddItem(new PulldownButtonData("Dimensions", "Dimensions")) as PulldownButton;
            //setup.LargeImage = IconManager.ToBitmapSource(Properties.Resources.setting32);


            //PulldownButton DimMenu = panel.AddItem(new PulldownButtonData("RoofTagMenu", "Tag")) as PulldownButton;
            //tagMenu.LargeImage = IconManager.ToBitmapSource(Properties.Resources.addtag32);


            //tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagAll", "Tag All Roof Points 1.0", assemblyPath, "Revit22_Plugin.WorksetFromLinked.Commands.CreateWorksetsFromLinkedFiles"));

            //DimMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommandV5", "Roof Tag Command V5", assemblyPath, "Revit22_Plugin.RoofTagV5.RoofTagCommandV5"));
            DimMenu.AddPushButton(new PushButtonData("Btn_DtlLineDimCommand_01", "DtlLineDimCommand_01", assemblyPath, "Revit26_Plugin.DtlLineDim_V03.Commands.DtlLineDimCommand_01"));
            DimMenu.AddPushButton(new PushButtonData("Btn_AutoLinerCommand_01", "AutoLinerCommand_01", assemblyPath, "Revit26_Plugin.AutoLiner_V01.Commands.AutoLinerCommand"));
            DimMenu.AddPushButton(new PushButtonData("Btn_AutoLinerCommand_V02", "AutoLinerCommand_V02", assemblyPath, "Revit26_Plugin.AutoLiner_V02.Commands.AutoLinerCommand_V02"));
            DimMenu.AddPushButton(new PushButtonData("Btn_AutoLinerCommand_V04", "AutoLinerCommand_04", assemblyPath, "Revit26_Plugin.AutoLiner_V04.Commands.AutoLinerCommand"));

        }
    }
}
