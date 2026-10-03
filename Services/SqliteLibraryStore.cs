using Microsoft.Data.Sqlite;
using Spectrum.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace Spectrum.Services
{
    /// <summary>
    /// §9 "SQLite storage": the second ILibraryStore backend. Palettes, swatches
    /// and version history live in a single local `library.db`; saves are one
    /// atomic transaction (DELETE + reinsert — the whole library is small, so
    /// this is both simpler and safer than row-level syncing).
    ///
    /// First run with no database rows migrates an existing library.json into
    /// the database (the JSON file itself is left untouched as a backup).
    /// </summary>
    public sealed class SqliteLibraryStore : ILibraryStore
    {
        public static bool IsAvailable => true;

        private readonly string _directory;

        public SqliteLibraryStore(string? directory = null)
        {
            _directory = string.IsNullOrWhiteSpace(directory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spectrum")
                : directory;
        }

        public string ProviderName => "SQLite";

        private string DbPath => Path.Combine(_directory, "library.db");

        private SqliteConnection Open()
        {
            Directory.CreateDirectory(_directory);
            var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();
            return connection;
        }

        private static void EnsureSchema(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                CREATE TABLE IF NOT EXISTS LibraryMeta (
                    Key   TEXT PRIMARY KEY,
                    Value TEXT
                );
                CREATE TABLE IF NOT EXISTS Palettes (
                    Id         TEXT PRIMARY KEY,
                    Name       TEXT NOT NULL,
                    IsPinned   INTEGER NOT NULL,
                    CreatedUtc TEXT NOT NULL,
                    UpdatedUtc TEXT NOT NULL,
                    SortOrder  INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS Swatches (
                    PaletteId TEXT NOT NULL,
                    Ord       INTEGER NOT NULL,
                    Name      TEXT NOT NULL,
                    A INTEGER NOT NULL, R INTEGER NOT NULL, G INTEGER NOT NULL, B INTEGER NOT NULL,
                    IsLocked  INTEGER NOT NULL,
                    PRIMARY KEY (PaletteId, Ord)
                );
                CREATE TABLE IF NOT EXISTS Versions (
                    PaletteId TEXT NOT NULL,
                    Ord       INTEGER NOT NULL,
                    SavedUtc  TEXT NOT NULL,
                    PRIMARY KEY (PaletteId, Ord)
                );
                CREATE TABLE IF NOT EXISTS VersionSwatches (
                    PaletteId  TEXT NOT NULL,
                    VersionOrd INTEGER NOT NULL,
                    Ord        INTEGER NOT NULL,
                    Name       TEXT NOT NULL,
                    A INTEGER NOT NULL, R INTEGER NOT NULL, G INTEGER NOT NULL, B INTEGER NOT NULL,
                    IsLocked   INTEGER NOT NULL,
                    PRIMARY KEY (PaletteId, VersionOrd, Ord)
                );
                """;
            cmd.ExecuteNonQuery();
        }

        public Task<LibraryDto?> LoadAsync()
        {
            LibraryDto? loaded = null;

            if (File.Exists(DbPath))
            {
                using var connection = Open();
                EnsureSchema(connection);
                loaded = ReadLibrary(connection);
            }

            // First SQLite run: migrate an existing JSON library, if any.
            if (loaded is null || loaded.Palettes.Count == 0)
            {
                var json = new JsonLibraryStore { DirectoryPath = _directory };
                var fromJson = json.LoadAsync().GetAwaiter().GetResult();
                if (fromJson is not null && fromJson.Palettes.Count > 0)
                {
                    SaveAsync(fromJson).GetAwaiter().GetResult();
                    return Task.FromResult<LibraryDto?>(fromJson);
                }
            }

            return Task.FromResult(loaded);
        }

        private static LibraryDto? ReadLibrary(SqliteConnection connection)
        {
            var library = new LibraryDto();

            using (var meta = connection.CreateCommand())
            {
                meta.CommandText = "SELECT Key, Value FROM LibraryMeta";
                using var reader = meta.ExecuteReader();
                while (reader.Read())
                {
                    if (reader.GetString(0) == "ActivePaletteId")
                        library.ActivePaletteId = reader.GetString(1);
                }
            }

            var palettes = new Dictionary<string, PaletteDto>();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT Id, Name, IsPinned, CreatedUtc, UpdatedUtc FROM Palettes ORDER BY SortOrder";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var palette = new PaletteDto
                    {
                        Id = reader.GetString(0),
                        Name = reader.GetString(1),
                        IsPinned = reader.GetInt64(2) != 0,
                        CreatedUtc = ParseDate(reader.GetString(3)),
                        UpdatedUtc = ParseDate(reader.GetString(4)),
                    };
                    palettes[palette.Id] = palette;
                    library.Palettes.Add(palette);
                }
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT PaletteId, Name, A, R, G, B, IsLocked FROM Swatches ORDER BY PaletteId, Ord";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (!palettes.TryGetValue(reader.GetString(0), out var palette)) continue;
                    palette.Swatches.Add(new PaletteSwatchDto(
                        reader.GetString(1),
                        (byte)reader.GetInt64(2), (byte)reader.GetInt64(3),
                        (byte)reader.GetInt64(4), (byte)reader.GetInt64(5),
                        reader.GetInt64(6) != 0));
                }
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT PaletteId, Ord, SavedUtc FROM Versions ORDER BY PaletteId, Ord";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (!palettes.TryGetValue(reader.GetString(0), out var palette)) continue;
                    palette.Versions.Add(new PaletteVersionDto
                    {
                        SavedUtc = ParseDate(reader.GetString(2)),
                    });
                }
            }

            // Version swatches (keyed by version ordinal within each palette).
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT PaletteId, VersionOrd, Name, A, R, G, B, IsLocked " +
                    "FROM VersionSwatches ORDER BY PaletteId, VersionOrd, Ord";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (!palettes.TryGetValue(reader.GetString(0), out var palette)) continue;
                    var versionIndex = (int)reader.GetInt64(1);
                    if (versionIndex < 0 || versionIndex >= palette.Versions.Count) continue;

                    palette.Versions[versionIndex].Swatches.Add(new PaletteSwatchDto(
                        reader.GetString(2),
                        (byte)reader.GetInt64(3), (byte)reader.GetInt64(4),
                        (byte)reader.GetInt64(5), (byte)reader.GetInt64(6),
                        reader.GetInt64(7) != 0));
                }
            }

            return library.Palettes.Count > 0 ? library : null;
        }

        public Task SaveAsync(LibraryDto library)
        {
            using var connection = Open();
            EnsureSchema(connection);

            using var tx = connection.BeginTransaction();

            Execute(connection, tx,
                "DELETE FROM LibraryMeta; DELETE FROM Palettes; DELETE FROM Swatches; DELETE FROM Versions; DELETE FROM VersionSwatches;");

            using (var meta = connection.CreateCommand())
            {
                meta.Transaction = tx;
                meta.CommandText =
                    "INSERT INTO LibraryMeta (Key, Value) VALUES ($k, $v)";
                meta.Parameters.AddWithValue("$k", "ActivePaletteId");
                meta.Parameters.AddWithValue("$v", library.ActivePaletteId ?? "");
                meta.ExecuteNonQuery();
            }

            var order = 0;
            foreach (var palette in library.Palettes)
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText =
                        "INSERT INTO Palettes (Id, Name, IsPinned, CreatedUtc, UpdatedUtc, SortOrder) " +
                        "VALUES ($id, $name, $pinned, $created, $updated, $order)";
                    cmd.Parameters.AddWithValue("$id", palette.Id);
                    cmd.Parameters.AddWithValue("$name", palette.Name);
                    cmd.Parameters.AddWithValue("$pinned", palette.IsPinned ? 1 : 0);
                    cmd.Parameters.AddWithValue("$created", palette.CreatedUtc.ToString("o", CultureInfo.InvariantCulture));
                    cmd.Parameters.AddWithValue("$updated", palette.UpdatedUtc.ToString("o", CultureInfo.InvariantCulture));
                    cmd.Parameters.AddWithValue("$order", order++);
                    cmd.ExecuteNonQuery();
                }

                var swatchOrder = 0;
                foreach (var s in palette.Swatches)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText =
                        "INSERT INTO Swatches (PaletteId, Ord, Name, A, R, G, B, IsLocked) " +
                        "VALUES ($p, $o, $n, $a, $r, $g, $b, $l)";
                    cmd.Parameters.AddWithValue("$p", palette.Id);
                    cmd.Parameters.AddWithValue("$o", swatchOrder++);
                    cmd.Parameters.AddWithValue("$n", s.Name);
                    cmd.Parameters.AddWithValue("$a", s.A);
                    cmd.Parameters.AddWithValue("$r", s.R);
                    cmd.Parameters.AddWithValue("$g", s.G);
                    cmd.Parameters.AddWithValue("$b", s.B);
                    cmd.Parameters.AddWithValue("$l", s.IsLocked ? 1 : 0);
                    cmd.ExecuteNonQuery();
                }

                for (var v = 0; v < palette.Versions.Count; v++)
                {
                    var version = palette.Versions[v];
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText =
                            "INSERT INTO Versions (PaletteId, Ord, SavedUtc) VALUES ($p, $o, $t)";
                        cmd.Parameters.AddWithValue("$p", palette.Id);
                        cmd.Parameters.AddWithValue("$o", v);
                        cmd.Parameters.AddWithValue("$t", version.SavedUtc.ToString("o", CultureInfo.InvariantCulture));
                        cmd.ExecuteNonQuery();
                    }

                    var vo = 0;
                    foreach (var s in version.Swatches)
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.Transaction = tx;
                        cmd.CommandText =
                            "INSERT INTO VersionSwatches (PaletteId, VersionOrd, Ord, Name, A, R, G, B, IsLocked) " +
                            "VALUES ($p, $v, $o, $n, $a, $r, $g, $b, $l)";
                        cmd.Parameters.AddWithValue("$p", palette.Id);
                        cmd.Parameters.AddWithValue("$v", v);
                        cmd.Parameters.AddWithValue("$o", vo++);
                        cmd.Parameters.AddWithValue("$n", s.Name);
                        cmd.Parameters.AddWithValue("$a", s.A);
                        cmd.Parameters.AddWithValue("$r", s.R);
                        cmd.Parameters.AddWithValue("$g", s.G);
                        cmd.Parameters.AddWithValue("$b", s.B);
                        cmd.Parameters.AddWithValue("$l", s.IsLocked ? 1 : 0);
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            tx.Commit();
            return Task.CompletedTask;
        }

        public Task ResetAsync()
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                var path = DbPath + suffix;
                try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
            }

            return Task.CompletedTask;
        }

        private static void Execute(SqliteConnection connection, SqliteTransaction tx, string sql)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        private static DateTime ParseDate(string value) =>
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
                ? dt
                : DateTime.UtcNow;
    }
}
