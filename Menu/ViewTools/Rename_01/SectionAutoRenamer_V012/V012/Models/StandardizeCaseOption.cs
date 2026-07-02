namespace Revit26_Plugin.SectionAutoRenamer.V012.Models
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
