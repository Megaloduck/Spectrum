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
    /// <summary>Which context the Preview gallery is rendering the palette into.</summary>
    public enum MockupScene
    {
        Dashboard,
        Landing,
        Mobile,
        Chart,
        Poster,
    }

    /// <summary>Which canvas the scenes are painted on. Deliberately separate from
    /// the app's own theme: the gallery is the thing being judged, so its
    /// light/dark choice must not be the app chrome's choice.</summary>
    public enum PreviewTheme
    {
        Light,
        Dark,
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

        /// <summary>What this role paints in the scenes — shown as a tooltip so the
        /// dock can stay to a single row per role.</summary>
        public string Hint { get; init; } = string.Empty;

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

    /// <summary>One bar in the chart scene: its axis label, the value printed above
    /// the bar, the pixel height (already scaled) and the palette color it encodes.
    /// <c>LabelBrush</c> carries the resolved Text role so the axis follows the map
    /// instead of reaching up the visual tree with an ancestor binding.</summary>
    public sealed record ChartBar(string Label, string Value, double Height, IBrush Brush, IBrush LabelBrush);

    /// <summary>One slice of the channel-mix bar, with an already-scaled pixel width.</summary>
    public sealed record ChartSegment(string Label, string Value, double Width, IBrush Brush, IBrush LabelBrush, IBrush MutedBrush);

    /// <summary>One row of the ranked-pages chart: the track uses the Border role and
    /// the fill the palette color, both resolved through the role map.</summary>
    public sealed record RankBar(string Label, string Value, double Width, IBrush Brush, IBrush TrackBrush, IBrush LabelBrush, IBrush MutedBrush);

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
    ///   · Light / dark: the same map is rendered on a near-white or near-black
    ///     canvas tinted with the accent, so both modes can be judged in place.
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
                new() { Name = "Primary", Hint = "Filled buttons, links, avatars and the active tab" },
                new() { Name = "Accent", Hint = "Badges, status chips and the secondary action" },
                new() { Name = "Surface", Hint = "Cards, list panels and fields that sit on the page" },
                new() { Name = "Background", Hint = "The page canvas every scene is painted on" },
                new() { Name = "Text", Hint = "Headlines, body copy and axis labels" },
                new() { Name = "Border", Hint = "Hairlines, card edges and gridlines" },
            };

            foreach (var role in Roles) role.RoleChanged += RaiseMockupPropertiesChanged;

            RebuildRoleOptions();
            RebuildCharts();
        }

        public ObservableCollection<MockupRole> Roles { get; }

        /// <summary>True while any role is pinned to a palette color — drives the
        /// dock's "Reset all" action.</summary>
        public bool HasRoleOverrides => Roles.Any(r => r.IsOverridden);

        /// <summary>Snaps every role back to Auto in a single refresh instead of
        /// one rebuild per role.</summary>
        [RelayCommand]
        private void ResetAllRoles()
        {
            if (!HasRoleOverrides) return;

            _isRefreshing = true;
            try
            {
                // Silent on purpose: the loop shouldn't run six refreshes, and the
                // explicit one below covers every role at once.
                foreach (var role in Roles) role.SetSelectedOptionSilently(0);
            }
            finally
            {
                _isRefreshing = false;
            }

            RaiseMockupPropertiesChanged();
            _shell.StatusMessage = "All roles are back to their derived defaults.";
        }

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
            OnPropertyChanged(nameof(SceneTitle));
            OnPropertyChanged(nameof(SceneHint));
        }

        public bool IsSceneDashboard => SelectedScene == MockupScene.Dashboard;
        public bool IsSceneLanding => SelectedScene == MockupScene.Landing;
        public bool IsSceneMobile => SelectedScene == MockupScene.Mobile;
        public bool IsSceneChart => SelectedScene == MockupScene.Chart;
        public bool IsScenePoster => SelectedScene == MockupScene.Poster;

        /// <summary>Scene tabs on the toolbar — a named command keeps the segment
        /// buttons declarative with a CommandParameter.</summary>
        [RelayCommand]
        private void SelectScene(MockupScene scene) => SelectedScene = scene;

        /// <summary>Scene name shown above the gallery, outside the mockup.</summary>
        public string SceneTitle => SelectedScene switch
        {
            MockupScene.Dashboard => "Dashboard",
            MockupScene.Landing => "Landing",
            MockupScene.Mobile => "Mobile",
            MockupScene.Chart => "Chart",
            _ => "Poster",
        };

        /// <summary>What to look at in this scene — guidance lives here, not inside
        /// the mockup, so the preview never reads like a tutorial about itself.</summary>
        public string SceneHint => SelectedScene switch
        {
            MockupScene.Dashboard =>
                "Surface cards, hairline borders and a primary action on the page background — check the button label and the card edges.",
            MockupScene.Landing =>
                "Big type on the page background and a primary call to action — check the hero headline and the nav at a glance.",
            MockupScene.Mobile =>
                "A small screen: list rows on a surface card with muted metadata and a primary tab. Check legibility at 10–12 px.",
            MockupScene.Chart =>
                "Four charts on one page — bars, a channel split, ranked pages and a trend line. Check that neighbouring colors stay distinguishable and that the axis and legend text read on the surface.",
            _ =>
                "Primary becomes the field and the on-primary color becomes the voice — check the small print and the date chip.",
        };

        /// <summary>Drives the gallery ↔ empty-state swap: Auto fallbacks would
        /// otherwise render a plausible grey mockup for an empty palette.</summary>
        public bool HasPalette => _shell.Palette.Count > 0;

        // ---------------- Light / dark theme ----------------

        /// <summary>Light or dark canvas for the scenes. Switching it changes the
        /// derived Background, Surface, Text and Border roles, so the same palette
        /// can be judged in both modes without generating a second palette.</summary>
        [ObservableProperty]
        private PreviewTheme _theme = PreviewTheme.Light;

        partial void OnThemeChanged(PreviewTheme value)
        {
            OnPropertyChanged(nameof(IsLightTheme));
            OnPropertyChanged(nameof(IsDarkTheme));
            OnPropertyChanged(nameof(ThemeName));
            OnPropertyChanged(nameof(ThemeCaption));
            RaiseMockupPropertiesChanged();
        }

        public bool IsLightTheme => Theme == PreviewTheme.Light;
        public bool IsDarkTheme => Theme == PreviewTheme.Dark;

        public string ThemeName => IsDarkTheme ? "Dark preview" : "Light preview";

        public string ThemeCaption => IsDarkTheme
            ? "Auto derives the canvas from your accent and keeps surfaces a step lighter; pick a color below to pin it."
            : "Auto derives the canvas from your accent and keeps surfaces a step darker; pick a color below to pin it.";

        [RelayCommand]
        private void SelectTheme(PreviewTheme theme) => Theme = theme;

        [RelayCommand]
        private void ToggleTheme() => Theme = IsDarkTheme ? PreviewTheme.Light : PreviewTheme.Dark;

        // ---------------- Canvas colors ----------------

        /// <summary>Optional canvas color for the light theme. Null keeps the canvas
        /// derived from the palette, so a separate override is only paid for when
        /// the user asks for one.</summary>
        [ObservableProperty]
        private Color? _lightCanvasColor;

        [ObservableProperty]
        private Color? _darkCanvasColor;

        partial void OnLightCanvasColorChanged(Color? value) => RaiseCanvasPropertiesChanged();

        partial void OnDarkCanvasColorChanged(Color? value) => RaiseCanvasPropertiesChanged();

        private void RaiseCanvasPropertiesChanged()
        {
            OnPropertyChanged(nameof(HasLightCanvasColor));
            OnPropertyChanged(nameof(HasDarkCanvasColor));
            OnPropertyChanged(nameof(LightCanvasSwatch));
            OnPropertyChanged(nameof(DarkCanvasSwatch));
            OnPropertyChanged(nameof(LightCanvasHex));
            OnPropertyChanged(nameof(DarkCanvasHex));
            RaiseMockupPropertiesChanged();
        }

        public bool HasLightCanvasColor => LightCanvasColor.HasValue;
        public bool HasDarkCanvasColor => DarkCanvasColor.HasValue;

        public IBrush LightCanvasSwatch => new SolidColorBrush(LightCanvasColor ?? DerivedLightBackground);
        public IBrush DarkCanvasSwatch => new SolidColorBrush(DarkCanvasColor ?? DerivedDarkBackground);

        public string LightCanvasHex => ColorMathService.ToHex(LightCanvasColor ?? DerivedLightBackground);
        public string DarkCanvasHex => ColorMathService.ToHex(DarkCanvasColor ?? DerivedDarkBackground);

        /// <summary>Opens the shared spectrum dialog; the picked color becomes the
        /// light canvas for the scenes (and survives switching to dark and back).</summary>
        [RelayCommand]
        private async Task PickLightCanvasAsync()
        {
            var picked = await MainWindowViewModel.PickColorAsync(LightCanvasColor ?? DerivedLightBackground);
            if (picked is null) return;

            LightCanvasColor = picked.Value;
            _shell.StatusMessage = "Light canvas color updated.";
        }

        [RelayCommand]
        private async Task PickDarkCanvasAsync()
        {
            var picked = await MainWindowViewModel.PickColorAsync(DarkCanvasColor ?? DerivedDarkBackground);
            if (picked is null) return;

            DarkCanvasColor = picked.Value;
            _shell.StatusMessage = "Dark canvas color updated.";
        }

        [RelayCommand]
        private void ResetLightCanvas() => LightCanvasColor = null;

        [RelayCommand]
        private void ResetDarkCanvas() => DarkCanvasColor = null;

        // ---------------- Role resolution ----------------

        /// <summary>Every color the scenes draw goes through the color-blind simulation
        /// (when one is selected in Analyze), exactly like the swatch cards do — a
        /// preview that ignored the setting would flatter the palette.</summary>
        private Color Simulated(Color color) => ColorBlindnessService.Simulate(color, _shell.SelectedColorBlindMode);

        private Color ResolveRole(int roleIndex, Color autoColor)
        {
            var option = Roles[roleIndex].SelectedOption;
            if (option < 1 || option > _shell.Palette.Count) return autoColor;
            return Simulated(_shell.Palette[option - 1].Color);
        }

        private Color PaletteAt(int index, Color fallback) =>
            _shell.Palette.Count == 0
                ? fallback
                : Simulated(_shell.Palette[Math.Clamp(index, 0, _shell.Palette.Count - 1)].Color);

        private Color LastColor => PaletteAt(_shell.Palette.Count - 1, Colors.White);

        // Auto defaults: primary = first swatch, accent = last, surfaces derived
        // from the accent through OKLCH (no muddy RGB midpoint), text = the
        // palette color with the best contrast on the resolved background.
        // Every derived surface follows the preview theme: mixing toward white
        // gives the light canvas, toward black the dark one, so both keep the
        // palette's hue instead of turning into neutral grey.
        private Color AutoPrimary => PaletteAt(0, Colors.Gray);
        private Color AutoAccent => LastColor;

        private Color DerivedLightBackground => ColorMathService.MixOklch(LastColor, Colors.White, 0.92);
        private Color DerivedDarkBackground => DarkCanvas(0.17, 0.02);
        private Color DerivedLightSurface => ColorMathService.MixOklch(LastColor, Colors.White, 0.98);
        private Color DerivedDarkSurface => DarkCanvas(0.23, 0.03);

        private Color AutoBackground => IsDarkTheme
            ? DarkCanvasColor ?? DerivedDarkBackground
            : LightCanvasColor ?? DerivedLightBackground;

        private Color AutoSurface => IsDarkTheme
            ? (DarkCanvasColor is { } dark ? StepFromCanvas(dark, lighten: true) : DerivedDarkSurface)
            : (LightCanvasColor is { } light ? StepFromCanvas(light, lighten: false) : DerivedLightSurface);

        /// <summary>Cards must separate from a custom canvas even when the user picks
        /// pure white or pure black, so with an override the surface moves a small
        /// step away from the canvas instead of toward white/black — a mix toward
        /// white would collapse into the canvas at #FFFFFF and make cards vanish.</summary>
        private static Color StepFromCanvas(Color canvas, bool lighten) =>
            ColorMathService.MixOklch(canvas, lighten ? Colors.White : Colors.Black, lighten ? 0.07 : 0.045);

        /// <summary>A near-black canvas at a fixed OKLCH lightness that keeps the
        /// palette's hue and a trace of its chroma. Mixing toward black
        /// proportionally — which is what the light theme does toward white —
        /// would make a bright palette almost pure black and a dark palette
        /// indistinguishable from it, so the dark step is set outright.</summary>
        private Color DarkCanvas(double lightness, double maxChroma)
        {
            if (_shell.Palette.Count == 0) return Color.FromRgb(0x12, 0x13, 0x16);

            var ok = ColorMathService.RgbToOklch(LastColor);
            return ColorMathService.OklchToRgb(new OklchColor(
                lightness,
                Math.Min(ok.C, maxChroma),
                ok.H));
        }

        private static Color LightInk => Color.FromRgb(0x1E, 0x20, 0x25);

        private Color AutoText
        {
            get
            {
                if (_shell.Palette.Count == 0)
                    return IsDark(AutoBackground) ? Colors.White : LightInk;

                var background = ResolveRole(3, AutoBackground);
                var best = _shell.Palette
                    .Select(s => Simulated(s.Color))
                    .OrderByDescending(c => ContrastService.ContrastRatio(c, background))
                    .First();

                // A palette made of near-background colors would otherwise paint
                // the whole scene in an unreadable tone — e.g. a dark palette in
                // dark mode, or a light palette on a dark custom canvas. 3:1 is the
                // large-text bar; below that the ink that reads on this background
                // stands in, and an explicit role mapping still wins.
                return ContrastService.ContrastRatio(best, background) >= 3.0
                    ? best
                    : IsDark(background) ? Colors.White : LightInk;
            }
        }

        private Color AutoBorder => ColorMathService.MixOklch(
            AutoText, AutoBackground, IsDark(AutoBackground) ? 0.80 : 0.88);

        public IBrush PrimaryBrush => new SolidColorBrush(ResolveRole(0, AutoPrimary));
        public IBrush AccentBrush => new SolidColorBrush(ResolveRole(1, AutoAccent));
        public IBrush SurfaceBrush => new SolidColorBrush(ResolveRole(2, AutoSurface));
        public IBrush BackgroundBrush => new SolidColorBrush(ResolveRole(3, AutoBackground));
        public IBrush TextBrush => new SolidColorBrush(ResolveRole(4, AutoText));
        public IBrush BorderBrush => new SolidColorBrush(ResolveRole(5, AutoBorder));

        public IBrush PrimaryForegroundBrush => new SolidColorBrush(ReadableOn(ResolveRole(0, AutoPrimary)));
        public IBrush AccentForegroundBrush => new SolidColorBrush(ReadableOn(ResolveRole(1, AutoAccent)));

        /// <summary>Secondary copy on a Primary field (the Poster scene). Derived by
        /// mixing the on-primary color toward Primary so it is a real color the
        /// palette owns — not the resolved color dimmed with Opacity, which would
        /// make the preview lie about contrast.</summary>
        public IBrush PrimaryMutedForegroundBrush
        {
            get
            {
                var primary = ResolveRole(0, AutoPrimary);
                return new SolidColorBrush(ColorMathService.MixOklch(ReadableOn(primary), primary, 0.22));
            }
        }

        /// <summary>Muted body copy: the resolved text color at reduced strength on the
        /// resolved background, so secondary text can be judged too.</summary>
        public IBrush MutedTextBrush =>
            new SolidColorBrush(ColorMathService.MixOklch(ResolveRole(4, AutoText), ResolveRole(3, AutoBackground), 0.35));

        /// <summary>W3C relative-luminance test, the same one the app's contrast
        /// readouts use — a custom canvas can invert the theme, so "dark" has to be
        /// measured on the color actually in play rather than on the theme name.</summary>
        private static bool IsDark(Color color) =>
            (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0 < 0.45;

        /// <summary>Ink for a filled field (buttons, badges). The 0.6 threshold is
        /// deliberately higher than <see cref="IsDark"/>: on a colored fill a light
        /// ink reads better than on a page canvas.</summary>
        private static Color ReadableOn(Color color) =>
            (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0 > 0.6
                ? LightInk
                : Colors.White;

        // ---------------- Chart scene data ----------------

        public ObservableCollection<ChartBar> ChartBars { get; } = new();
        public ObservableCollection<ChartSegment> ChannelSegments { get; } = new();
        public ObservableCollection<RankBar> TopPages { get; } = new();

        /// <summary>Trend line and area as built geometry (fixed 300 × 108 canvas).
        /// Geometry rather than a point list keeps the shape exact and lets the
        /// scene bind one property each, with no list-type guesswork at runtime.</summary>
        [ObservableProperty]
        private Geometry _trendLineGeometry = new StreamGeometry();

        [ObservableProperty]
        private Geometry _trendAreaGeometry = new StreamGeometry();

        public IBrush TrendStrokeBrush => new SolidColorBrush(PaletteAt(0, Colors.Gray));

        /// <summary>Area fill is a real derived color (Primary worked into the page),
        /// not the stroke at reduced opacity — the preview shouldn't show a tone the
        /// palette doesn't own.</summary>
        public IBrush TrendAreaBrush => new SolidColorBrush(ColorMathService.MixOklch(
            PaletteAt(0, Colors.Gray), ResolveRole(3, AutoBackground), 0.78));

        private static readonly double[] ChartHeights = { 0.55, 0.82, 0.38, 1.0, 0.64, 0.76 };

        /// <summary>Plot height in pixels; the tallest bar fills it exactly.</summary>
        private const double ChartPlotHeight = 140;

        /// <summary>Seven-day session trend, normalised 0–1 so it fits any palette.</summary>
        private static readonly double[] TrendValues = { 0.46, 0.60, 0.52, 0.78, 0.69, 0.92, 1.0 };

        private static readonly (string Label, string Value, double Share)[] ChannelMix =
        {
            ("Organic", "46%", 0.46),
            ("Referral", "31%", 0.31),
            ("Paid", "23%", 0.23),
        };

        private static readonly (string Label, string Value, double Share)[] RankedPages =
        {
            ("/pricing", "8.4k", 1.0),
            ("/docs", "6.1k", 0.73),
            ("/blog", "3.9k", 0.46),
            ("/changelog", "2.2k", 0.26),
        };

        // Fixed chart geometry: readable at both the two-card width and full width,
        // and it keeps the polyline points exact instead of guessing a stretch.
        private const double ChannelTrackWidth = 220;
        private const double RankTrackWidth = 190;
        private const double TrendCanvasWidth = 300;
        private const double TrendCanvasHeight = 108;

        private void RebuildCharts()
        {
            ChartBars.Clear();
            ChannelSegments.Clear();
            TopPages.Clear();

            if (_shell.Palette.Count == 0)
            {
                TrendLineGeometry = new StreamGeometry();
                TrendAreaGeometry = new StreamGeometry();
                return;
            }

            // Axis and legend text follow the Text role, like every other string in
            // the scene; the ranked-bar track follows Border.
            var labelBrush = new SolidColorBrush(ResolveRole(4, AutoText));
            var mutedBrush = new SolidColorBrush(ColorMathService.MixOklch(
                ResolveRole(4, AutoText), ResolveRole(3, AutoBackground), 0.35));
            var trackBrush = new SolidColorBrush(ResolveRole(5, AutoBorder));
            var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < ChartHeights.Length; i++)
            {
                var swatch = _shell.Palette[i % _shell.Palette.Count];
                var label = swatch.Name;

                // Duplicate swatch names would make two bars share an axis label.
                if (used.TryGetValue(label, out var seen))
                {
                    used[label] = ++seen;
                    label = $"{label} {seen}";
                }
                else
                {
                    used[label] = 1;
                }

                ChartBars.Add(new ChartBar(
                    label,
                    $"{ChartHeights[i] * 100:N0}%",
                    ChartHeights[i] * ChartPlotHeight,
                    new SolidColorBrush(Simulated(swatch.Color)),
                    labelBrush));
            }

            // Channel mix: a stacked bar plus its legend, one palette color per slice.
            for (var i = 0; i < ChannelMix.Length; i++)
            {
                var (label, value, share) = ChannelMix[i];
                ChannelSegments.Add(new ChartSegment(
                    label,
                    value,
                    Math.Round(share * ChannelTrackWidth),
                    new SolidColorBrush(Simulated(_shell.Palette[i % _shell.Palette.Count].Color)),
                    labelBrush,
                    mutedBrush));
            }

            // Ranked pages: horizontal bars on a Border-role track.
            for (var i = 0; i < RankedPages.Length; i++)
            {
                var (label, value, share) = RankedPages[i];
                TopPages.Add(new RankBar(
                    label,
                    value,
                    Math.Round(share * RankTrackWidth),
                    new SolidColorBrush(Simulated(_shell.Palette[(i + 1) % _shell.Palette.Count].Color)),
                    trackBrush,
                    labelBrush,
                    mutedBrush));
            }

            // Weekly trend line, plus the closed polygon that fills the area under it.
            var points = new List<Point>(TrendValues.Length);
            for (var i = 0; i < TrendValues.Length; i++)
            {
                var x = i * TrendCanvasWidth / (TrendValues.Length - 1);
                var y = 4 + (1 - TrendValues[i]) * (TrendCanvasHeight - 8);
                points.Add(new Point(x, y));
            }

            var line = new StreamGeometry();
            using (var ctx = line.Open())
            {
                ctx.BeginFigure(points[0], isFilled: false);
                for (var i = 1; i < points.Count; i++) ctx.LineTo(points[i]);
                ctx.EndFigure(false);
            }

            var area = new StreamGeometry();
            using (var ctx = area.Open())
            {
                ctx.BeginFigure(new Point(0, TrendCanvasHeight - 4), isFilled: true);
                foreach (var point in points) ctx.LineTo(point);
                ctx.LineTo(new Point(TrendCanvasWidth, TrendCanvasHeight - 4));
                ctx.EndFigure(true);
            }

            TrendLineGeometry = line;
            TrendAreaGeometry = area;
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
                RebuildCharts();

                OnPropertyChanged(nameof(HasPalette));
                OnPropertyChanged(nameof(HasRoleOverrides));
                OnPropertyChanged(nameof(PrimaryBrush));
                OnPropertyChanged(nameof(PrimaryForegroundBrush));
                OnPropertyChanged(nameof(PrimaryMutedForegroundBrush));
                OnPropertyChanged(nameof(AccentBrush));
                OnPropertyChanged(nameof(AccentForegroundBrush));
                OnPropertyChanged(nameof(SurfaceBrush));
                OnPropertyChanged(nameof(BackgroundBrush));
                OnPropertyChanged(nameof(TextBrush));
                OnPropertyChanged(nameof(BorderBrush));
                OnPropertyChanged(nameof(MutedTextBrush));
                OnPropertyChanged(nameof(TrendStrokeBrush));
                OnPropertyChanged(nameof(TrendAreaBrush));
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
