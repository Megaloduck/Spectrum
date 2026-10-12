using Avalonia.Media;
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
    /// <summary>One row of the reference record's conversion table. The copy text is
    /// the displayed value, so what you see is exactly what lands on the clipboard.</summary>
    public partial class ColorValueRow : ObservableObject
    {
        public string Label { get; init; } = string.Empty;
        public string ToolTip { get; init; } = string.Empty;

        private string _value = string.Empty;

        /// <summary>Mutable so slider drags update rows in place instead of
        /// rebuilding the collection on every tick (which churned the
        /// ItemsControl 60×/second while dragging).</summary>
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        /// <summary>Wired by the owning ViewModel (same pattern as ColorSwatch commands).</summary>
        public IRelayCommand<ColorValueRow>? CopyCommand { get; set; }
    }

    /// <summary>One usage verdict in the contrast panel: a role (body text, large
    /// text, UI component), the WCAG requirement it must meet, and whether the
    /// current pair passes it.</summary>
    public sealed record ContrastRole(string Role, string Requirement, string Verdict, bool Passed);

    /// <summary>A collapsible group of conversion rows (common vs. advanced spaces).</summary>
    public sealed record ValueSection(string Title, ObservableCollection<ColorValueRow> Rows);

    /// <summary>Ctrl+3 · Analyze — the color *reference record* for the base color,
    /// restructured from a bare readout column into identity → values → usage:
    ///
    ///   · Identity header: big swatch, name, hex, character tags and a one-line
    ///     usage note (ColorCharacterService), so the color is legible before any math.
    ///   · Conversion table: HEX / RGB / HSL / HSV plus HWB, LAB/LCH, OKLCH and CMYK,
    ///     grouped into common and advanced sections, each row copyable on click.
    ///   · Contrast: preview + ratio, then role-based WCAG verdicts (body / large /
    ///     UI component) and the APCA Lc line — three labeled pass-fail rows instead
    ///     of two loose labels.
    ///   · Color-vision collisions: every palette pair measured under each simulated
    ///     CVD form with ΔE (VisionCollisionService), worst first.
    ///   · OKLCH ramp: tints → base → shades preview strip, click a step to add it;
    ///     the Tints/Shades/Tones generators now mix in OKLCH.
    ///
    /// The base color itself is owned by <see cref="StudioWorkspaceViewModel"/>;
    /// this workspace is notified through <see cref="RefreshForBaseColor"/> whenever
    /// it changes, and through <see cref="RefreshForPalette"/> when the palette does.
    /// </summary>
    public partial class AnalyzeWorkspaceViewModel : ViewModelBase
    {
        private readonly MainWindowViewModel _shell;

        public AnalyzeWorkspaceViewModel(MainWindowViewModel shell)
        {
            _shell = shell;
            RebuildValueRows();
            RebuildRamp();
        }

        private StudioWorkspaceViewModel Studio => _shell.Studio;

        // ---------------- Identity header ----------------

        public string BaseName => Studio.BaseColorName;

        public string BaseHexForHeader =>
            Studio.BaseColor.A == 255
                ? ColorMathService.ToHex(Studio.BaseColor)
                : $"{ColorMathService.ToHex(Studio.BaseColor)}{Studio.BaseColor.A:X2}";

        public IBrush BaseSwatchBrush => Studio.BaseColorBrush;

        /// <summary>Character tags joined for a single header line (e.g. "Pastel · Warm · Orange").</summary>
        public string CharacterTags
        {
            get
            {
                var tags = ColorCharacterService.Describe(Studio.BaseColor);
                return tags.Count == 0 ? "Neutral" : string.Join("  ·  ", tags);
            }
        }

        public string CharacterNote => ColorCharacterService.Note(Studio.BaseColor);

        // ---------------- Conversion table ----------------

        public ObservableCollection<ValueSection> ValueSections { get; } = new();

        private void RebuildValueRows()
        {
            var c = Studio.BaseColor;
            var (h, s, l) = ColorMathService.RgbToHsl(c);
            var hsv = ColorMathService.RgbToHsv(c);
            var hwb = ColorMathService.RgbToHwb(c);
            var lab = ColorMathService.RgbToLab(c);
            var lch = ColorMathService.LabToLch(lab);
            var ok = ColorMathService.RgbToOklch(c);
            var cmyk = ColorMathService.RgbToCmyk(c);

            var hex = c.A == 255
                ? ColorMathService.ToHex(c)
                : $"{ColorMathService.ToHex(c)}{c.A:X2}";
            var rgb = c.A == 255
                ? $"{c.R}, {c.G}, {c.B}"
                : $"{c.R}, {c.G}, {c.B}, {Math.Round(c.A / 255.0, 2)}";

            // Fast path: the rows already exist (slider drag) — update values in
            // place so the ItemsControl isn't torn down 60×/second.
            if (ValueSections.Count == 2 &&
                ValueSections[0].Rows.Count == 4 && ValueSections[1].Rows.Count == 5)
            {
                var common = ValueSections[0].Rows;
                var advanced = ValueSections[1].Rows;

                common[0].Value = hex;
                common[1].Value = rgb;
                common[2].Value = $"{h:N0}°, {s * 100:N0}%, {l * 100:N0}%";
                common[3].Value = $"{hsv.H:N0}°, {hsv.S * 100:N0}%, {hsv.V * 100:N0}%";

                advanced[0].Value = $"{hwb.H:N0}°, {hwb.W * 100:N0}%, {hwb.B * 100:N0}%";
                advanced[1].Value = $"{lab.L:N1}, {lab.A:N1}, {lab.B:N1}";
                advanced[2].Value = $"{lch.L:N1}, {lch.C:N1}, {lch.H:N0}°";
                advanced[3].Value = $"{ok.L:N3}, {ok.C:N3}, {ok.H:N0}°";
                advanced[4].Value = $"{cmyk.C * 100:N0}%, {cmyk.M * 100:N0}%, {cmyk.Y * 100:N0}%, {cmyk.K * 100:N0}%";
                return;
            }

            var commonRows = new ObservableCollection<ColorValueRow>
            {
                Row("HEX", hex, "HEX color code"),
                Row("RGB", rgb, "Red, green, blue"),
                Row("HSL", $"{h:N0}°, {s * 100:N0}%, {l * 100:N0}%", "Hue, saturation, lightness"),
                Row("HSV", $"{hsv.H:N0}°, {hsv.S * 100:N0}%, {hsv.V * 100:N0}%", "Hue, saturation, value"),
            };

            var advancedRows = new ObservableCollection<ColorValueRow>
            {
                Row("HWB", $"{hwb.H:N0}°, {hwb.W * 100:N0}%, {hwb.B * 100:N0}%", "Hue, whiteness, blackness"),
                Row("LAB", $"{lab.L:N1}, {lab.A:N1}, {lab.B:N1}", "CIELAB (D65) — perceptually uniform"),
                Row("LCH", $"{lch.L:N1}, {lch.C:N1}, {lch.H:N0}°", "CIELAB in polar form"),
                Row("OKLCH", $"{ok.L:N3}, {ok.C:N3}, {ok.H:N0}°", "Ottosson's perceptual space — modern CSS oklch()"),
                Row("CMYK", $"{cmyk.C * 100:N0}%, {cmyk.M * 100:N0}%, {cmyk.Y * 100:N0}%, {cmyk.K * 100:N0}%",
                    "Screen approximation of print ink (no ICC profile)"),
            };

            ValueSections.Clear();
            ValueSections.Add(new ValueSection("COMMON", commonRows));
            ValueSections.Add(new ValueSection("ADVANCED SPACES", advancedRows));
        }

        private ColorValueRow Row(string label, string value, string toolTip)
        {
            var row = new ColorValueRow { Label = label, Value = value, ToolTip = toolTip };
            row.CopyCommand = CopyValueCommand;
            return row;
        }

        [RelayCommand]
        private async Task CopyValueAsync(ColorValueRow? row)
        {
            if (row is null) return;
            await ClipboardHelper.SetTextAsync(row.Value);
            _shell.StatusMessage = $"Copied {row.Value} ({row.Label}).";
        }

        [RelayCommand]
        private async Task CopyAllValuesAsync()
        {
            var text = string.Join(Environment.NewLine,
                ValueSections.SelectMany(sec => sec.Rows).Select(r => $"{r.Label,-7} {r.Value}"));
            await ClipboardHelper.SetTextAsync(text);
            _shell.StatusMessage = "Copied every conversion of the base color.";
        }

        // ---------------- Contrast: preview + role verdicts ----------------

        [ObservableProperty]
        private Color _contrastBackground = Colors.White;

        public IBrush ContrastBackgroundBrush => new SolidColorBrush(ContrastBackground);

        public string ContrastPairLabel =>
            $"{ColorMathService.ToHex(Studio.BaseColor)} text on {ColorMathService.ToHex(ContrastBackground)}";

        public double ContrastRatioNow => ContrastService.ContrastRatio(Studio.BaseColor, ContrastBackground);

        public string ContrastRatioLabel => $"{ContrastRatioNow:N2}:1";

        /// <summary>Three labeled rows: the roles WCAG actually defines, instead of
        /// two loose AA/AAA labels the user has to interpret.</summary>
        public ObservableCollection<ContrastRole> ContrastRoles
        {
            get
            {
                var ratio = ContrastRatioNow;
                return new ObservableCollection<ContrastRole>
                {
                    new("Body text", "AA needs 4.5:1",
                        ContrastService.Rate(ratio), ratio >= 4.5),
                    new("Large text", "AA needs 3:1",
                        ContrastService.Rate(ratio, largeText: true), ratio >= 3.0),
                    new("UI component", "AA needs 3:1",
                        ratio >= 3.0 ? "Pass" : "Fail", ratio >= 3.0),
                };
            }
        }

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
            OnPropertyChanged(nameof(ContrastRoles));
            OnPropertyChanged(nameof(ApcaNow));
            OnPropertyChanged(nameof(ApcaLabel));
            OnPropertyChanged(nameof(ApcaRatingLabel));
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

        // ---------------- Color-vision collisions ----------------

        public ObservableCollection<VisionCollision> Collisions { get; } = new();

        public bool HasCollisions => Collisions.Count > 0;

        public string CollisionsSummary => Collisions.Count == 0
            ? "No palette pair collapses under simulated color vision."
            : $"{Collisions.Count} pair{(Collisions.Count == 1 ? "" : "s")} hard to tell apart — separate them with a label, shape or spacing.";

        /// <summary>Recomputes the pairwise CVD collision list from the current palette.
        /// Called by the shell whenever the palette (or any swatch color) changes.</summary>
        public void RefreshForPalette()
        {
            var palette = _shell.Palette
                .Select(s => (s.Name, s.Color))
                .ToList();

            var found = VisionCollisionService.FindCollisions(palette);

            Collisions.Clear();
            foreach (var collision in found.Take(12))
                Collisions.Add(collision);

            OnPropertyChanged(nameof(HasCollisions));
            OnPropertyChanged(nameof(CollisionsSummary));
        }

        // ---------------- OKLCH ramp ----------------

        public ObservableCollection<Color> Ramp { get; } = new();

        private void RebuildRamp()
        {
            var steps = ColorMathService.RampOklch(Studio.BaseColor);

            // Index-set in place when the strip already exists (slider drag) —
            // one PropertyChanged per step instead of Clear + 9 Adds per tick.
            if (Ramp.Count == steps.Count)
            {
                for (var i = 0; i < steps.Count; i++) Ramp[i] = steps[i];
                return;
            }

            Ramp.Clear();
            foreach (var color in steps) Ramp.Add(color);
        }

        /// <summary>Click a ramp step to add that exact color to the palette.</summary>
        [RelayCommand]
        private void AddRampStep(Color color)
        {
            _shell.PushHistory();
            var name = ColorNamingService.GetClosestName(color);
            _shell.Palette.Add(_shell.CreateSwatch(color, name));
            _shell.StatusMessage = $"Added {ColorMathService.ToHex(color)} ({name}) from the ramp.";
        }

        // ---------------- Generators (now OKLCH-interpolated) ----------------

        [RelayCommand] private void AddTints() => AppendGenerated(ColorMathService.TintsOklch(Studio.BaseColor), "tint");
        [RelayCommand] private void AddShades() => AppendGenerated(ColorMathService.ShadesOklch(Studio.BaseColor), "shade");
        [RelayCommand] private void AddTones() => AppendGenerated(ColorMathService.TonesOklch(Studio.BaseColor), "tone");

        private void AppendGenerated(List<Color> colors, string kind)
        {
            _shell.PushHistory();
            foreach (var c in colors)
                _shell.Palette.Add(_shell.CreateSwatch(c, ColorNamingService.GetClosestName(c)));
            _shell.StatusMessage = $"Added {colors.Count} OKLCH {kind}s derived from {ColorMathService.ToHex(Studio.BaseColor)}.";
        }

        // ---------------- Refresh + contrast matrix ----------------

        /// <summary>Refreshes every readout that derives from the Studio base color.
        /// Wired to <see cref="StudioWorkspaceViewModel.BaseColorChanged"/> by the shell.</summary>
        public void RefreshForBaseColor()
        {
            RebuildValueRows();
            RebuildRamp();

            OnPropertyChanged(nameof(BaseName));
            OnPropertyChanged(nameof(BaseHexForHeader));
            OnPropertyChanged(nameof(BaseSwatchBrush));
            OnPropertyChanged(nameof(CharacterTags));
            OnPropertyChanged(nameof(CharacterNote));
            RaiseContrastPropertiesChanged();
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
