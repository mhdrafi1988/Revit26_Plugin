namespace Revit26_Plugin_Revised.TaperedEpsPattern2D.V002.Revit
{
    /// <summary>How the created detail lines are packaged.</summary>
    public enum GroupingMode
    {
        /// <summary>Everything in one detail group EPS2D_&lt;roofId&gt;_&lt;viewId&gt; (V001 behaviour).</summary>
        SingleGroup,

        /// <summary>
        /// One detail group per layer (…_Grid, …_HipRidge, …_Contours, …_Labels) so each layer can be moved,
        /// rotated or aligned on its own. In "each edge" mode the grid is split per edge (…_Grid_E1, …_Grid_E2 …).
        /// </summary>
        GroupPerLayer,

        /// <summary>Loose detail lines — every line can be aligned, rotated or edited directly. Cannot be replaced automatically.</summary>
        NoGroup
    }
}
