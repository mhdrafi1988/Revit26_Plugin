namespace Revit26_Plugin.LinesFromMechanical.V003.Models;

public sealed class OperationSummary
{
    public int LinkedModelsProcessed { get; set; }
    public int MechanicalEquipmentFound { get; set; }
    public int ValidPointBasedFamilies { get; set; }
    public int DetailLinesCreated { get; set; }
    public int FloorsCreated { get; set; }
    public int SkippedElements { get; set; }
    public int DuplicateElementsSkipped { get; set; }
    public int UnloadedLinksSkipped { get; set; }
    public int ExistingElementsSkipped { get; set; }

    public string ToDisplayText()
    {
        return
            $"Linked models processed: {LinkedModelsProcessed}\n" +
            $"Mechanical Equipment elements found: {MechanicalEquipmentFound}\n" +
            $"Valid point-based families: {ValidPointBasedFamilies}\n" +
            $"Detail lines created: {DetailLinesCreated}\n" +
            $"Floors created: {FloorsCreated}\n" +
            $"Skipped elements (invalid): {SkippedElements}\n" +
            $"Duplicate elements skipped (same location): {DuplicateElementsSkipped}\n" +
            $"Existing elements skipped (already created): {ExistingElementsSkipped}\n" +
            $"Unloaded links skipped: {UnloadedLinksSkipped}";
    }
}