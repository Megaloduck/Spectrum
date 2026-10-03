namespace Spectrum.Models
{
    /// <summary>Target format for "copy color" — also the §10 setting "default color format on copy".</summary>
    public enum CopyFormat
    {
        Hex,
        Rgb,
        Rgba,
        CssRgb,
        Hsl,
        CssHsl,
        Hsv,
        Lab,
        Oklch,
    }
}
