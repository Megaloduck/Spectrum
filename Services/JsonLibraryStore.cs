using Spectrum.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Spectrum.Services
{
    /// <summary>
    /// JSON file backend for the palette library (the v1 default from §9).
    ///
    /// Durability features:
    ///  - Atomic saves: everything is first written to a .tmp journal file and
    ///    then moved over the real file, so a crash mid-write can never corrupt
    ///    the library.
    ///  - Crash recovery: if a *valid* journal file survives a crash (write
    ///    finished, move didn't), it is adopted on next load because it is
    ///    newer than the real file.
    ///  - Rotating backups: before each save the previous library file is copied
    ///    into a backups/ folder; the newest 5 are kept.
    ///  - Corruption recovery: an unparseable library file is set aside (never
    ///    deleted) and the newest parseable backup is restored instead.
    /// </summary>
    public sealed class JsonLibraryStore : ILibraryStore
    {
        private const int MaxBackups = 5;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>Directory holding library.json. Overridable by the Storage location setting.</summary>
        public string DirectoryPath { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spectrum");

        public string ProviderName => "JSON";

        private string LibraryPath => Path.Combine(DirectoryPath, "library.json");
        private string JournalPath => Path.Combine(DirectoryPath, "library.json.tmp");
        private string BackupsDirectory => Path.Combine(DirectoryPath, "backups");

        public Task<LibraryDto?> LoadAsync()
        {
            // 1. Crash recovery: a valid journal newer than the library means we
            //    crashed between writing the new state and moving it into place.
            var fromJournal = TryRead(JournalPath);
            var regular = TryRead(LibraryPath);
            if (fromJournal is not null &&
                (regular is null || File.GetLastWriteTimeUtc(JournalPath) > File.GetLastWriteTimeUtc(LibraryPath)))
            {
                TryDelete(JournalPath);
                return Task.FromResult<LibraryDto?>(fromJournal);
            }

            TryDelete(JournalPath);

            // 2. Normal path.
            if (regular is not null)
                return Task.FromResult<LibraryDto?>(regular);

            // 3. Corruption recovery: library.json exists but won't parse — set it
            //    aside (so nothing is lost) and try backups newest-first.
            if (File.Exists(LibraryPath))
            {
                var quarantine = Path.Combine(DirectoryPath, $"library.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                try { File.Move(LibraryPath, quarantine); } catch { /* best effort */ }
            }

            foreach (var backup in GetBackupsNewestFirst())
            {
                var restored = TryRead(backup);
                if (restored is not null)
                    return Task.FromResult<LibraryDto?>(restored);
            }

            return Task.FromResult<LibraryDto?>(null);
        }

        public Task SaveAsync(LibraryDto library)
        {
            Directory.CreateDirectory(DirectoryPath);
            var json = JsonSerializer.Serialize(library, SerializerOptions);

            // Back up the current library before replacing it.
            if (File.Exists(LibraryPath))
            {
                Directory.CreateDirectory(BackupsDirectory);
                var backupPath = Path.Combine(BackupsDirectory, $"library.backup-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                try { File.Copy(LibraryPath, backupPath, overwrite: true); } catch { /* disk full etc. — non-fatal */ }
                PruneBackups();
            }

            // Journal then atomic move: readers only ever see a complete file.
            File.WriteAllText(JournalPath, json);
            File.Move(JournalPath, LibraryPath, overwrite: true);

            return Task.CompletedTask;
        }

        public Task ResetAsync()
        {
            TryDelete(JournalPath);
            TryDelete(LibraryPath);
            try
            {
                if (Directory.Exists(BackupsDirectory))
                    Directory.Delete(BackupsDirectory, recursive: true);
            }
            catch { /* best effort */ }

            return Task.CompletedTask;
        }

        private LibraryDto? TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var library = JsonSerializer.Deserialize<LibraryDto>(File.ReadAllText(path), SerializerOptions);
                if (library?.Palettes is null) return null;

                // Defensive: drop malformed entries rather than crashing on them.
                library.Palettes.RemoveAll(p => p is null || p.Swatches is null);
                foreach (var palette in library.Palettes)
                {
                    palette.Versions ??= new();
                    palette.Versions.RemoveAll(v => v is null || v.Swatches is null);
                }

                return library;
            }
            catch (JsonException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        private List<string> GetBackupsNewestFirst()
        {
            try
            {
                if (!Directory.Exists(BackupsDirectory)) return new List<string>();
                return new DirectoryInfo(BackupsDirectory)
                    .GetFiles("library.backup-*.json")
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Select(f => f.FullName)
                    .ToList();
            }
            catch (IOException)
            {
                return new List<string>();
            }
        }

        private void PruneBackups()
        {
            var extras = GetBackupsNewestFirst().Skip(MaxBackups);
            foreach (var path in extras)
            {
                try { File.Delete(path); } catch { /* best effort */ }
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
        }
    }
}
