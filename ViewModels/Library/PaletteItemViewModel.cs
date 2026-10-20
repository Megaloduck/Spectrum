using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Spectrum.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Spectrum.ViewModels
{
    /// <summary>
    /// One palette row in the sidebar: exposes the DTO's data as bindable
    /// properties (plus a hard-stop gradient preview strip of its colors) and
    /// calls back into the library ViewModel when the user edits it inline.
    /// </summary>
    public partial class PaletteItemViewModel : ViewModelBase
    {
        public PaletteDto Dto { get; }

        /// <summary>Raised after an inline edit (rename, pin) so the owner can autosave.</summary>
        public Action? Changed { get; set; }

        [ObservableProperty]
        private bool _isSelected;

        public PaletteItemViewModel(PaletteDto dto)
        {
            Dto = dto;
        }

        /// <summary>True while the user is editing the name inline (TextBox shown in the row).</summary>
        [ObservableProperty]
        private bool _isRenaming;

        /// <summary>The name the palette had when the editor opened — Escape puts it back.</summary>
        private string _nameBeforeRename = string.Empty;

        /// <summary>Raised after an inline rename is committed or cancelled, so the owner
        /// can re-apply search / sort. The sidebar list is deliberately *not* rebuilt
        /// while <see cref="IsRenaming"/> is true: clearing and re-adding the rows would
        /// throw the live TextBox away (taking focus with it) on every keystroke.</summary>
        public Action? RenameEnded { get; set; }

        partial void OnIsRenamingChanged(bool value)
        {
            if (value) _nameBeforeRename = Dto.Name;
        }

        public string Name
        {
            get => Dto.Name;
            set
            {
                var trimmed = string.IsNullOrWhiteSpace(value) ? "Untitled palette" : value;
                if (Dto.Name == trimmed) return;
                Dto.Name = trimmed;
                OnPropertyChanged();
                Changed?.Invoke();
            }
        }

        /// <summary>Commits the current name and exits inline rename mode.</summary>
        public void EndRename()
        {
            if (!IsRenaming) return; // Enter already ended it — the TextBox losing focus second must not re-run the commit.
            IsRenaming = false;
            OnPropertyChanged(nameof(Name));
            RenameEnded?.Invoke();
        }

        /// <summary>Exits inline rename mode and restores the name the palette had
        /// when the editor opened (Escape).</summary>
        public void CancelRename()
        {
            if (!IsRenaming) return;
            Name = _nameBeforeRename;
            EndRename();
        }

        public bool IsPinned
        {
            get => Dto.IsPinned;
            set
            {
                if (Dto.IsPinned == value) return;
                Dto.IsPinned = value;
                OnPropertyChanged();
                Changed?.Invoke();
            }
        }

        public string CountLabel =>
            Dto.Swatches.Count == 1 ? "1 color" : $"{Dto.Swatches.Count} colors";

        public string UpdatedLabel => RelativeTime(Dto.UpdatedUtc);

        public string VersionsLabel =>
            Dto.Versions.Count == 0 ? "No saved versions yet" : $"{Dto.Versions.Count} saved version(s)";

        /// <summary>Newest few snapshots — enough to restore from without turning
        /// the library flyout into an endless history log.</summary>
        public IEnumerable<PaletteVersionDto> RecentVersions =>
            Dto.Versions.OrderByDescending(v => v.SavedUtc).Take(5);

        /// <summary>
        /// Hard-stop horizontal gradient of the first colors — a miniature of
        /// the palette used as the sidebar thumbnail (no extra controls needed).
        /// </summary>
        public IBrush? PreviewBrush
        {
            get
            {
                if (Dto.Swatches.Count == 0) return null;

                var colors = Dto.Swatches
                    .Take(6)
                    .Select(s => Color.FromRgb(s.R, s.G, s.B))
                    .ToList();

                var brush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                };

                for (var i = 0; i < colors.Count; i++)
                {
                    var start = (double)i / colors.Count;
                    var end = (double)(i + 1) / colors.Count;
                    brush.GradientStops.Add(new GradientStop(colors[i], start));
                    brush.GradientStops.Add(new GradientStop(colors[i], end));
                }

                return brush;
            }
        }

        // ---- Commands wired by PaletteLibraryViewModel (same pattern as
        // ---- ColorSwatch): the rail's context flyout binds straight to the
        // ---- item, so a flyout never needs to reach out to the parent VM.
        public ICommand? RenameCommand { get; set; }
        public ICommand? DuplicateCommand { get; set; }
        public ICommand? DeleteCommand { get; set; }
        public ICommand? TogglePinCommand { get; set; }

        public void Refresh()
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(IsPinned));
            OnPropertyChanged(nameof(CountLabel));
            OnPropertyChanged(nameof(UpdatedLabel));
            OnPropertyChanged(nameof(VersionsLabel));
            OnPropertyChanged(nameof(RecentVersions));
            OnPropertyChanged(nameof(PreviewBrush));
        }

        private static string RelativeTime(DateTime utc)
        {
            var delta = DateTime.UtcNow - utc;
            if (delta < TimeSpan.Zero) return "just now";
            if (delta.TotalMinutes < 1) return "just now";
            if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
            if (delta.TotalHours < 24) return $"{(int)delta.TotalHours}h ago";
            if (delta.TotalDays < 7) return $"{(int)delta.TotalDays}d ago";
            return utc.ToLocalTime().ToString("d");
        }
    }
}