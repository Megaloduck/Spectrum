using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spectrum.Models;
using Spectrum.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Spectrum.ViewModels
{
    public enum LibraryFilter
    {
        All,
        Pinned,
        Recent,
    }

    /// <summary>
    /// Sidebar palette list (§1): create / rename (inline) / duplicate / delete,
    /// pin, search, filter (all/pinned/recent), version history + restore, and
    /// whole-library import/export (§9). Owns the PaletteItemViewModel rows and
    /// surfaces the active palette selection to the parent ViewModel.
    /// </summary>
    public partial class PaletteLibraryViewModel : ViewModelBase
    {
        public ObservableCollection<PaletteItemViewModel> Palettes { get; } = new();
        public ObservableCollection<PaletteItemViewModel> VisiblePalettes { get; } = new();

        /// <summary>Palettes currently open as tabs above the board (§7).</summary>
        public ObservableCollection<PaletteItemViewModel> OpenTabs { get; } = new();

        public Array Filters => Enum.GetValues(typeof(LibraryFilter));

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private LibraryFilter _selectedFilter = LibraryFilter.All;

        [ObservableProperty]
        private PaletteItemViewModel? _selectedPalette;

        [ObservableProperty]
        private string _saveStateLabel = "All changes saved";

        /// <summary>Fired when the user selects a different palette in the sidebar.</summary>
        public event Action<PaletteDto?>? ActivePaletteChanged;

        public PaletteLibraryViewModel()
        {
            PaletteLibraryService.Initialize();

            foreach (var dto in PaletteLibraryService.Library.Palettes)
                Palettes.Add(CreateItem(dto));

            PaletteLibraryService.Edited += OnEdited;
            PaletteLibraryService.Saved += OnSaved;

            RefreshVisible();
        }

        /// <summary>Detaches service hooks — called by the window when it closes.</summary>
        public void Detach()
        {
            PaletteLibraryService.Edited -= OnEdited;
            PaletteLibraryService.Saved -= OnSaved;
        }

        public PaletteDto? ActivePalette => SelectedPalette?.Dto;

        // ---------------- Commands ----------------

        [RelayCommand]
        private void NewPalette()
        {
            var palette = PaletteLibraryService.AddPalette(
                NextName("Palette"),
                SeedSwatches(DefaultPaletteSize));

            var item = CreateItem(palette);
            Palettes.Insert(0, item);
            RefreshVisible();
            SelectedPalette = item;
            StatusMessage($"Created \"{palette.Name}\".");
        }

        [RelayCommand]
        private void DuplicatePalette(PaletteItemViewModel? source)
        {
            source ??= SelectedPalette;
            if (source is null) return;

            var copy = PaletteLibraryService.AddPalette(
                NextName(source.Dto.Name + " copy"),
                source.Dto.Swatches.Select(s => new PaletteSwatchDto(s.Name, s.A, s.R, s.G, s.B, s.IsLocked)));

            var item = CreateItem(copy);
            Palettes.Insert(Palettes.IndexOf(source) + 1, item);
            RefreshVisible();
            SelectedPalette = item;
            StatusMessage($"Duplicated \"{source.Dto.Name}\".");
        }

        [RelayCommand]
        private void DeletePalette(PaletteItemViewModel? target)
        {
            target ??= SelectedPalette;
            if (target is null) return;

            var wasSelected = ReferenceEquals(target, SelectedPalette);
            var name = target.Dto.Name;

            Palettes.Remove(target);
            PaletteLibraryService.DeletePalette(target.Dto);
            RefreshVisible();

            if (wasSelected)
                SelectedPalette = VisiblePalettes.FirstOrDefault();

            if (Palettes.Count == 0)
            {
                ActivePaletteChanged?.Invoke(null);
            }

            PaletteLibraryService.MarkDirty();
            StatusMessage($"Deleted \"{name}\".");
        }

        [RelayCommand]
        private void TogglePin(PaletteItemViewModel? item)
        {
            item ??= SelectedPalette;
            if (item is null) return;

            item.IsPinned = !item.IsPinned;
            RefreshVisible();
            StatusMessage(item.IsPinned ? $"Pinned \"{item.Name}\"." : $"Unpinned \"{item.Name}\".");
        }

        [RelayCommand]
        private void RestoreVersion(PaletteVersionDto? version)
        {
            if (version is null) return;

            var palette = PaletteLibraryService.Library.Palettes.FirstOrDefault(p => p.Versions.Contains(version));
            if (palette is null) return;

            // Restore the snapshot into the DTO; the snapshot stays in history
            // so it can be restored again later.
            palette.Swatches = version.Swatches
                .Select(s => new PaletteSwatchDto(s.Name, s.A, s.R, s.G, s.B, s.IsLocked))
                .ToList();

            var item = Palettes.FirstOrDefault(p => ReferenceEquals(p.Dto, palette));
            item?.Refresh();
            PaletteLibraryService.MarkDirty(palette);

            // Reload the board if the restored palette is the one being edited.
            if (ReferenceEquals(ActivePalette, palette))
                ActivePaletteChanged?.Invoke(palette);

            StatusMessage($"Restored a saved version of \"{palette.Name}\".");
        }

        [RelayCommand]
        private async Task ImportPaletteFileAsync()
        {
            var path = await PaletteFileService.PickOpenFileAsync(
                "Palette files (*.css, *.gpl, *.ase, *.aco)",
                new[] { "*.css", "*.gpl", "*.ase", "*.aco" },
                "Import palette from file");
            if (string.IsNullOrEmpty(path)) return;

            var swatches = await PaletteImportService.ImportFileAsync(path);
            if (swatches is null || swatches.Count == 0)
            {
                StatusMessage("No usable colors found in that file (supported: CSS, GPL, ASE, ACO).");
                return;
            }

            var palette = PaletteLibraryService.AddPalette(
                NextName(System.IO.Path.GetFileNameWithoutExtension(path)), swatches);

            var item = CreateItem(palette);
            Palettes.Insert(0, item);
            RefreshVisible();
            SelectedPalette = item;
            StatusMessage($"Imported {swatches.Count} color{(swatches.Count == 1 ? "" : "s")} into \"{palette.Name}\".");
        }

        [RelayCommand]
        private async Task ExportLibraryAsync()
        {
            var path = await PaletteFileService.PickSaveFileAsync("spectrum-library", "json", "Spectrum Library (*.json)");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                PaletteLibraryService.Flush();
                await System.IO.File.WriteAllTextAsync(
                    path,
                    System.Text.Json.JsonSerializer.Serialize(
                        PaletteLibraryService.Library,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                StatusMessage("Library exported.");
            }
            catch (Exception ex)
            {
                StatusMessage($"Couldn't export library: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ImportLibraryAsync()
        {
            var path = await PaletteFileService.PickOpenFileAsync("Spectrum Library (*.json)", "*.json");
            if (string.IsNullOrEmpty(path)) return;

            LibraryDto? imported;
            try
            {
                imported = System.Text.Json.JsonSerializer.Deserialize<LibraryDto>(
                    await System.IO.File.ReadAllTextAsync(path));
            }
            catch (Exception ex)
            {
                StatusMessage($"Couldn't read that file: {ex.Message}");
                return;
            }

            if (imported?.Palettes is null || imported.Palettes.Count == 0)
            {
                StatusMessage("That file didn't contain a usable library.");
                return;
            }

            // Merge: imported palettes come in first, ids de-duplicated.
            var existingIds = new HashSet<string>(PaletteLibraryService.Library.Palettes.Select(p => p.Id));
            var added = 0;
            foreach (var palette in imported.Palettes)
            {
                if (existingIds.Contains(palette.Id))
                    palette.Id = Guid.NewGuid().ToString("N");
                PaletteLibraryService.Library.Palettes.Add(palette);

                var item = CreateItem(palette);
                Palettes.Add(item);
                added++;
            }

            PaletteLibraryService.MarkDirty();
            RefreshVisible();
            StatusMessage($"Imported {added} palette{(added == 1 ? "" : "s")}.");
        }

        /// <summary>Registers a freshly stored palette (variant generator, imports) and selects it.</summary>
        public void AddAndSelect(PaletteDto dto)
        {
            var item = CreateItem(dto);
            Palettes.Insert(0, item);
            RefreshVisible();
            SelectedPalette = item;
        }

        /// <summary>
        /// Rebuilds every row after the storage backend changed (Settings →
        /// storage location / provider / reset) and restores the selection.
        /// </summary>
        public void ReloadFromService()
        {
            var previousId = SelectedPalette?.Dto.Id ?? PaletteLibraryService.Library.ActivePaletteId;

            _refreshing = true;
            try
            {
                Palettes.Clear();
                OpenTabs.Clear();
                foreach (var dto in PaletteLibraryService.Library.Palettes)
                    Palettes.Add(CreateItem(dto));
            }
            finally
            {
                _refreshing = false;
            }

            RefreshVisible();

            var target = Palettes.FirstOrDefault(p => p.Dto.Id == previousId)
                         ?? VisiblePalettes.FirstOrDefault();
            SelectedPalette = target; // fires ActivePaletteChanged → board reloads
        }

        // ---------------- Filtering / selection ----------------

        partial void OnSearchTextChanged(string value) => RefreshVisible();

        partial void OnSelectedFilterChanged(LibraryFilter value) => RefreshVisible();

        partial void OnSelectedPaletteChanged(PaletteItemViewModel? value)
        {
            // While VisiblePalettes is being rebuilt the ListBox may write null
            // back through the binding — that must not change the real selection
            // (or clear the board), so those writes are suppressed and undone.
            if (_refreshing) return;

            // Tabs: selecting a palette opens it as a tab and marks the active one.
            foreach (var item in OpenTabs)
                item.IsSelected = ReferenceEquals(item, value);
            if (value is not null && !OpenTabs.Contains(value))
            {
                value.IsSelected = true;
                OpenTabs.Add(value);
            }

            PaletteLibraryService.Library.ActivePaletteId = value?.Dto.Id;
            PaletteLibraryService.MarkMetaDirty();
            ActivePaletteChanged?.Invoke(value?.Dto);
        }

        private bool _refreshing;

        [RelayCommand]
        private void CloseTab(PaletteItemViewModel? tab)
        {
            if (tab is null) return;

            var wasActive = ReferenceEquals(tab, SelectedPalette);
            var index = OpenTabs.IndexOf(tab);
            OpenTabs.Remove(tab);
            tab.IsSelected = false;

            if (wasActive)
            {
                var next = OpenTabs.Count > 0
                    ? OpenTabs[System.Math.Clamp(index, 0, OpenTabs.Count - 1)]
                    : null;
                SelectedPalette = next;
            }
        }

        public void RefreshVisible()
        {
            var selected = SelectedPalette;
            _refreshing = true;
            try
            {
                RebuildVisible(selected);
            }
            finally
            {
                // Restore the selection the view may have nulled out mid-rebuild.
                if (selected is not null && !ReferenceEquals(SelectedPalette, selected))
                    SelectedPalette = selected;
                _refreshing = false;
            }
        }

        private void RebuildVisible(PaletteItemViewModel? keepSelected)
        {
            var query = SearchText.Trim();
            IEnumerable<PaletteItemViewModel> items = Palettes;

            items = SelectedFilter switch
            {
                LibraryFilter.Pinned => items.Where(i => i.Dto.IsPinned),
                LibraryFilter.Recent => items.OrderByDescending(i => i.Dto.UpdatedUtc).Take(10),
                _ => items.OrderBy(i => !i.Dto.IsPinned)   // pinned float to the top, stable otherwise
                          .ThenByDescending(i => i.Dto.UpdatedUtc == default ? DateTime.MinValue : i.Dto.UpdatedUtc),
            };

            if (query.Length > 0)
            {
                items = items.Where(i =>
                    i.Dto.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    i.Dto.Swatches.Any(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)));
            }

            VisiblePalettes.Clear();
            foreach (var item in items)
                VisiblePalettes.Add(item);
        }

        /// <summary>Called by the parent ViewModel after it syncs swatch edits into the DTO.</summary>
        public void RefreshPreview(PaletteDto palette)
        {
            var item = Palettes.FirstOrDefault(p => ReferenceEquals(p.Dto, palette));
            item?.Refresh();
        }

        /// <summary>Creates and activates the first palette for a brand-new library.</summary>
        public void CreateDefaultPalette()
        {
            NewPaletteCommand.Execute(null);
        }

        public void SelectFirst()
        {
            RefreshVisible();
            SelectedPalette = VisiblePalettes.FirstOrDefault();
        }

        public void SelectPalette(PaletteDto palette)
        {
            RefreshVisible();
            var item = VisiblePalettes.FirstOrDefault(p => ReferenceEquals(p.Dto, palette))
                       ?? Palettes.FirstOrDefault(p => ReferenceEquals(p.Dto, palette));
            if (item is not null) SelectedPalette = item;
        }

        // ---------------- Internals ----------------

        private PaletteItemViewModel CreateItem(PaletteDto dto)
        {
            return new PaletteItemViewModel(dto)
            {
                Changed = () =>
                {
                    PaletteLibraryService.MarkDirty(dto);
                    RefreshPreview(dto);
                    RefreshVisible();
                },
            };
        }

        private void OnEdited() => SaveStateLabel = "Unsaved changes…";

        private void OnSaved() => SaveStateLabel =
            PaletteLibraryService.LastSaveError is { } error
                ? $"Autosave failed: {error}"
                : $"All changes saved · {PaletteLibraryService.Store.ProviderName}";

        private static List<PaletteSwatchDto> SeedSwatches(int count)
        {
            var rng = Random.Shared;
            var swatches = new List<PaletteSwatchDto>();
            for (var i = 0; i < count; i++)
            {
                var c = ColorHarmonyService.RandomPleasant(rng);
                var name = ColorNamingService.GetClosestName(c);
                swatches.Add(new PaletteSwatchDto(name, c.A, c.R, c.G, c.B, false));
            }
            return swatches;
        }

        private const int DefaultPaletteSize = 5;

        private string NextName(string baseName)
        {
            var names = new HashSet<string>(
                Palettes.Select(p => p.Dto.Name),
                StringComparer.OrdinalIgnoreCase);

            if (!names.Contains(baseName)) return baseName;

            for (var i = 2; ; i++)
            {
                var candidate = $"{baseName} {i}";
                if (!names.Contains(candidate)) return candidate;
            }
        }

        private static void StatusMessage(string message)
        {
            if (App.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow?.DataContext is MainWindowViewModel main)
            {
                main.StatusMessage = message;
            }
        }
    }
}
