namespace Revit26_Plugin.ParaManager.V003.Models
{
    /// <summary>
    /// The two supported Revit shared-parameter binding types.
    /// Chosen once per Run click and applied to every parameter in the batch.
    /// </summary>
    public enum BindingChoice
    {
        Instance,
        Type
    }
}
