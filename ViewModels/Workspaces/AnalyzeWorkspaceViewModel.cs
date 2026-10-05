using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spectrum.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Spectrum.ViewModels
{
    /// <summary>
    /// Ctrl+3 · Analyze — the conversion readouts (HEX / RGB / HSL / HSV / LAB /
    /// OKLCH), the WCAG + APCA contrast checker, the tints / shades / tones
    /// generators and the contrast matrix. The base color itself is owned by
    /// <see cref="StudioWorkspaceViewModel"/>; this workspace is notified through
    /// <see cref="RefreshForBaseColor"/> whenever it changes.
    /// </summary>
    public partial class AnalyzeWorkspaceViewModel : ViewModelBase
    {
        private readonly MainWindowViewModel _shell;

        public AnalyzeWorkspaceViewModel(MainWindowViewModel shell) => _shell = shell;

        private StudioWorkspaceViewModel Studio => _shell.Studio;

        // ---------------- Inspector: color science (§4) ----------------

        [ObservableProperty]
        private Color _contrastBackground = Colors.White;

        // Live conversion readouts for the base color.
        public string ScienceHex =>
            Studio.BaseColor.A == 255
                ? ColorMathService.ToHex(Studio.BaseColor)
                : $"{ColorMathService.ToHex(Studio.BaseColor)}{Studio.BaseColor.A:X2}";

        public string ScienceRgb =>
            Studio.BaseColor.A == 255
                ? $"{Studio.BaseColor.R}, {Studio.BaseColor.G}, {Studio.BaseColor.B}"
                : $"{Studio.BaseColor.R}, {Studio.BaseColor.G}, {Studio.BaseColor.B}, {Math.Round(Studio.BaseColor.A / 255.0, 2)}";

        public string ScienceHsl
        {
            get
            {
                var (h, s, l) = ColorMathService.RgbToHsl(Studio.BaseColor);
                return $"{h:N0}°, {s * 100:N0}%, {l * 100:N0}%";
            }
        }

        public string ScienceHsv
        {
            get
            {
                var hsv = ColorMathService.RgbToHsv(Studio.BaseColor);
                return $"{hsv.H:N0}°, {hsv.S * 100:N0}%, {hsv.V * 100:N0}%";
            }
        }

        public string ScienceLab
        {
            get
            {
                var lab = ColorMathService.RgbToLab(Studio.BaseColor);
                var lch = ColorMathService.LabToLch(lab);
                return $"{lab.L:N1}, {lab.A:N1}, {lab.B:N1}\nLCH {lch.L:N1}, {lch.C:N1}, {lch.H:N0}°";
            }
        }

        public string ScienceOklch
        {
            get
            {
                var ok = ColorMathService.RgbToOklch(Studio.BaseColor);
                return $"{ok.L:N3}, {ok.C:N3}, {ok.H:N0}°";
            }
        }

        // Contrast checker (WCAG + APCA) of the base color as text over ContrastBackground.
        public IBrush ContrastBackgroundBrush => new SolidColorBrush(ContrastBackground);

        public string ContrastPairLabel =>
            $"{ColorMathService.ToHex(Studio.BaseColor)} text on {ColorMathService.ToHex(ContrastBackground)}";

        public double ContrastRatioNow => ContrastService.ContrastRatio(Studio.BaseColor, ContrastBackground);

        public string ContrastRatioLabel => $"{ContrastRatioNow:N2}:1";
        public string ContrastRatingLabel => ContrastService.Rate(ContrastRatioNow);
        public string ContrastRatingLargeLabel => ContrastService.Rate(ContrastRatioNow, largeText: true);

        public double ApcaNow => ContrastService.Apca(Studio.BaseColor, ContrastBackground);
        public string ApcaLabel => $"Lc {ApcaNow:N0}";
        public string ApcaRatingLabel => ContrastService.RateApca(ApcaNow);

        partial void OnContrastBackgroundChanged(Color value) => RaiseContrastPropertiesChanged();

        private void RaiseContrastPropertiesChanged()
        {
            OnPropertyChanged(nameof(ContrastBackgroundBrush));
            OnPropertyChanged(nameof(ContrastPairLabel));
            OnPropertyChanged(nameof(ContrastRatioNow));
            OnPropertyChanged(nameof(ContrastRatioLabel));
            OnPropertyChanged(nameof(ContrastRatingLabel));
            OnPropertyChanged(nameof(ContrastRatingLargeLabel));
            OnPropertyChanged(nameof(ApcaNow));
            OnPropertyChanged(nameof(ApcaLabel));
            OnPropertyChanged(nameof(ApcaRatingLabel));
        }

        /// <summary>Refreshes every readout that derives from the Studio base color.
        /// Wired to <see cref="StudioWorkspaceViewModel.BaseColorChanged"/> by the shell.</summary>
        public void RefreshForBaseColor()
        {
            OnPropertyChanged(nameof(ScienceHex));
            OnPropertyChanged(nameof(ScienceRgb));
            OnPropertyChanged(nameof(ScienceHsl));
            OnPropertyChanged(nameof(ScienceHsv));
            OnPropertyChanged(nameof(ScienceLab));
            OnPropertyChanged(nameof(ScienceOklch));
            RaiseContrastPropertiesChanged();
        }

        [RelayCommand]
        private void UseWhiteContrastBackground() => ContrastBackground = Colors.White;

        [RelayCommand]
        private void UseBlackContrastBackground() => ContrastBackground = Colors.Black;

        [RelayCommand]
        private async Task PickContrastBackgroundAsync()
        {
            var picked = await MainWindowViewModel.PickColorAsync(ContrastBackground);
            if (picked is null) return;
            ContrastBackground = picked.Value;
            _shell.StatusMessage = "Contrast background updated.";
        }

        // Tints / shades / tones generators (§4 "Generate tints / shades").
        [RelayCommand] private void AddTints() => AppendGenerated(ColorMathService.Tints(Studio.BaseColor), "tint");
        [RelayCommand] private void AddShades() => AppendGenerated(ColorMathService.Shades(Studio.BaseColor), "shade");
        [RelayCommand] private void AddTones() => AppendGenerated(ColorMathService.Tones(Studio.BaseColor), "tone");

        private void AppendGenerated(List<Color> colors, string kind)
        {
            _shell.PushHistory();
            foreach (var c in colors)
                _shell.Palette.Add(_shell.CreateSwatch(c, ColorNamingService.GetClosestName(c)));
            _shell.StatusMessage = $"Added {colors.Count} {kind}s derived from {ColorMathService.ToHex(Studio.BaseColor)}.";
        }

        /// <summary>§4/§8 "Contrast matrix": every text-on-background pair of the
        /// palette scored at once (WCAG color-coded, APCA in tooltips).</summary>
        [RelayCommand]
        private async Task OpenContrastMatrixAsync()
        {
            if (MainWindowViewModel.GetOwnerWindow() is not { } owner) return;

            var current = PaletteLibraryService.FindPalette(_shell.ActivePaletteId)
                          ?? PaletteLibraryService.Library.Palettes.FirstOrDefault();

            var window = new Views.ContrastMatrixWindow(current);
            await window.ShowDialog(owner);
        }
    }
}
