using Spectrum.Models;
using System.Threading.Tasks;

namespace Spectrum.Services
{
    /// <summary>
    /// Storage backend for the palette library. The library service talks to
    /// this interface only, so the JSON file store (v1 default) can be swapped
    /// for the SQLite store without touching any caller.
    /// </summary>
    public interface ILibraryStore
    {
        /// <summary>Human-readable backend name, shown in Settings and the status bar.</summary>
        string ProviderName { get; }

        /// <summary>Loads the whole library, or null when nothing has been saved yet.</summary>
        Task<LibraryDto?> LoadAsync();

        /// <summary>Persists the whole library atomically (no partial writes visible).</summary>
        Task SaveAsync(LibraryDto library);

        /// <summary>Deletes all persisted library data (Settings → Reset / clear data).</summary>
        Task ResetAsync();
    }
}
