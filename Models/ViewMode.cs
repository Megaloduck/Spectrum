namespace Spectrum.Models
{
    /// <summary>§8 "Grid / list / compact view modes" (plus the classic single-row layout).</summary>
    public enum ViewMode
    {
        /// <summary>Coolors-style one horizontal strip of full-height cards.</summary>
        Row,

        /// <summary>Wrapped grid of fixed-size cards.</summary>
        Grid,

        /// <summary>One wide row per swatch with all its data.</summary>
        List,

        /// <summary>Small color chips, many at once.</summary>
        Compact,
    }
}
