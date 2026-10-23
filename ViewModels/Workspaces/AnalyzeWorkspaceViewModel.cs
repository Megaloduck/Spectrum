using Avalonia;
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
    /// <summary>One row of the evaluation card's conversion table. The copy text is
    /// the displayed value, so what you see is exactly what lands on the clipboard.</summary>
    public partial class ColorValueRow : ObservableObject
    {
        public string Label { get; init; } = string.Empty;
        public string ToolTip { get; init; } = string.Empty;

        private string _value = string.Empty;

        /// <summary>Mutable so edits update rows in place instead of rebuilding the
        /// collection on every refresh.</summary>
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public IRelayCommand<ColorValueRow>? CopyCommand { get; set; }
    }

    /// <summary>One usage verdict in the contrast details: a role (body text, large
    /// text, UI component), the WCAG requirement it must meet, and whether the
    /// current pair passes it.</summary>
    public sealed record ContrastRole(string Role, string Requirement, string Verdict, bool Passed);

    /// <summary>A group of conversion rows (common vs. advanced spaces).</summary>
    public sealed record ValueSection(string Title, ObservableCollection<ColorValueRow> Rows);

    /// <summary>One related-color rule: the rule name, the colors it generates from
    /// the evaluated color, and the command that adds one of them to the palette.</summary>
    public partial class HarmonyRow : ObservableObject
    {
        public string Rule { get; init; } = string.Empty;
        public ObservableCollection<Color> Colors { get; } = new();
        public IRelayCommand<Color>? AddCommand { get; set; }
    }

    /// <summary>Ctrl+3 · Analyze — a single-color evaluation of the *selected* board
    /// swatch (or the Studio base color when nothing is selected).
    ///
    /// The center owns the whole story:
    ///   · Character: name, tags and a one-line "what this color is".
    ///   · Strengths / weaknesses: design-use roles and measured accessibility
    ///     results, strongest first and worst first (ColorEvaluationService).
    ///   · Details: conversions and contrast math, collapsed until asked for.
    ///   · Palette context: collisions, related colors, nearest neighbors and the
    ///     OKLCH ramp / tints / shades / tones.
    ///
    /// Hovering a board card never changes this record; only selecting a swatch or
    /// editing the base color (while nothing is selected) does.
    /// </summary>
    public partial class AnalyzeWorkspaceViewModel : ViewModelBase
    {
        private readonly MainWindowViewModel _shell;

        public AnalyzeWorkspaceViewModel(MainWindowViewModel shell)
        {
            _shell = shell;
            RefreshEvaluation();
            RebuildGlossary();
        }

        private StudioWorkspaceViewModel Studio => _shell.Studio;

        // ---------------- Evaluation target ----------------

        public ColorSwatch? SelectedSwatch => _shell.SelectedSwatch;

        /// <summary>True while the record documents the Studio base color because
        /// no board swatch is selected. Shows the hint and the add-base-color CTA.</summary>
        public bool IsEvaluatingBaseColor => SelectedSwatch is null;

        public Color EvaluationColor => SelectedSwatch?.Color ?? Studio.BaseColor;

        public string EvaluationName =>
            SelectedSwatch?.Name ?? ColorNamingService.GetClosestName(Studio.BaseColor);

        public string EvaluationHex
        {
            get
            {
                var c = EvaluationColor;
                return c.A == 255
                    ? ColorMathService.ToHex(c)
                    : $"{ColorMathService.ToHex(c)}{c.A:X2}";
            }
        }

        public IBrush EvaluationSwatchBrush => new SolidColorBrush(EvaluationColor);

        public string EvaluationTargetHint => IsEvaluatingBaseColor
            ? "No swatch selected — evaluating the Studio base color. Select a swatch to evaluate it instead."
            : "Evaluating the swatch selected on the board.";

        public string EvaluationTargetBadge => IsEvaluatingBaseColor ? "Base color" : "Selected swatch";

        /// <summary>Breadcrumb for the record: workspace › palette › color.</summary>
        public string ReferenceWorkspace => "Analyze";

        public string ReferencePalette => _shell.Library.SelectedPalette?.Name ?? "Palette";

        public string ReferencePath => $"{ReferenceWorkspace} › {ReferencePalette} › {EvaluationName}";

        /// <summary>Board header in Analyze only: palette identity plus a hint.</summary>
        public string BoardHeaderLabel => $"{ReferencePalette} · {_shell.PaletteCountLabel}";

        public string BoardHint => "Select a color to evaluate it.";

        public void RefreshReferencePath()
        {
            OnPropertyChanged(nameof(ReferenceWorkspace));
            OnPropertyChanged(nameof(ReferencePalette));
            OnPropertyChanged(nameof(ReferencePath));
        }

        // ---------------- Character + evaluation ----------------

        private ColorEvaluation? _evaluation;

        private IReadOnlyList<EvaluationPoint> _designStrengths = Array.Empty<EvaluationPoint>();
        private IReadOnlyList<EvaluationPoint> _accessibilityStrengths = Array.Empty<EvaluationPoint>();
        private IReadOnlyList<EvaluationPoint> _designWeaknesses = Array.Empty<EvaluationPoint>();
        private IReadOnlyList<EvaluationPoint> _accessibilityWeaknesses = Array.Empty<EvaluationPoint>();

        public IReadOnlyList<string> EvaluationTags => _evaluation?.Tags ?? Array.Empty<string>();

        public string EvaluationOneLine => _evaluation?.OneLine ?? string.Empty;

        public IReadOnlyList<EvaluationPoint> DesignStrengths => _designStrengths;
        public IReadOnlyList<EvaluationPoint> AccessibilityStrengths => _accessibilityStrengths;
        public IReadOnlyList<EvaluationPoint> DesignWeaknesses => _designWeaknesses;
        public IReadOnlyList<EvaluationPoint> AccessibilityWeaknesses => _accessibilityWeaknesses;

        public bool HasDesignStrengths => _designStrengths.Count > 0;
        public bool HasAccessibilityStrengths => _accessibilityStrengths.Count > 0;
        public bool HasDesignWeaknesses => _designWeaknesses.Count > 0;
        public bool HasAccessibilityWeaknesses => _accessibilityWeaknesses.Count > 0;

        public bool HasAnyStrengths => HasDesignStrengths || HasAccessibilityStrengths;
        public bool HasAnyWeaknesses => HasDesignWeaknesses || HasAccessibilityWeaknesses;

        // ---------------- Palette context ----------------

        public IReadOnlyList<VisionCollision> CollisionsForSelection =>
            _evaluation?.Collisions ?? Array.Empty<VisionCollision>();

        public bool HasCollisionsForSelection => CollisionsForSelection.Count > 0;

        public string CollisionsSummary
        {
            get
            {
                var count = CollisionsForSelection.Count;
                return count == 0
                    ? "No pair involving this color collapses under simulated vision."
                    : $"{count} pair{(count == 1 ? "" : "s")} involving this color {(count == 1 ? "is" : "are")} hard to tell apart.";
            }
        }

        public IReadOnlyList<PaletteNeighbor> PaletteNeighbors =>
            _evaluation?.Neighbors ?? Array.Empty<PaletteNeighbor>();

        public bool HasPaletteNeighbors => PaletteNeighbors.Count > 0;

        // ---------------- Conversion table ----------------

        public ObservableCollection<ValueSection> ValueSections { get; } = new();

        private void RebuildValueRows()
        {
            var c = EvaluationColor;
            var (h, s, l) = ColorMathService.RgbToHsl(c);
            var hsv = ColorMathService.RgbToHsv(c);
            var hwb = ColorMathService.RgbToHwb(c);
            var lab = ColorMathService.RgbToLab(c);
            var lch = ColorMathService.LabToLch(lab);
            var ok = ColorMathService.RgbToOklch(c);
            var cmyk = ColorMathService.RgbToCmyk(c);

            var hex = EvaluationHex;
            var rgb = c.A == 255
                ? $"{c.R}, {c.G}, {c.B}"
                : $"{c.R}, {c.G}, {c.B}, {Math.Round(c.A / 255.0, 2)}";

            // Fast path: the rows already exist — update values in place so the
            // ItemsControl isn't torn down on every refresh.
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
            _shell.StatusMessage = "Copied every conversion of the evaluated color.";
        }

        [RelayCommand]
        private async Task CopyEvaluationHexAsync()
        {
            await ClipboardHelper.SetTextAsync(EvaluationHex);
            _shell.StatusMessage = $"Copied {EvaluationHex}.";
        }

        // ---------------- Contrast details ----------------

        [ObservableProperty]
        private Color _contrastBackground = Colors.White;

        partial void OnContrastBackgroundChanged(Color value) => RaiseContrastPropertiesChanged();

        public IBrush ContrastBackgroundBrush => new SolidColorBrush(ContrastBackground);

        public string ContrastPairLabel =>
            $"{EvaluationHex} text on {ColorMathService.ToHex(ContrastBackground)}";

        public double ContrastRatioNow => ContrastService.ContrastRatio(EvaluationColor, ContrastBackground);

        public string ContrastRatioLabel => $"{ContrastRatioNow:N2}:1";

        /// <summary>Three labeled rows: the roles WCAG actually defines.</summary>
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

        public double ApcaNow => ContrastService.Apca(EvaluationColor, ContrastBackground);
        public string ApcaLabel => $"Lc {ApcaNow:N0}";
        public string ApcaRatingLabel => ContrastService.RateApca(ApcaNow);

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

        // ---------------- Details disclosure ----------------

        [ObservableProperty]
        private bool _showDetails;

        public string DetailsToggleLabel => ShowDetails ? "Hide details" : "Show details";

        partial void OnShowDetailsChanged(bool value) => OnPropertyChanged(nameof(DetailsToggleLabel));

        // ---------------- Related colors ----------------

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
            var row = new HarmonyRow { Rule = rule, AddCommand = AddRelatedColorCommand };

            foreach (var c in ColorHarmonyService.Generate(EvaluationColor, type))
            {
                // Most rules return the evaluated color first — it is already on
                // screen, so the strip only offers the related ones.
                if (c.R == EvaluationColor.R && c.G == EvaluationColor.G && c.B == EvaluationColor.B) continue;
                row.Colors.Add(c);
            }

            Harmonies.Add(row);
        }

        [RelayCommand]
        private void AddRelatedColor(Color color)
        {
            _shell.PushHistory();
            var name = ColorNamingService.GetClosestName(color);
            _shell.Palette.Add(_shell.CreateSwatch(color, name));
            _shell.StatusMessage = $"Added {ColorMathService.ToHex(color)} ({name}) from related colors.";
        }

        // ---------------- OKLCH ramp + generators ----------------

        public ObservableCollection<Color> Ramp { get; } = new();

        private void RebuildRamp()
        {
            var steps = ColorMathService.RampOklch(EvaluationColor);

            if (Ramp.Count == steps.Count)
            {
                for (var i = 0; i < steps.Count; i++) Ramp[i] = steps[i];
                return;
            }

            Ramp.Clear();
            foreach (var color in steps) Ramp.Add(color);
        }

        [RelayCommand]
        private void AddRampStep(Color color)
        {
            _shell.PushHistory();
            var name = ColorNamingService.GetClosestName(color);
            _shell.Palette.Add(_shell.CreateSwatch(color, name));
            _shell.StatusMessage = $"Added {ColorMathService.ToHex(color)} ({name}) from the ramp.";
        }

        [RelayCommand] private void AddTints() => AppendGenerated(ColorMathService.TintsOklch(EvaluationColor), "tint");
        [RelayCommand] private void AddShades() => AppendGenerated(ColorMathService.ShadesOklch(EvaluationColor), "shade");
        [RelayCommand] private void AddTones() => AppendGenerated(ColorMathService.TonesOklch(EvaluationColor), "tone");

        private void AppendGenerated(List<Color> colors, string kind)
        {
            _shell.PushHistory();
            foreach (var c in colors)
                _shell.Palette.Add(_shell.CreateSwatch(c, ColorNamingService.GetClosestName(c)));
            _shell.StatusMessage =
                $"Added {colors.Count} OKLCH {kind}s derived from {ColorMathService.ToHex(EvaluationColor)}.";
        }

        /// <summary>Only meaningful in the fallback state: the evaluated color is the
        /// Studio base color and is not on the board yet.</summary>
        [RelayCommand]
        private void AddCurrentColorToPalette()
        {
            var color = EvaluationColor;
            _shell.PushHistory();
            var name = ColorNamingService.GetClosestName(color);
            _shell.Palette.Add(_shell.CreateSwatch(color, name));
            _shell.StatusMessage = $"Added {ColorMathService.ToHex(color)} ({name}) to the palette.";
        }

        // ---------------- Glossary ----------------

        public ObservableCollection<GlossaryTerm> GlossaryTerms { get; } = new();

        [ObservableProperty]
        private string _glossarySearchText = string.Empty;

        public bool IsGlossaryEmpty => GlossaryTerms.Count == 0;

        public string GlossaryCountLabel => GlossaryTerms.Count == 1
            ? "1 term"
            : $"{GlossaryTerms.Count} terms";

        partial void OnGlossarySearchTextChanged(string value) => RebuildGlossary();

        private void RebuildGlossary()
        {
            var query = GlossarySearchText?.Trim() ?? string.Empty;

            GlossaryTerms.Clear();
            foreach (var term in GlossaryService.Terms)
            {
                if (query.Length > 0 &&
                    !term.Term.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                    !term.Definition.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                GlossaryTerms.Add(term);
            }

            OnPropertyChanged(nameof(IsGlossaryEmpty));
            OnPropertyChanged(nameof(GlossaryCountLabel));
        }

        // ---------------- Refresh ----------------

        /// <summary>Wired to Studio's BaseColorChanged. A swatch selection retargets
        /// the evaluation, so base-color edits only refresh while nothing is selected.</summary>
        public void RefreshForBaseColor()
        {
            if (IsEvaluatingBaseColor) RefreshEvaluation();
        }

        /// <summary>Wired to palette changes and selected-swatch edits.</summary>
        public void RefreshForPalette()
        {
            OnPropertyChanged(nameof(ReferencePalette));
            OnPropertyChanged(nameof(ReferencePath));
            RefreshEvaluation();
        }

        /// <summary>Recomputes the whole record from the current evaluation target.</summary>
        public void RefreshEvaluation()
        {
            var palette = _shell.Palette
                .Select(s => (s.Name, s.Color))
                .ToList();

            _evaluation = ColorEvaluationService.Evaluate(EvaluationColor, EvaluationName, palette);

            _designStrengths = _evaluation.Strengths
                .Where(p => p.Aspect == EvaluationAspect.DesignUse).ToArray();
            _accessibilityStrengths = _evaluation.Strengths
                .Where(p => p.Aspect == EvaluationAspect.Accessibility).ToArray();
            _designWeaknesses = _evaluation.Weaknesses
                .Where(p => p.Aspect == EvaluationAspect.DesignUse).ToArray();
            _accessibilityWeaknesses = _evaluation.Weaknesses
                .Where(p => p.Aspect == EvaluationAspect.Accessibility).ToArray();

            RebuildValueRows();
            RebuildRamp();
            RebuildHarmonies();
            RaiseContrastPropertiesChanged();

            OnPropertyChanged(nameof(SelectedSwatch));
            OnPropertyChanged(nameof(IsEvaluatingBaseColor));
            OnPropertyChanged(nameof(EvaluationColor));
            OnPropertyChanged(nameof(EvaluationName));
            OnPropertyChanged(nameof(EvaluationHex));
            OnPropertyChanged(nameof(EvaluationSwatchBrush));
            OnPropertyChanged(nameof(EvaluationTargetHint));
            OnPropertyChanged(nameof(EvaluationTargetBadge));
            OnPropertyChanged(nameof(EvaluationTags));
            OnPropertyChanged(nameof(EvaluationOneLine));
            OnPropertyChanged(nameof(DesignStrengths));
            OnPropertyChanged(nameof(AccessibilityStrengths));
            OnPropertyChanged(nameof(DesignWeaknesses));
            OnPropertyChanged(nameof(AccessibilityWeaknesses));
            OnPropertyChanged(nameof(HasDesignStrengths));
            OnPropertyChanged(nameof(HasAccessibilityStrengths));
            OnPropertyChanged(nameof(HasAnyStrengths));
            OnPropertyChanged(nameof(HasDesignWeaknesses));
            OnPropertyChanged(nameof(HasAccessibilityWeaknesses));
            OnPropertyChanged(nameof(HasAnyWeaknesses));
            OnPropertyChanged(nameof(CollisionsForSelection));
            OnPropertyChanged(nameof(HasCollisionsForSelection));
            OnPropertyChanged(nameof(CollisionsSummary));
            OnPropertyChanged(nameof(PaletteNeighbors));
            OnPropertyChanged(nameof(HasPaletteNeighbors));
            OnPropertyChanged(nameof(ReferencePalette));
            OnPropertyChanged(nameof(ReferencePath));
            OnPropertyChanged(nameof(BoardHeaderLabel));
        }

        // ---------------- Contrast matrix (§4/§8) ----------------

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
