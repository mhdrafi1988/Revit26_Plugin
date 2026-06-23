namespace Revit26_Plugin.LinesFromMechanical.V001_01.Models;

public sealed class OperationSummary
{
    public int LinkedModelsProcessed { get; set; }
    public int MechanicalEquipmentFound { get; set; }
    public int ValidPointBasedFamilies { get; set; }
    public int CirclesCreated { get; set; }
    public int SkippedElements { get; set; }
    public int DuplicateElementsSkipped { get; set; }
    public int UnloadedLinksSkipped { get; set; }

    public string ToDisplayText()
    {
        return
            $"Linked models processed: {LinkedModelsProcessed}\n" +
            $"Mechanical Equipment elements found: {MechanicalEquipmentFound}\n" +
            $"Valid point-based families: {ValidPointBasedFamilies}\n" +
            $"Circles successfully created: {CirclesCreated}\n" +
            $"Skipped elements: {SkippedElements}\n" +
            $"Duplicate elements skipped: {DuplicateElementsSkipped}\n" +
            $"Unloaded links skipped: {UnloadedLinksSkipped}";
    }
}