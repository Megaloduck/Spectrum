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

    /// <summary>One related-color rule in the reference record: the rule name,
    /// the colors it generates from the current record color, and the command
    /// that adds one of them to the palette (wired by the owning ViewModel,
    /// same pattern as <see cref="ColorValueRow.CopyCommand"/>).</summary>
    public partial class HarmonyRow : ObservableObject
    {
        public string Rule { get; init; } = string.Empty;
        public ObservableCollection<Color> Colors { get; } = new();
        public IRelayCommand<Color>? AddCommand { get; set; }
    }

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
            RebuildHarmonies();
        }

        private StudioWorkspaceViewModel Studio => _shell.Studio;

        // ---------------- Current record color (hover preview aware) ----------------

        /// <summary>Hover preview: while the pointer is over a board swatch the
        /// whole reference record reads from this color instead of the committed
        /// base color, then snaps back on leave. Null = no preview.</summary>
        private Color? _hoverColor;

        /// <summary>The color this dock currently documents: the hovered swatch
        /// if there is one, otherwise the Studio base color. Every readout in this
        /// ViewModel derives from this so hover and selection stay in lockstep.</summary>
        public Color CurrentColor => _hoverColor ?? Studio.BaseColor;

        /// <summary>True while a board hover is driving the record — the identity
        /// card shows a "previewing" hint so click-to-keep feels intentional.</summary>
        public bool IsHoverPreview => _hoverColor.HasValue;

        /// <summary>Feeds the board's pointer-enter handler. No-ops when the color
        /// is unchanged so rapid card-to-card moves only refresh once each.</summary>
        public void SetHoverPreview(Color? color)
        {
            if (_hoverColor == color) return;
            _hoverColor = color;
            RefreshRecord();
        }

        /// <summary>True when a preview of exactly this color is active. The board's
        /// leave handler uses it so a stale PointerExited from a previously hovered
        /// card can't clear the card now under the pointer.</summary>
        public bool IsHoverPreviewColor(Color color) => _hoverColor == color;

        // ---------------- Identity header ----------------

        public string BaseName => ColorNamingService.GetClosestName(CurrentColor);

        public string BaseHexForHeader =>
            CurrentColor.A == 255
                ? ColorMathService.ToHex(CurrentColor)
                : $"{ColorMathService.ToHex(CurrentColor)}{CurrentColor.A:X2}";

        public IBrush BaseSwatchBrush => new SolidColorBrush(CurrentColor);

        /// <summary>Contrast-aware foreground for text sitting on the identity
        /// swatch — mirrors Studio's luminance rule but for <see cref="CurrentColor"/>.</summary>
        public IBrush BaseForegroundBrush =>
            (0.2126 * CurrentColor.R + 0.7152 * CurrentColor.G + 0.0722 * CurrentColor.B) / 255.0 > 0.6
                ? new SolidColorBrush(Color.FromRgb(0x1E, 0x20, 0x25))
                : Brushes.White;

        /// <summary>Character tags joined for a single header line (e.g. "Pastel · Warm · Orange").</summary>
        public string CharacterTags
        {
            get
            {
                var tags = ColorCharacterService.Describe(CurrentColor);
                return tags.Count == 0 ? "Neutral" : string.Join("  ·  ", tags);
            }
        }

        public string CharacterNote => ColorCharacterService.Note(CurrentColor);

        /// <summary>Short usage-first line for the identity card, derived from the
        /// same HSL/OKLCH logic as the character tags. Kept separate from the
        /// longer <see cref="CharacterNote"/> so the dock can stay compact.</summary>
        private static string AnalyzeUsageNote(Color c)
        {
            var (h, s, l) = ColorMathService.RgbToHsl(c);
            var family = HueFamilyForNote(h, s)?.ToLowerInvariant() ?? "neutral";
            var vivid = s >= 0.70 && l is > 0.30 and < 0.75;
            var deepMuted = s <= 0.25 && l <= 0.35;
            var lightMuted = s <= 0.25 && l >= 0.65;

            static string? HueFamilyForNote(double h, double s)
            {
                if (s <= 0.08)
                    return h >= 340 || h < 20 ? "warm gray" : h < 70 ? "warm gray" : h < 160 ? "green gray"
                        : h < 280 ? "blue gray" : "cool gray";

                return h switch
                {
                    < 15 or >= 345 => "red",
                    < 45 => "orange",
                    < 70 => "yellow",
                    < 160 => "green",
                    < 200 => "cyan",
                    < 255 => "blue",
                    < 290 => "violet",
                    < 345 => "magenta",
                    _ => null,
                };
            }

            if (vivid)
                return $"Strong as an accent or call-to-action; check body-text contrast before using it on large areas.";
            if (deepMuted)
                return $"Works as a dark-mode surface or as text on light backgrounds.";
            if (lightMuted)
                return $"Calm enough for large surfaces and cards behind content.";
            if (l >= 0.93)
                return $"Best as a page or card background, never as text.";
            if (l <= 0.07)
                return $"Reads as text on light surfaces; too heavy for large fills.";

            return $"A balanced {family} — useful for buttons, links and highlights.";
        }

        /// <summary>Breadcrumb-style reference path segments for the Analyze dock:
        /// workspace › palette › current color (last segment styled as current).
        /// This is the same "reference object" language as the spec — the dock is
        /// not a random readout, it is the current color record.</summary>
        public string ReferenceWorkspace => "Studio";

        public string ReferencePalette => _shell.Library.SelectedPalette?.Name ?? "Palette";

        public string ReferenceColor => BaseName;

        public string ReferencePath =>
            $"{ReferenceWorkspace} › {ReferencePalette} › {ReferenceColor}";

        // ---------------- Conversion table ----------------

        public ObservableCollection<ValueSection> ValueSections { get; } = new();

        private void RebuildValueRows()
        {
            var c = CurrentColor;
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
            $"{ColorMathService.ToHex(CurrentColor)} text on {ColorMathService.ToHex(ContrastBackground)}";

        public double ContrastRatioNow => ContrastService.ContrastRatio(CurrentColor, ContrastBackground);

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

        public double ApcaNow => ContrastService.Apca(CurrentColor, ContrastBackground);
        public string ApcaLabel => $"Lc {ApcaNow:N0}";
        public string ApcaRatingLabel => ContrastService.RateApca(ApcaNow);

        /// <summary>Color reference note for the identity card, kept short enough for
        /// the dock but useful as the color's "why this color" line.</summary>
        public string UsageNote => AnalyzeUsageNote(CurrentColor);

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
            OnPropertyChanged(nameof(ReferencePalette));
            OnPropertyChanged(nameof(ReferencePath));
            OnPropertyChanged(nameof(CollisionsSummary));
        }

        // ---------------- Related colors (harmonies) ----------------

        /// <summary>Complementary / triadic / analogous strips derived from the
        /// record color — the "related colors" section of a reference page.</summary>
        public ObservableCollection<HarmonyRow> Harmonies { get; } = new();

        private void RebuildHarmonies()
        {
            Harmonies.Clear();
            AddHarmonyRule("COMPLEMENTARY", HarmonyType.Complementary);
            AddHarmonyRule("TRIADIC", HarmonyType.Triadic);
            AddHarmonyRule("ANALOGOUS", HarmonyType.Analogous);
        }

        private void AddHarmonyRule(string rule, HarmonyType type)
        {
            var row = new HarmonyRow { Rule = rule };
            row.AddCommand = AddRampStepCommand;

            foreach (var c in ColorHarmonyService.Generate(CurrentColor, type))
            {
                // Most rules return the base color first — the record itself is
                // already on screen, so the strip only shows the related ones.
                if (c.R == CurrentColor.R && c.G == CurrentColor.G && c.B == CurrentColor.B) continue;
                row.Colors.Add(c);
            }

            Harmonies.Add(row);
        }

        // ---------------- OKLCH ramp ----------------

        public ObservableCollection<Color> Ramp { get; } = new();

        private void RebuildRamp()
        {
            var steps = ColorMathService.RampOklch(CurrentColor);

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

        [RelayCommand] private void AddTints() => AppendGenerated(ColorMathService.TintsOklch(CurrentColor), "tint");
        [RelayCommand] private void AddShades() => AppendGenerated(ColorMathService.ShadesOklch(CurrentColor), "shade");
        [RelayCommand] private void AddTones() => AppendGenerated(ColorMathService.TonesOklch(CurrentColor), "tone");

        private void AppendGenerated(List<Color> colors, string kind)
        {
            _shell.PushHistory();
            foreach (var c in colors)
                _shell.Palette.Add(_shell.CreateSwatch(c, ColorNamingService.GetClosestName(c)));
            _shell.StatusMessage = $"Added {colors.Count} OKLCH {kind}s derived from {ColorMathService.ToHex(CurrentColor)}.";
        }

        // ---------------- Refresh + contrast matrix ----------------

        /// <summary>Refreshes every readout that derives from the Studio base color.
        /// Wired to <see cref="StudioWorkspaceViewModel.BaseColorChanged"/> by the shell.</summary>
        public void RefreshForBaseColor() => RefreshRecord();

        /// <summary>Re-raises everything that derives from <see cref="CurrentColor"/> —
        /// shared by base-color edits and board hover previews so both paths
        /// always produce the same record.</summary>
        private void RefreshRecord()
        {
            RebuildValueRows();
            RebuildRamp();
            RebuildHarmonies();

            OnPropertyChanged(nameof(CurrentColor));
            OnPropertyChanged(nameof(IsHoverPreview));
            OnPropertyChanged(nameof(BaseName));
            OnPropertyChanged(nameof(BaseHexForHeader));
            OnPropertyChanged(nameof(BaseSwatchBrush));
            OnPropertyChanged(nameof(BaseForegroundBrush));
            OnPropertyChanged(nameof(CharacterTags));
            OnPropertyChanged(nameof(CharacterNote));
            OnPropertyChanged(nameof(ReferencePath));
            OnPropertyChanged(nameof(ReferenceColor));
            OnPropertyChanged(nameof(UsageNote));
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
