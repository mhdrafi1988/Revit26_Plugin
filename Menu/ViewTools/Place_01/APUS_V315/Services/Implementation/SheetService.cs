using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V315.Helpers;
using Revit26_Plugin.APUS_V315.Models.Entities;
using Revit26_Plugin.APUS_V315.Models.Requests;
using Revit26_Plugin.APUS_V315.Services.Abstractions;
using System;

namespace Revit26_Plugin.APUS_V315.Services.Implementation;

public sealed class SheetService : ISheetService
{
    public ViewSheet CreateSheet(Document document, ElementId titleBlockId, int index)
    {
        if (!document.IsModifiable)
            throw new InvalidOperationException("Sheet creation requires an active transaction");

        var titleBlock = document.GetElement(titleBlockId) as FamilySymbol;
        if (titleBlock == null)
            throw new ArgumentException("Invalid title block ID", nameof(titleBlockId));

        if (!titleBlock.IsActive)
            titleBlock.Activate();

        var sheet = ViewSheet.Create(document, titleBlockId);
        sheet.Name = $"APUS-{index:000}";
        sheet.SheetNumber = $"AP{index:000}";

        return sheet;
    }

    public ViewSheet CreateSheet(Document document, ElementId titleBlockId, string number, string name)
    {
        if (!document.IsModifiable)
            throw new InvalidOperationException("Sheet creation requires an active transaction");

        var titleBlock = document.GetElement(titleBlockId) as FamilySymbol;
        if (titleBlock == null)
            throw new ArgumentException("Invalid title block ID", nameof(titleBlockId));

        if (!titleBlock.IsActive)
            titleBlock.Activate();

        var sheet = ViewSheet.Create(document, titleBlockId);
        sheet.SheetNumber = number;
        sheet.Name = name;

        return sheet;
    }

    public SheetPlacementArea CalculatePlacementArea(ViewSheet sheet, Margins margins)
    {
        if (sheet == null)
            throw new ArgumentNullException(nameof(sheet));

        double left = UnitConversionHelper.MmToFeet(margins.LeftMm);
        double right = UnitConversionHelper.MmToFeet(margins.RightMm);
        double top = UnitConversionHelper.MmToFeet(margins.TopMm);
        double bottom = UnitConversionHelper.MmToFeet(margins.BottomMm);

        var outline = sheet.Outline;

        double width = outline.Max.U - outline.Min.U - left - right;
        double height = outline.Max.V - outline.Min.V - top - bottom;

        var origin = new XYZ(
            outline.Min.U + left,
            outline.Max.V - top,
            0);

        return new SheetPlacementArea(origin, Math.Max(width, 0), Math.Max(height, 0));
    }

    public void DeleteSheet(Document document, ElementId sheetId)
    {
        if (!document.IsModifiable)
            throw new InvalidOperationException("Sheet deletion requires an active transaction");

        document.Delete(sheetId);
    }
}