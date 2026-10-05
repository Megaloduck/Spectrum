using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Spectrum.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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

        public void Refresh()
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(IsPinned));
            OnPropertyChanged(nameof(CountLabel));
            OnPropertyChanged(nameof(UpdatedLabel));
            OnPropertyChanged(nameof(VersionsLabel));
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

    /// <summary>A row in the version-history flyout of one palette.</summary>
    public class PaletteVersionItemViewModel : ViewModelBase
    {
        public PaletteVersionDto Version { get; }

        public PaletteVersionItemViewModel(PaletteVersionDto version)
        {
            Version = version;
        }

        public string Label =>
            $"{Version.SavedUtc.ToLocalTime():MMM d, HH:mm} · {Version.Swatches.Count} colors";

        public string SwatchSummary =>
            string.Join("  ", Version.Swatches.Take(6).Select(s => $"#{s.R:X2}{s.G:X2}{s.B:X2}"));
    }
}
