using System;
using System.Collections.Generic;

namespace Spectrum.Models
{
    /// <summary>
    /// One recorded snapshot of a palette's swatches, used for the per-palette
    /// version history (restore an earlier state of a palette).
    /// </summary>
    public class PaletteVersionDto
    {
        public DateTime SavedUtc { get; set; } = DateTime.UtcNow;
        public List<PaletteSwatchDto> Swatches { get; set; } = new();
    }

    /// <summary>
    /// A palette as it lives on disk: metadata + swatches + version snapshots.
    /// Kept separate from ColorSwatch (Avalonia types + ICommand references
    /// must not be serialized).
    /// </summary>
    public class PaletteDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "New Palette";
        public bool IsPinned { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
        public List<PaletteSwatchDto> Swatches { get; set; } = new();
        public List<PaletteVersionDto> Versions { get; set; } = new();
    }

    /// <summary>
    /// The whole library document — everything that gets persisted in one file
    /// (or one SQLite database, see SqliteLibraryStore).
    /// </summary>
    public class LibraryDto
    {
        public int SchemaVersion { get; set; } = 1;
        public string? ActivePaletteId { get; set; }
        public List<PaletteDto> Palettes { get; set; } = new();
    }
}
