using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V100.Services
{
    public class SlabShapeService
    {
        private readonly Document _doc;
        private readonly ILogService _log;

        public SlabShapeService(Document document, ILogService log)
        {
            _doc = document;
            _log = log;
        }

        /// <summary>
        /// Enables slab shape editing for FootPrintRoof only.
        /// Returns null if roof type is unsupported.
        /// </summary>
        public SlabShapeEditor EnableSlabShapeEditing(RoofBase roof)
        {
            using (_log.Scope(nameof(SlabShapeService), "EnableSlabShapeEditing"))
            {
                if (roof == null)
                {
                    _log.Error(nameof(SlabShapeService),
                        "RoofBase is null.");
                    return null;
                }

                if (roof is not FootPrintRoof footprintRoof)
                {
                    _log.Error(nameof(SlabShapeService),
                        $"Unsupported roof type: {roof.GetType().Name}. " +
                        "Only FootPrintRoof supports slab shape editing.");
                    return null;
                }

                using (Transaction tx =
                    new Transaction(_doc, "Enable Slab Shape Editing"))
                {
                    tx.Start();

                    SlabShapeEditor editor =
                        footprintRoof.GetSlabShapeEditor();

                    if (!editor.IsEnabled)
                    {
                        editor.Enable();
                        _log.Info(nameof(SlabShapeService),
                            "Slab shape editing enabled.");
                    }
                    else
                    {
                        _log.Info(nameof(SlabShapeService),
                            "Slab shape editing already enabled.");
                    }

                    tx.Commit();
                    return editor;
                }
            }
        }
    }
}
