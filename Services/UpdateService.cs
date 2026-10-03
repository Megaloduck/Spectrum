using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Spectrum.Services
{
    public sealed record UpdateInfo(string Version, string? Notes, string? Url);

    /// <summary>
    /// §11 "Auto-update mechanism", shaped by §11 "Offline-first": Spectrum
    /// makes no network calls in the background, ever. A check runs only when
    /// the user explicitly clicks "Check for updates" in Settings, it fetches a
    /// tiny static manifest, and it only ever *tells* you about a newer
    /// version — downloads/installs are left to the user via the release link.
    ///
    /// The manifest URL is a placeholder until the project has a real release
    /// channel (see README); a failed/unreachable check degrades to a friendly
    /// "you're offline / no release channel configured" message.
    /// </summary>
    public static class UpdateService
    {
        public const string ManifestUrl =
            "https://raw.githubusercontent.com/spectrum-app/spectrum/main/latest.json";

        public static string CurrentVersion
        {
            get
            {
                var version = typeof(UpdateService).Assembly.GetName().Version;
                return version is null ? "1.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
            }
        }

        /// <summary>Explicit, user-triggered only. Returns null when no update info is available.</summary>
        public static async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

            var json = await http.GetStringAsync(ManifestUrl);
            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;
            var version = root.TryGetProperty("version", out var v) ? v.GetString() ?? "0.0.0" : "0.0.0";
            var notes = root.TryGetProperty("notes", out var n) ? n.GetString() : null;
            var url = root.TryGetProperty("url", out var u) ? u.GetString() : null;

            return IsNewer(version, CurrentVersion) ? new UpdateInfo(version, notes, url) : null;
        }

        private static bool IsNewer(string candidate, string current)
        {
            if (Version.TryParse(candidate, out var a) && Version.TryParse(current, out var b))
                return a > b;

            return !string.Equals(candidate, current, StringComparison.OrdinalIgnoreCase);
        }
    }
}
