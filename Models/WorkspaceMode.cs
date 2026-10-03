namespace Spectrum.Models
{
    /// <summary>
    /// The app's top-level workspaces (§7 desktop UX). The app bar switches
    /// between them and each one shows only the panels its task needs, instead
    /// of putting every tool on screen at once.
    /// </summary>
    public enum WorkspaceMode
    {
        /// <summary>Build the palette: harmony rule, eyedroppers, sliders, board.</summary>
        Studio,

        /// <summary>See the palette: layout, live mockup, variants, side-by-side compare.</summary>
        Preview,

        /// <summary>Measure the palette: conversions, contrast, color-vision simulation.</summary>
        Analyze,

        /// <summary>Move files in and out: export presets, PNG sheet, imports.</summary>
        Export,
    }
}
