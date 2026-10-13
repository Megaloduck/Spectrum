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
    /// <summary>Which context the Preview gallery is rendering the palette into.</summary>
    public enum MockupScene
    {
        Dashboard,
        Landing,
        Mobile,
        Chart,
        Poster,
    }

    /// <summary>One entry in a role's color dropdown: the color chip, its label and
    /// hex. For "Auto" the chip is the derived default's *current* color, so the
    /// entry previews what Auto resolves to instead of being a word with no color.</summary>
    public partial class MockupOption : ObservableObject
    {
        public MockupOption(string label, Color color, bool isAuto = false)
        {
            Label = label;
            IsAuto = isAuto;
            Update(color);
        }

        public string Label { get; }
        public bool IsAuto { get; }

        /// <summary>Updated in place for Auto entries: replacing the item in the
        /// collection instead would make the ComboBox drop its selection (and write
        /// -1 back into SelectedOption).</summary>
        [ObservableProperty]
        private Color _color;

        [ObservableProperty]
        private string _hex = string.Empty;

        /// <summary>Brush for the chip shown in the item and in the closed ComboBox.</summary>
        [ObservableProperty]
        private IBrush _swatch = Brushes.Transparent;

        public void Update(Color color)
        {
            Color = color;
            Hex = ColorMathService.ToHex(color);
            Swatch = new SolidColorBrush(color);
        }
    }

    /// <summary>One semantic role in the mockup (Primary, Accent, Surface…), bound to
    /// a palette color by index — or to Auto (index 0) for the derived default.
    /// Options[0] is always "Auto"; options 1..n map to palette swatch i-1.</summary>
    public partial class MockupRole : ObservableObject
    {
        /// <summary>Raised when the user reassigns this role, so the shell can refresh.</summary>
        public event Action? RoleChanged;

        public string Name { get; init; } = string.Empty;

        public ObservableCollection<MockupOption> Options { get; } = new();

        /// <summary>True while this role is pinned to a palette color instead of the
        /// derived default — keeps the reset button's enabled state in step.</summary>
        public bool IsOverridden => SelectedOption > 0;

        /// <summary>Snap back to the derived default (Auto).</summary>
        [RelayCommand]
        private void ResetToAuto()
        {
            if (SelectedOption == 0) return;

            // Deliberately not silent: the shell has to refresh the mockup brushes.
            SelectedOption = 0;
        }

        [ObservableProperty]
        private int _selectedOption;

        /// <summary>True while the owning ViewModel rebuilds Options. The ComboBox's
        /// two-way SelectedIndex binding writes transient -1/0 values back while
        /// items are cleared and re-added; reacting to those would ping-pong
        /// RoleChanged → RaiseMockupPropertiesChanged → RebuildRoleOptions →
        /// Options.Clear → binding writeback → … and overflow the stack (this
        /// exact bug crashed the app at startup).</summary>
        private bool _suppressChangeNotification;

        /// <summary>Programmatic selection that must not re-enter the refresh path.</summary>
        public void SetSelectedOptionSilently(int value)
        {
            _suppressChangeNotification = true;
            try
            {
                SelectedOption = value;
            }
            finally
            {
                _suppressChangeNotification = false;
            }
        }

        partial void OnSelectedOptionChanged(int value)
        {
            // Fires during silent rebuilds too — it is a pure view flag, unlike RoleChanged.
            OnPropertyChanged(nameof(IsOverridden));
            if (!_suppressChangeNotification) RoleChanged?.Invoke();
        }
    }

    /// <summary>One bar in the chart scene (pixel height, already scaled).</summary>
    public sealed record ChartBar(double Height, IBrush Brush);

    /// <summary>Ctrl+2 · Preview — the palette rendered as a *context gallery*:
    ///
    ///   · Role mapping: assign palette colors to semantic roles (Primary, Accent,
    ///     Surface, Background, Text, Border) once; every scene follows the map,
    ///     and Auto keeps the old derived defaults.
    ///   · Scenes: Dashboard, Landing, Mobile, Chart and Poster — five real
    ///     contexts instead of one dashboard card, so a palette is judged where
    ///     it will actually be used.
    ///   · Surfaces derive through OKLCH mixing (MixOklch) so the washed-out
    ///     gray backgrounds of RGB mixing don't muddy the preview.
    ///
    /// The board layout (Row / Grid / List / Compact) is shared board state and
    /// stays on <see cref="MainWindowViewModel"/>. Variants and comparison are
    /// unchanged; see RaiseMockupPropertiesChanged for the refresh entry point.
    /// </summary>
    public partial class PreviewWorkspaceViewModel : ViewModelBase
    {
        private readonly MainWindowViewModel _shell;

        public PreviewWorkspaceViewModel(MainWindowViewModel shell)
        {
            _shell = shell;

            Roles = new ObservableCollection<MockupRole>
            {
                new() { Name = "Primary" },
                new() { Name = "Accent" },
                new() { Name = "Surface" },
                new() { Name = "Background" },
                new() { Name = "Text" },
                new() { Name = "Border" },
            };

            foreach (var role in Roles) role.RoleChanged += RaiseMockupPropertiesChanged;

            RebuildRoleOptions();
            RebuildChartBars();
        }

        public ObservableCollection<MockupRole> Roles { get; }

        // ---------------- Scenes ----------------

        public Array Scenes { get; } = Enum.GetValues(typeof(MockupScene));

        [ObservableProperty]
        private MockupScene _selectedScene = MockupScene.Dashboard;

        partial void OnSelectedSceneChanged(MockupScene value)
        {
            OnPropertyChanged(nameof(IsSceneDashboard));
            OnPropertyChanged(nameof(IsSceneLanding));
            OnPropertyChanged(nameof(IsSceneMobile));
            OnPropertyChanged(nameof(IsSceneChart));
            OnPropertyChanged(nameof(IsScenePoster));
        }

        public bool IsSceneDashboard => SelectedScene == MockupScene.Dashboard;
        public bool IsSceneLanding => SelectedScene == MockupScene.Landing;
        public bool IsSceneMobile => SelectedScene == MockupScene.Mobile;
        public bool IsSceneChart => SelectedScene == MockupScene.Chart;
        public bool IsScenePoster => SelectedScene == MockupScene.Poster;

        // ---------------- Role resolution ----------------

        private Color ResolveRole(int roleIndex, Color autoColor)
        {
            var option = Roles[roleIndex].SelectedOption;
            if (option < 1 || option > _shell.Palette.Count) return autoColor;
            return _shell.Palette[option - 1].Color;
        }

        private Color PaletteAt(int index, Color fallback) =>
            _shell.Palette.Count == 0
                ? fallback
                : _shell.Palette[Math.Clamp(index, 0, _shell.Palette.Count - 1)].Color;

        private Color LastColor => PaletteAt(_shell.Palette.Count - 1, Colors.White);

        // Auto defaults: primary = first swatch, accent = last, surfaces derived
        // from the accent through OKLCH (no muddy RGB midpoint), text = the
        // palette color with the best contrast on the resolved background.
        private Color AutoPrimary => PaletteAt(0, Colors.Gray);
        private Color AutoAccent => LastColor;
        private Color AutoBackground => ColorMathService.MixOklch(LastColor, Colors.White, 0.92);
        private Color AutoSurface => ColorMathService.MixOklch(LastColor, Colors.White, 0.98);

        private Color AutoText
        {
            get
            {
                if (_shell.Palette.Count == 0) return Color.FromRgb(0x1E, 0x20, 0x25);
                var background = ResolveRole(3, AutoBackground);
                return _shell.Palette
                    .Select(s => s.Color)
                    .OrderByDescending(c => ContrastService.ContrastRatio(c, background))
                    .First();
            }
        }

        private Color AutoBorder => ColorMathService.MixOklch(AutoText, AutoBackground, 0.88);

        public IBrush PrimaryBrush => new SolidColorBrush(ResolveRole(0, AutoPrimary));
        public IBrush AccentBrush => new SolidColorBrush(ResolveRole(1, AutoAccent));
        public IBrush SurfaceBrush => new SolidColorBrush(ResolveRole(2, AutoSurface));
        public IBrush BackgroundBrush => new SolidColorBrush(ResolveRole(3, AutoBackground));
        public IBrush TextBrush => new SolidColorBrush(ResolveRole(4, AutoText));
        public IBrush BorderBrush => new SolidColorBrush(ResolveRole(5, AutoBorder));

        public IBrush PrimaryForegroundBrush => new SolidColorBrush(ReadableOn(ResolveRole(0, AutoPrimary)));
        public IBrush AccentForegroundBrush => new SolidColorBrush(ReadableOn(ResolveRole(1, AutoAccent)));

        /// <summary>Muted body copy: the resolved text color at reduced strength on the
        /// resolved background, so secondary text can be judged too.</summary>
        public IBrush MutedTextBrush =>
            new SolidColorBrush(ColorMathService.MixOklch(ResolveRole(4, AutoText), ResolveRole(3, AutoBackground), 0.35));

        private static Color ReadableOn(Color color) =>
            (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0 > 0.6
                ? Color.FromRgb(0x1E, 0x20, 0x25)
                : Colors.White;

        // ---------------- Chart scene data ----------------

        public ObservableCollection<ChartBar> ChartBars { get; } = new();

        private static readonly double[] ChartHeights = { 0.55, 0.82, 0.38, 1.0, 0.64, 0.76 };

        private void RebuildChartBars()
        {
            ChartBars.Clear();
            if (_shell.Palette.Count == 0) return;

            for (var i = 0; i < ChartHeights.Length; i++)
            {
                var color = _shell.Palette[i % _shell.Palette.Count].Color;
                ChartBars.Add(new ChartBar(ChartHeights[i] * 150, new SolidColorBrush(color)));
            }
        }

        // ---------------- Role option rebuilding ----------------

        /// <summary>Guards the whole refresh against reentrancy: rebuilding options
        /// mutates collections the ComboBox two-way bindings write back into, and
        /// without this guard that feedback loop recursed until the stack died.</summary>
        private bool _isRefreshing;

        private void RebuildRoleOptions()
        {
            foreach (var role in Roles)
            {
                var previous = role.SelectedOption;
                role.Options.Clear();
                // Placeholder: the chip is patched in the second pass, once every
                // selection has been restored (Auto's color may depend on them).
                role.Options.Add(new MockupOption("Auto", Colors.Transparent, isAuto: true));
                foreach (var swatch in _shell.Palette)
                    role.Options.Add(new MockupOption(swatch.Name, swatch.Color));

                // Keep the assignment when possible; fall back to Auto when the
                // palette shrank below the stored index. Silent: the binding
                // writebacks during Clear/Add above must not re-enter here.
                role.SetSelectedOptionSilently(previous <= _shell.Palette.Count ? previous : 0);
            }

            // Second pass: selections are final, so the derived defaults — which
            // read them (Text contrasts against the resolved Background, Border
            // mixes Text into Background) — can now be previewed accurately. Patched
            // in place so the ComboBox's collection never changes here.
            for (var i = 0; i < Roles.Count; i++)
                Roles[i].Options[0].Update(AutoColorFor(i));
        }

        /// <summary>The color role <paramref name="roleIndex"/> falls back to when it is Auto.</summary>
        private Color AutoColorFor(int roleIndex) => roleIndex switch
        {
            0 => AutoPrimary,
            1 => AutoAccent,
            2 => AutoSurface,
            3 => AutoBackground,
            4 => AutoText,
            _ => AutoBorder,
        };

        /// <summary>Called by the shell whenever the palette or any swatch color changes
        /// so the role options, mockup brushes and chart follow. Reentrant-safe:
        /// collection/binding side effects during the rebuild may call back in.</summary>
        public void RaiseMockupPropertiesChanged()
        {
            if (_isRefreshing) return;
            _isRefreshing = true;
            try
            {
                RebuildRoleOptions();
                RebuildChartBars();

                OnPropertyChanged(nameof(PrimaryBrush));
                OnPropertyChanged(nameof(PrimaryForegroundBrush));
                OnPropertyChanged(nameof(AccentBrush));
                OnPropertyChanged(nameof(AccentForegroundBrush));
                OnPropertyChanged(nameof(SurfaceBrush));
                OnPropertyChanged(nameof(BackgroundBrush));
                OnPropertyChanged(nameof(TextBrush));
                OnPropertyChanged(nameof(BorderBrush));
                OnPropertyChanged(nameof(MutedTextBrush));
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        /// <summary>§8 "Dark / light mode variant generator": derives a lighter or darker
        /// sibling palette from the current one and opens it.</summary>
        [RelayCommand]
        private void GenerateLightVariant() => GenerateVariant(light: true);

        [RelayCommand]
        private void GenerateDarkVariant() => GenerateVariant(light: false);

        private void GenerateVariant(bool light)
        {
            if (_shell.Palette.Count == 0)
            {
                _shell.StatusMessage = "Add some colors before generating a variant.";
                return;
            }

            var baseName = PaletteLibraryService.FindPalette(_shell.ActivePaletteId)?.Name ?? "Palette";
            var suffix = light ? "Light" : "Dark";
            var swatches = _shell.Palette.Select(s =>
            {
                var derived = light
                    ? ColorMathService.MixOklch(s.Color, Colors.White, 0.30)
                    : ColorMathService.MixOklch(s.Color, Colors.Black, 0.35);
                return new PaletteSwatchDto(
                    light ? $"{s.Name} tint" : $"{s.Name} shade",
                    derived.A, derived.R, derived.G, derived.B, s.IsLocked);
            }).ToList();

            var dto = PaletteLibraryService.AddPalette($"{baseName} ({suffix})", swatches);
            _shell.Library.AddAndSelect(dto);
            _shell.StatusMessage = $"Generated {suffix.ToLowerInvariant()} mode variant with {swatches.Count} colors.";
        }

        /// <summary>§8 "Side-by-side palette comparison".</summary>
        [RelayCommand]
        private async Task ComparePalettesAsync()
        {
            if (MainWindowViewModel.GetOwnerWindow() is not { } owner) return;

            var current = PaletteLibraryService.FindPalette(_shell.ActivePaletteId)
                          ?? PaletteLibraryService.Library.Palettes.FirstOrDefault();

            var window = new Views.CompareWindow(current);
            await window.ShowDialog(owner);
        }
    }
}
