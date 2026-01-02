using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    /// <summary>
    /// Final classified 2D roof topology.
    /// </summary>
    public class ClassifiedRoofLoops
    {
        public EdgeLoop2D OuterLoop { get; set; }
        public IList<EdgeLoop2D> InnerLoops { get; }
            = new List<EdgeLoop2D>();
    }
}
