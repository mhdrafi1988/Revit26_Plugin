using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.SectionAutoRenumber.Models;
using Revit26_Plugin.SectionAutoRenumber.Services;
using Revit26_Plugin.SectionAutoRenumber.ViewModels;
using System;

namespace Revit26_Plugin.SectionAutoRenumber.Handlers
{
    public class SectionAutoRenumberHandler : IExternalEventHandler
    {
        // ─── payload set by the ViewModel before Raise() ───────────────────
        public ViewSheet? TargetSheet   { get; set; }
        public int        StartNumber   { get; set; }
        public double     ThresholdFt   { get; set; }

        // ─── callback posted back to the ViewModel on completion ───────────
        public Action<RenumberSummary>? OnCompleted { get; set; }

        public void Execute(UIApplication app)
        {
            if (TargetSheet is null || OnCompleted is null) return;

            Document doc = app.ActiveUIDocument.Document;

            try
            {
                var summary = SectionAutoRenumberService.Run(
                    doc,
                    TargetSheet,
                    StartNumber,
                    ThresholdFt);

                OnCompleted(summary);
            }
            catch (Exception ex)
            {
                OnCompleted(new RenumberSummary
                {
                    SheetNumber = TargetSheet.SheetNumber,
                    SheetName   = TargetSheet.Name,
                    LogLines    = { $"[error] {ex.Message}" }
                });
            }
        }

        public string GetName() => "SectionAutoRenumber";
    }
}
