using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using System;
using System.Reflection;
using System.Windows.Markup;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class RoofToolsRibbon
    {

        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "Roof Tools");

            PulldownButton SlopeMenu = panel.AddItem(new PulldownButtonData("RoofSlopeMenu", "Auto SLope")) as PulldownButton;
            SlopeMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.Autoslope32.png");

            //Slope BY Point
             
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint_028", "Auto Slope ByPoint_028 (WIP ##)", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.V028.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPointRPF_V003", "Auto Slope By Point RPF V003 ####", assemblyPath, "Revit26_Plugin.AutoSlopeByPointRPF.V003.Commands.AutoSlopeCommand"));
            //Slope BY Drain

            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByDrain_V006", "Auto Slope By Drain V006 (Drain)##", assemblyPath, "Revit26_Plugin.AutoSlopeByDrain.V006.Commands.AutoSlopeByDrain"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByDrain_V007", "Auto Slope By Drain V007 (Drain)##", assemblyPath, "Revit26_Plugin.AutoSlopeByDrain.V007.Commands.AutoSlopeByDrain"));

            //Create Shape Point Shape Point Shape Point Shape Point
            PulldownButton ShapepointMenu = panel.AddItem(new PulldownButtonData("ShapepointMenu", "Shape Points")) as PulldownButton;
            ShapepointMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.Shapepoints32.png");
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_InnerLoopDivider_V009", "Divide Inner Loops   V009", assemblyPath, "Revit26_Plugin.InnerLoopDivider.V009.Commands.InnerLoopDividerCommand"));//Refactored
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofLoopAnalyzerPDC_V005", "Roof Loop Analyzer PDC V005", assemblyPath, "Revit26_Plugin.RoofLoopAnalyzerPDC.V005.Commands.RoofLoopAnalyzerPDCCommand"));//Refactored
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_OuterCurveDivider_V004", "Outer CurveDivider.V004", assemblyPath, "Revit26_Plugin.OuterCurveDivider.V004.Commands.CurveDividerCommand"));//Refactored
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofDetailLineIntersect_V011", "Roof Detail Line Intersect V011 #", assemblyPath, "Revit26_Plugin.RoofDetailLineIntersect.V011.Commands.RoofDetailLineIntersectCommand"));//Refactored
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_InnerLoopsAndPerpendicular_V005", "Inner Loops And Perpendicular V005##", assemblyPath, "Revit26_Plugin.InnerLoopsAndPerpendicular.V005.Commands.InnerLoopsAndPerpendicularCommand"));//Refactored
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_VertexReducer_V007", "RoofEdgeVertexReducer V007", assemblyPath, "Revit26_Plugin.RoofEdgeVertexReducer.V007.Commands.RoofEdgeVertexReducerCommand"));//Refactored

            PulldownButton LineAndPoint = panel.AddItem(new PulldownButtonData("LineAndPointMenu", "Line-Point")) as PulldownButton;
            LineAndPoint.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.LinePoint.png");

            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V57", "Auto Ridger(Multiple Shapes)57(By Point)", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V057.Commands.RoofRidgeCommand"));//Working
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V56", "Auto Ridger(Multiple Shapes)56(By Point)", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V56.Commands.RoofRidgeCommand"));
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V60", "Auto Ridger(Multiple Shapes)60(By Shape)", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V60.Commands.RoofRidgeCommand"));
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V62", "Auto Ridger(Multiple Shapes)62(By Shape)", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V62.Commands.RoofRidgeCommand"));
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V67", "Auto Ridger(Multiple Shapes)67(By Shape)", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V67.Commands.RoofRidgeCommand"));//Working
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V68", "Auto Ridger(Multiple Shapes)68(By Shape)", assemblyPath, "Revit26_Plugin.RoofRidgeLines.V068.Commands.RoofRidgeCommand"));

                        //Slope Liner Menu
            PulldownButton SlopeLinerMenu = panel.AddItem(new PulldownButtonData("SlopeLiner", "SlopeLiner")) as PulldownButton;
            SlopeLinerMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.SlopeLiner.png");
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V002_01", "CreaserAdvCommand V002_01 # Working", assemblyPath, "Revit26_Plugin.CreaserAdv_V002_01.Commands.CreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V003_01", "CreaserAdvCommand V003_01 # Working", assemblyPath, "Revit26_Plugin.AutoCreaser.V003.Commands.CreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V004_00", "CreaserAdvCommand V004_00 # Working", assemblyPath, "Revit26_Plugin.CreaserAdv_V004_00.Commands.CreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V005_00", "CreaserAdvCommand V005_00 # Working", assemblyPath, "Revit26_Plugin.CreaserAdv_V005_00.Commands.CreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V006_00", "CreaserAdvCommand V006_00 # Working", assemblyPath, "Revit26_Plugin.CreaserAdv_V006_00.Commands.CreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V007_00", "CreaserAdvCommand V007_00 # Working", assemblyPath, "Revit26_Plugin.CreaserAdv_V007_00.Commands.CreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V008_00", "CreaserAdvCommand V008_00 # Working", assemblyPath, "Revit26_Plugin.CreaserAdv_V008_00.Commands.CreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V009_00", "CreaserAdvCommand V009_00 # Working", assemblyPath, "Revit26_Plugin.CreaserAdv.V009.Commands.CreaserAdvCommand"));

            PulldownButton tagMenu = panel.AddItem(new PulldownButtonData("RoofTagMenu", "Tag")) as PulldownButton;
            tagMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.Addtag32.png");
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V008)", "RoofTagCommand V008", assemblyPath, "Revit26_Plugin.RoofTag_V008.RoofTagCommand"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V014)", "RoofTagCommand V014", assemblyPath, "Revit26_Plugin.RoofTag_V014.RoofTagCommand"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V015)", "RoofTagCommand V015", assemblyPath, "Revit26_Plugin.RoofTag_V015.RoofTagCommand"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V016)", "RoofTagCommand V016", assemblyPath, "Revit26_Plugin.RoofTag.V016.RoofTagCommand"));
            PulldownButton CreateMenu = panel.AddItem(new PulldownButtonData("RoofCreateMenu", "Create")) as PulldownButton;
            CreateMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.SlopeLiner.png");
            //CreateMenu.LargeImage = IconManager.ToBitmapSource(Properties.Resources.addtag32);
                        
            CreateMenu.AddPushButton(new PushButtonData("Btn_RoofFromDetailLines.V007", "Roof From Detail Lines_V007", assemblyPath, "Revit26_Plugin.RoofFromDetailLines.V007.Command"));

        }
    }
}