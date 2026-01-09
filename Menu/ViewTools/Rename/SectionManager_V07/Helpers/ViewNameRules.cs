namespace Revit26_Plugin.SectionManager_V07.Helpers
{
    public static class ViewNameRules
    {
        public static string NormalizeDuplicate(string name)
        {
            return name.Replace("(dup)", "").Trim();
        }
    }
}
