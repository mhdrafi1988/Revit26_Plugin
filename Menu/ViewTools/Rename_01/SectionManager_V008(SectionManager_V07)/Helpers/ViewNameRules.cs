namespace Revit26_Plugin.SectionManager.V008.Helpers
{
    public static class ViewNameRules
    {
        public static string NormalizeDuplicate(string name)
        {
            return name.Replace("(dup)", "").Trim();
        }
    }
}
