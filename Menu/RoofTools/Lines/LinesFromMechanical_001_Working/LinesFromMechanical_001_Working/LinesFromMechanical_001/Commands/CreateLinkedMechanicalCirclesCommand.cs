using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.LinesFromMechanical.V001_01.Models;
using Revit26_Plugin.LinesFromMechanical.V001_01.Services;
using System;

namespace Revit26_Plugin.LinesFromMechanical.V001_01.Commands;

[Transaction(TransactionMode.Manual)]
public sealed class CreateLinkedMechanicalCirclesCommand : IExternalCommand
{
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements)
    {
        UIDocument uiDoc = commandData.Application.ActiveUIDocument;
        Document doc = uiDoc.Document;

        View activeView = doc.ActiveView;

        if (activeView is not ViewPlan viewPlan)
        {
            message = "Active view must be a host plan view.";
            return Result.Failed;
        }

        if (activeView.IsTemplate)
        {
            message = "Active view cannot be a view template.";
            return Result.Failed;
        }

        double radiusFeet = Helpers.UnitHelper.MillimetersToFeet(300.0);

        try
        {
            var service = new LinkedMechanicalCircleService();
            OperationSummary summary = service.CreateCircles(doc, viewPlan, radiusFeet);

            TaskDialog.Show(
                "Linked Mechanical Equipment Circles",
                summary.ToDisplayText());

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }
    }
}