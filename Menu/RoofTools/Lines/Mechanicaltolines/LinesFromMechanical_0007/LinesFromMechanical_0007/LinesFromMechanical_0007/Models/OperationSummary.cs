namespace Revit26_Plugin.LinesFromMechanical.V007.Models;

public sealed class OperationSummary
{
    public int LinkedModelsProcessed    { get; set; }
    public int MechanicalEquipmentFound { get; set; }
    public int ValidPointBasedFamilies  { get; set; }
    public int DetailLinesCreated       { get; set; }
    public int FloorsCreated            { get; set; }
    public int SkippedElements          { get; set; }
    public int DuplicateElementsSkipped { get; set; }
    public int UnloadedLinksSkipped     { get; set; }
    public int ExistingElementsSkipped  { get; set; }

    public string ToDisplayText() =>
        $"Linked models processed    : {LinkedModelsProcessed}\n"  +
        $"Mechanical Equipment found : {MechanicalEquipmentFound}\n" +
        $"Valid point-based families : {ValidPointBasedFamilies}\n"  +
        $"Detail lines created       : {DetailLinesCreated}\n"  +
        $"Floors created             : {FloorsCreated}\n"       +
        $"Skipped (invalid)          : {SkippedElements}\n"     +
        $"Duplicate location skipped : {DuplicateElementsSkipped}\n" +
        $"Already exists skipped     : {ExistingElementsSkipped}\n"  +
        $"Unloaded links skipped     : {UnloadedLinksSkipped}";
}
