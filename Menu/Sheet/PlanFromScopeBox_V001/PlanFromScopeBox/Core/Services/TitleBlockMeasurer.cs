using System;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.PlanFromScopeBox.V001.Core.Services
{
    /// <summary>Measured title-block info: full sheet size and the clear-zone strips (mm).</summary>
    public sealed class TitleBlockExtents
    {
        public double SheetWidthMm { get; init; }
        public double SheetHeightMm { get; init; }

        /// <summary>Width of the title-block strip along the right edge (mm).</summary>
        public double ClearRightMm { get; init; }

        /// <summary>Height of the title-block strip along the bottom edge (mm).</summary>
        public double ClearBottomMm { get; init; }

        public bool Measured { get; init; }
    }

    /// <summary>
    /// Reads a title-block TYPE's sheet dimensions in API context (M3 auto-read). Must be
    /// called from inside an ExternalEvent, never on the UI thread.
    ///
    /// Approach: title-block families expose Sheet Width / Sheet Height type parameters. The
    /// clear zone (where the title strip sits) is not directly queryable from the type alone,
    /// so this returns the sheet size and leaves clear-zone estimation to a heuristic the
    /// caller can override manually. Without placing the block we cannot measure the exact
    /// artwork bounds, so ClearRight/Bottom default to 0 here and rely on manual override.
    /// A more exact measurement (placing the block and reading its bounding box) can be added
    /// later if needed.
    /// </summary>
    public sealed class TitleBlockMeasurer
    {
        private const double FeetToMm = 304.8;

        public TitleBlockExtents Measure(Document doc, ElementId titleBlockTypeId)
        {
            try
            {
                if (doc.GetElement(titleBlockTypeId) is not FamilySymbol symbol)
                    return new TitleBlockExtents { Measured = false };

                double wMm = ReadLengthParamMm(symbol, BuiltInParameter.SHEET_WIDTH);
                double hMm = ReadLengthParamMm(symbol, BuiltInParameter.SHEET_HEIGHT);

                return new TitleBlockExtents
                {
                    SheetWidthMm = wMm,
                    SheetHeightMm = hMm,
                    ClearRightMm = 0,   // manual override drives this until exact bounds read is added
                    ClearBottomMm = 0,
                    Measured = wMm > 0 && hMm > 0
                };
            }
            catch
            {
                return new TitleBlockExtents { Measured = false };
            }
        }

        private double ReadLengthParamMm(Element el, BuiltInParameter bip)
        {
            Parameter p = el.get_Parameter(bip);
            if (p == null || !p.HasValue) return 0;
            // Length params are in internal feet.
            return p.AsDouble() * FeetToMm;
        }
    }
}
