using Avalonia.Threading;
using Spectrum.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Spectrum.Services
{
    /// <summary>
    /// Owns the in-memory palette library and keeps the storage backend in
    /// sync. Implements autosave with a debounced dirty state (§1/§9):
    ///
    ///   edit → MarkDirty(palette) → 750ms of silence → Flush() → store save
    ///
    /// Flush also stamps UpdatedUtc and records a version-history snapshot for
    /// every dirty palette (at most one snapshot per 30s per palette, capped at
    /// 20 snapshots — so scrubbing a color slider doesn't flood the history).
    /// Everything runs on the UI thread; the file is small enough that a
    /// synchronous atomic write is cheaper (and safer) than juggling threads.
    /// </summary>
    public static class PaletteLibraryService
    {
        public const int AutosaveDelayMs = 750;
        private const int VersionIntervalSeconds = 30;
        private const int MaxVersionsPerPalette = 20;

        private static readonly DispatcherTimer Timer;
        private static readonly HashSet<string> DirtyPaletteIds = new();
        private static bool _metaDirty;
        private static bool _initialized;

        /// <summary>The storage backend. Swapped by Settings (JSON ↔ SQLite).</summary>
        public static ILibraryStore Store { get; set; } = new JsonLibraryStore();

        public static LibraryDto Library { get; private set; } = new();

        /// <summary>The last deleted palette, stashed for Ctrl+Z undo in the library.</summary>
        public static PaletteDto? LastDeletedPalette { get; private set; }

        /// <summary>Whether the next DeletePalette can be undone (tombstone available).</summary>
        public static bool CanUndoLastDelete => LastDeletedPalette is not null;

        /// <summary>Raised after a successful autosave (status bar, save pill).</summary>
        public static event Action? Saved;

        /// <summary>Raised whenever a palette is marked dirty (unsaved-changes pill).</summary>
        public static event Action? Edited;

        public static bool IsDirty => DirtyPaletteIds.Count > 0;
        public static string? LastSaveError { get; private set; }

        static PaletteLibraryService()
        {
            Timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(AutosaveDelayMs) };
            Timer.Tick += (_, _) => Flush();
        }

        public static void Initialize(bool force = false)
        {
            if (_initialized && !force) return;
            _initialized = true;

            Library = Store.LoadAsync().GetAwaiter().GetResult() ?? new LibraryDto();
            Library.Palettes ??= new();
            LastSaveError = null;
        }

        /// <summary>Persists pending changes to the current store, then switches to another
        /// one (storage location / provider changes in Settings, §10) and reloads.</summary>
        public static void ChangeStore(ILibraryStore store)
        {
            Flush();
            DirtyPaletteIds.Clear();
            _metaDirty = false;
            Store = store;
            Initialize(force: true);
        }

        public static PaletteDto? FindPalette(string? id) =>
            id is null ? null : Library.Palettes.FirstOrDefault(p => p.Id == id);

        public static PaletteDto AddPalette(string name, IEnumerable<PaletteSwatchDto>? swatches = null)
        {
            var palette = new PaletteDto
            {
                Name = name,
                Swatches = swatches?.ToList() ?? new List<PaletteSwatchDto>(),
            };
            Library.Palettes.Insert(0, palette);
            MarkDirty(palette);
            return palette;
        }

        public static void DeletePalette(PaletteDto palette)
        {
            // Stash a tombstone so the library-level delete can be undone.
            LastDeletedPalette = new PaletteDto
            {
                Id = palette.Id,
                Name = palette.Name,
                Swatches = palette.Swatches.Select(s => new PaletteSwatchDto(
                    s.Name, s.A, s.R, s.G, s.B, s.IsLocked)).ToList(),
                CreatedUtc = palette.CreatedUtc,
                UpdatedUtc = palette.UpdatedUtc,
            };

            Library.Palettes.Remove(palette);
            if (Library.ActivePaletteId == palette.Id)                    Library.ActivePaletteId = Library.Palettes.FirstOrDefault()?.Id;

            MarkDirty();
        }

        public static void UndoLastDelete()
        {
            var deleted = LastDeletedPalette;
            if (deleted is null) return;

            // Re-add the deleted palette as a brand-new item so its Id and its
            // position match the original row state, then drop the tombstone.
            Library.Palettes.Add(deleted);

            if (Library.ActivePaletteId is null)
            {
                Library.ActivePaletteId = deleted.Id;
            }

            LastDeletedPalette = null;
            MarkDirty();
        }

        /// <summary>Queues a persist (debounced) and notifies listeners of the dirty state.</summary>
        public static void MarkDirty(PaletteDto? palette = null)
        {
            if (palette is not null)
                DirtyPaletteIds.Add(palette.Id);

            Timer.Stop();
            Timer.Start();
            Edited?.Invoke();
        }

        /// <summary>
        /// Library-level metadata changed (e.g. the active palette selection):
        /// needs a persist, but must not bump any palette's UpdatedUtc or record
        /// a version snapshot.
        /// </summary>
        public static void MarkMetaDirty()
        {
            _metaDirty = true;
            Timer.Stop();
            Timer.Start();
            Edited?.Invoke();
        }

        /// <summary>Persists immediately; called by the debounce timer and on app exit.</summary>
        public static void Flush()
        {
            Timer.Stop();
            if (DirtyPaletteIds.Count == 0 && !_metaDirty) return;

            try
            {
                var now = DateTime.UtcNow;
                foreach (var id in DirtyPaletteIds.ToList())
                {
                    var palette = FindPalette(id);
                    if (palette is null) continue;
                    palette.UpdatedUtc = now;
                    RecordVersion(palette, now);
                }

                Store.SaveAsync(Library).GetAwaiter().GetResult();
                DirtyPaletteIds.Clear();
                _metaDirty = false;
                LastSaveError = null;
            }
            catch (Exception ex)
            {
                LastSaveError = ex.Message;
            }

            Saved?.Invoke();
        }

        /// <summary>Settings → Reset / clear data: wipe storage and start fresh in memory.</summary>
        public static void Reset()
        {
            Timer.Stop();
            DirtyPaletteIds.Clear();
            _metaDirty = false;
            Store.ResetAsync().GetAwaiter().GetResult();
            Library = new LibraryDto();
            _initialized = true;
        }

        /// <summary>
        /// Keeps a bounded per-palette version history: one snapshot per Flush,
        /// but no more often than every 30 seconds for the same palette.
        /// </summary>
        private static void RecordVersion(PaletteDto palette, DateTime now)
        {
            var last = palette.Versions.Count > 0 ? palette.Versions[^1].SavedUtc : DateTime.MinValue;
            if (now - last < TimeSpan.FromSeconds(VersionIntervalSeconds)) return;

            palette.Versions.Add(new PaletteVersionDto
            {
                SavedUtc = now,
                Swatches = palette.Swatches
                    .Select(s => new PaletteSwatchDto(s.Name, s.A, s.R, s.G, s.B, s.IsLocked))
                    .ToList(),
            });

            while (palette.Versions.Count > MaxVersionsPerPalette)
                palette.Versions.RemoveAt(0);
        }
    }
}
