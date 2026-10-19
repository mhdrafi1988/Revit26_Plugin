namespace Revit26_Plugin_Revised.SectionAutoRenamer.V026.Models
{
    /// <summary>
    /// Case rule applied by the Standardize row. Replaces the old
    /// standalone "Title case" checkbox — None keeps original casing.
    /// </summary>
    public enum StandardizeCaseOption
    {
        None,
        UpperCase,
        TitleCase,
        SentenceCase
    }
}
