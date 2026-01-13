using Autodesk.Revit.DB;

namespace Revit26_Plugin.CSFL_V07.Models
{
    /// <summary>
    /// Result object returned after attempting to create a section.
    /// </summary>
    public class SectionCreationResult
    {
        public bool Success { get; }
        public ViewSection Section { get; }
        public string ErrorMessage { get; }

        private SectionCreationResult(bool success, ViewSection section, string error)
        {
            Success = success;
            Section = section;
            ErrorMessage = error;
        }

        public static SectionCreationResult Ok(ViewSection section)
            => new(true, section, null);

        public static SectionCreationResult Fail(string error)
            => new(false, null, error);
    }
}
