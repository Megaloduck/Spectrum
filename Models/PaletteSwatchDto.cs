namespace Spectrum.Models
{
    /// <summary>
    /// Plain serializable shape of a swatch, used only when saving/loading
    /// palettes to/from disk. Kept separate from ColorSwatch (which carries
    /// Avalonia types and ICommand references that shouldn't be serialized).
    /// </summary>
    public record PaletteSwatchDto(string Name, byte A, byte R, byte G, byte B, bool IsLocked);
}
