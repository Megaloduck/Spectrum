using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spectrum.Models;
using Spectrum.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Spectrum.ViewModels
{
    /// <summary>
    /// Ctrl+1 · Studio — the color panel (HSL + temperature + opacity), recent
    /// picks, the eyedroppers, the harmony generator and the gradient studio.
    /// The shared board, undo history and library stay on
    /// <see cref="MainWindowViewModel"/>, which owns this instance.
    /// </summary>
    public partial class StudioWorkspaceViewModel : ViewModelBase
    {
        private const int DefaultPaletteSize = 5;

        private readonly MainWindowViewModel _shell;

        public StudioWorkspaceViewModel(MainWindowViewModel shell) => _shell = shell;

        /// <summary>Raised whenever <see cref="BaseColor"/> changes so the Analyze
        /// workspace can refresh its conversion and contrast readouts.</summary>
        public event Action? BaseColorChanged;

        public Array HarmonyTypes { get; } = Enum.GetValues(typeof(HarmonyType));

        // ---- Base color, expressed as Hue/Saturation/Lightness + a
        // ---- Temperature tint applied on top, matching the "COLOR PANEL" sliders.
        [ObservableProperty]
        private double _hue = 9;

        [ObservableProperty]
        private double _saturation = 100;

        [ObservableProperty]
        private double _lightness = 64;

        [ObservableProperty]
        private double _temperature; // -100 (cool) .. 100 (warm), 0 = neutral

        [ObservableProperty]
        private double _alpha = 100; // 0 (transparent) .. 100 (opaque), applied to BaseColor

        [ObservableProperty]
        private HarmonyType _selectedHarmony = HarmonyType.Random;

        public Color BaseColor
        {
            get
            {
                var hslColor = ColorHarmonyService.FromHsl(Hue, Saturation / 100.0, Lightness / 100.0);
                var tinted = ApplyTemperature(hslColor, Temperature);
                return Color.FromArgb(ToByte(Alpha / 100.0 * 255.0), tinted.R, tinted.G, tinted.B);
            }
        }

        public IBrush BaseColorBrush => new SolidColorBrush(BaseColor);

        public string BaseColorName => ColorNamingService.GetClosestName(BaseColor);

        public IBrush BaseForegroundBrush =>
            (0.2126 * BaseColor.R + 0.7152 * BaseColor.G + 0.0722 * BaseColor.B) / 255.0 > 0.6
                ? new SolidColorBrush(Color.FromRgb(0x1E, 0x20, 0x25))
                : Brushes.White;

        public string BaseHex
        {
            get => $"#{BaseColor.R:X2}{BaseColor.G:X2}{BaseColor.B:X2}";
            set
            {
                if (!TryParseHex(value, out var color)) return;

                var (h, s, l) = ColorHarmonyService.ToHsl(color);
                Hue = h;
                Saturation = s * 100.0;
                Lightness = l * 100.0;
                Temperature = 0;
            }
        }

        // Percentage label for the Opacity slider's caption (e.g. "72%").
        public string AlphaLabel => $"{Alpha:N0}%";

        // ---- Gradient track backgrounds for the four sliders. Hue,
        // ---- Brightness and Temperature are fixed; Saturation's end color
        // ---- depends on the current Hue, so it's recomputed whenever that changes.
        public IBrush HueTrackBrush { get; } = CreateHorizontalGradient(
            (Color.FromRgb(255, 0, 0), 0.0),
            (Color.FromRgb(255, 255, 0), 1.0 / 6),
            (Color.FromRgb(0, 255, 0), 2.0 / 6),
            (Color.FromRgb(0, 255, 255), 3.0 / 6),
            (Color.FromRgb(0, 0, 255), 4.0 / 6),
            (Color.FromRgb(255, 0, 255), 5.0 / 6),
            (Color.FromRgb(255, 0, 0), 1.0));

        public IBrush SaturationTrackBrush => CreateHorizontalGradient(
            (Colors.White, 0.0),
            (ColorHarmonyService.FromHsl(Hue, 1.0, 0.5), 1.0));

        public IBrush BrightnessTrackBrush { get; } = CreateHorizontalGradient(
            (Colors.Black, 0.0),
            (Colors.White, 1.0));

        public IBrush TemperatureTrackBrush { get; } = CreateHorizontalGradient(
            (Color.FromRgb(0x4A, 0x90, 0xE2), 0.0),
            (Color.FromRgb(0xF5, 0xA6, 0x23), 1.0));

        // Unlike the other tracks, this one depends on the live base
        // color (so the gradient always fades *that* hue from
        // transparent to opaque) rather than a fixed set of stops, so
        // it's a plain computed property kept in sync via
        // RaiseColorPropertiesChanged instead of a field initializer.
        public IBrush AlphaTrackBrush => CreateHorizontalGradient(
            (Color.FromArgb(0, BaseColor.R, BaseColor.G, BaseColor.B), 0.0),
            (Color.FromArgb(255, BaseColor.R, BaseColor.G, BaseColor.B), 1.0));

        partial void OnHueChanged(double value)
        {
            RaiseColorPropertiesChanged();
            OnPropertyChanged(nameof(SaturationTrackBrush));
        }

        partial void OnSaturationChanged(double value) => RaiseColorPropertiesChanged();
        partial void OnLightnessChanged(double value) => RaiseColorPropertiesChanged();
        partial void OnTemperatureChanged(double value) => RaiseColorPropertiesChanged();

        partial void OnAlphaChanged(double value)
        {
            OnPropertyChanged(nameof(AlphaLabel));
            RaiseColorPropertiesChanged();
        }

        private void RaiseColorPropertiesChanged()
        {
            OnPropertyChanged(nameof(BaseColor));
            OnPropertyChanged(nameof(BaseColorBrush));
            OnPropertyChanged(nameof(BaseColorName));
            OnPropertyChanged(nameof(BaseForegroundBrush));
            OnPropertyChanged(nameof(BaseHex));
            OnPropertyChanged(nameof(AlphaTrackBrush));
            BaseColorChanged?.Invoke();
        }

        private void ApplyToBaseColor(Color color)
        {
            var (h, s, l) = ColorHarmonyService.ToHsl(color);
            Hue = h;
            Saturation = s * 100.0;
            Lightness = l * 100.0;
            Temperature = 0;
            Alpha = color.A / 255.0 * 100.0;
        }

        private static Color ApplyTemperature(Color c, double temperature)
        {
            // A simple photo-style temperature shift: push red up / blue down
            // for "warm", or the reverse for "cool". Purely a visual tint on
            // top of the HSL-derived color, not a physical color-temperature model.
            var t = Math.Clamp(temperature, -100, 100) / 100.0;
            var r = c.R + t * 40;
            var b = c.B - t * 40;
            return Color.FromRgb(ToByte(r), c.G, ToByte(b));
        }

        private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);

        private static bool TryParseHex(string? input, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(input)) return false;

            var text = input.Trim().TrimStart('#');
            if (text.Length != 6) return false;

            if (byte.TryParse(text.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
                byte.TryParse(text.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
                byte.TryParse(text.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            {
                color = Color.FromRgb(r, g, b);
                return true;
            }

            return false;
        }

        private static LinearGradientBrush CreateHorizontalGradient(params (Color Color, double Offset)[] stops)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative)
            };

            foreach (var (color, offset) in stops)
            {
                brush.GradientStops.Add(new GradientStop(color, offset));
            }

            return brush;
        }

        // ---------------- Picking (§3): screen, image, recent history ----------------

        public ObservableCollection<RecentPick> RecentPicks { get; } = new();

        private const int MaxRecentPicks = 20;

        /// <summary>Prevents two windows' hotkey handlers opening two pickers at once.</summary>
        private static bool ScreenPickerOpen;

        /// <summary>§3 "System-wide eyedropper" — click anywhere on screen to capture the color.</summary>
        [RelayCommand]
        private async Task PickFromScreenAsync()
        {
            if (ScreenPickerOpen) return;
            if (!ScreenPickerService.IsSupported)
            {
                _shell.StatusMessage = "Screen picking uses native Windows interop — not available on this OS yet.";
                return;
            }

            if (MainWindowViewModel.GetOwnerWindow() is not { } owner) return;

            ScreenPickerOpen = true;
            try
            {
                var picker = new Views.ScreenPickerWindow();
                var color = await picker.ShowDialog<Color?>(owner);
                if (color is null)
                {
                    _shell.StatusMessage = "Screen pick cancelled.";
                    return;
                }

                ApplyPickedColor(color.Value, "screen");
            }
            finally
            {
                ScreenPickerOpen = false;
            }
        }

        /// <summary>§3 "In-app eyedropper (pick from loaded image)".</summary>
        [RelayCommand]
        private async Task PickFromImageAsync()
        {
            var bitmap = await FilePickerHelper.PickImageAsync();
            if (bitmap is null)
            {
                _shell.StatusMessage = "No image selected.";
                return;
            }

            if (MainWindowViewModel.GetOwnerWindow() is not { } owner)
            {
                bitmap.Dispose();
                return;
            }

            var picker = new Views.ImageEyedropperWindow(bitmap);
            var color = await picker.ShowDialog<Color?>(owner);
            if (color is null)
            {
                _shell.StatusMessage = "Eyedropper cancelled.";
                return;
            }

            ApplyPickedColor(color.Value, "image");
        }

        /// <summary>Applies a picked color to the color panel and records it in the history.</summary>
        private void ApplyPickedColor(Color color, string source)
        {
            ApplyToBaseColor(color);

            // Newest first, no duplicates, capped at 20 (§3 history).
            var existing = RecentPicks.FirstOrDefault(p => p.Color == color);
            if (existing is not null) RecentPicks.Remove(existing);
            RecentPicks.Insert(0, new RecentPick(color));
            while (RecentPicks.Count > MaxRecentPicks)
                RecentPicks.RemoveAt(RecentPicks.Count - 1);
            PersistRecentPicks();

            _shell.StatusMessage = $"Picked {ColorMathService.ToHex(color)} from {source} — add it to the palette when happy.";
        }

        [RelayCommand]
        private void ApplyRecentPick(RecentPick? pick)
        {
            if (pick is null) return;
            ApplyToBaseColor(pick.Color);
            _shell.StatusMessage = $"Loaded {pick.Hex} ({pick.Name}) into the color panel.";
        }

        /// <summary>§3 picked-color history, read at startup.</summary>
        public void RestoreRecentPicks()
        {
            foreach (var hex in SettingsService.Settings.RecentPicks)
            {
                if (RecentPicks.Count >= MaxRecentPicks) break;
                if (ColorMathService.TryParseHex(hex, out var color))
                    RecentPicks.Add(new RecentPick(color));
            }
        }

        private void PersistRecentPicks()
        {
            SettingsService.Settings.RecentPicks = RecentPicks.Select(p => p.Hex).ToList();
            SettingsService.Save();
        }

        /// <summary>§2 "Paste color from clipboard" — reads any supported notation into the color panel.</summary>
        [RelayCommand]
        private async Task PasteColorAsync()
        {
            var text = await ClipboardHelper.GetTextAsync();
            if (!ColorFormatService.TryParse(text, out var color))
            {
                _shell.StatusMessage = string.IsNullOrWhiteSpace(text)
                    ? "Clipboard is empty."
                    : "Couldn't read a color from the clipboard.";
                return;
            }

            ApplyToBaseColor(color);
            _shell.StatusMessage = $"Pasted {ColorMathService.ToHex(color)} from clipboard.";
        }

        /// <summary>§2 "Edit swatch color (picker)" — opens the spectrum dialog for the base color.</summary>
        [RelayCommand]
        private async Task OpenColorPickerAsync()
        {
            var picked = await MainWindowViewModel.PickColorAsync(BaseColor);
            if (picked is null)
            {
                _shell.StatusMessage = "Color pick cancelled.";
                return;
            }

            ApplyToBaseColor(picked.Value);
            _shell.StatusMessage = $"Base color set to {ColorNamingService.GetClosestName(picked.Value)}.";
        }

        /// <summary>Adds the current base color to the board.</summary>
        [RelayCommand]
        private void AddCurrentColor()
        {
            _shell.PushHistory();

            var name = ColorNamingService.GetClosestName(BaseColor);
            _shell.Palette.Add(_shell.CreateSwatch(BaseColor, name));
            _shell.StatusMessage = $"Added \"{name}\" to the palette.";
        }

        // Coolors-style generator: press the "Press Space to Shuffle" button
        // (or hit Space anywhere — see MainWindow.axaml.cs) and every unlocked
        // swatch is rerolled, in place, keeping the palette's order and size
        // intact. The board is padded up to 5 slots first if it's smaller.
        // The "Harmonize" tool controls *how* colors are rerolled: left on
        // Random, each unlocked swatch gets an independent random color;
        // pick a specific rule and the whole set is rerolled together from
        // that rule, cycling its colors across however many slots there are.
        [RelayCommand]
        private void GeneratePalette()
        {
            _shell.PushHistory();

            var rng = Random.Shared;
            var palette = _shell.Palette;

            while (palette.Count < DefaultPaletteSize)
            {
                var seedColor = ColorHarmonyService.RandomPleasant(rng);
                palette.Add(_shell.CreateSwatch(seedColor, ColorNamingService.GetClosestName(seedColor)));
            }

            List<Color>? harmonyColors = SelectedHarmony == HarmonyType.Random
                ? null
                : ColorHarmonyService.Generate(BaseColor, SelectedHarmony);

            var colorIndex = 0;
            var replaced = 0;

            foreach (var swatch in palette)
            {
                if (swatch.IsLocked) continue;

                var next = harmonyColors is { Count: > 0 }
                    ? harmonyColors[colorIndex++ % harmonyColors.Count]
                    : ColorHarmonyService.RandomPleasant(rng);

                swatch.Color = next;
                swatch.Name = ColorNamingService.GetClosestName(next);
                replaced++;
            }

            _shell.StatusMessage = replaced == 0
                ? "Every color is locked — unlock one to regenerate it."
                : harmonyColors is null
                    ? $"Generated {replaced} new color{(replaced == 1 ? "" : "s")}."
                    : $"Generated {replaced} new color{(replaced == 1 ? "" : "s")} ({SelectedHarmony}).";
        }

        [RelayCommand]
        private async Task ExtractFromImageAsync()
        {
            var bitmap = await FilePickerHelper.PickImageAsync();
            if (bitmap is null)
            {
                _shell.StatusMessage = "No image selected.";
                return;
            }

            _shell.PushHistory();

            List<Color> colors;
            try
            {
                colors = ImageColorExtractionService.ExtractDominantColors(bitmap, DefaultPaletteSize);
            }
            finally
            {
                bitmap.Dispose();
            }

            if (colors.Count == 0)
            {
                _shell.StatusMessage = "Couldn't find usable colors in that image.";
                return;
            }

            var palette = _shell.Palette;

            while (palette.Count < colors.Count)
            {
                palette.Add(_shell.CreateSwatch(Colors.Gray, "Color"));
            }

            var index = 0;
            var replaced = 0;

            foreach (var swatch in palette)
            {
                if (swatch.IsLocked || index >= colors.Count) continue;

                var next = colors[index++];
                swatch.Color = next;
                swatch.Name = ColorNamingService.GetClosestName(next);
                replaced++;
            }

            _shell.StatusMessage = $"Extracted {replaced} color{(replaced == 1 ? "" : "s")} from the image.";
        }

        /// <summary>§8 "Gradient studio": build a multi-stop gradient seeded from the
        /// palette, export it (CSS / SVG / PNG), then pull its colors into the board.</summary>
        [RelayCommand]
        private async Task OpenGradientStudioAsync()
        {
            if (MainWindowViewModel.GetOwnerWindow() is not { } owner) return;

            var initial = _shell.Palette.Select(s => s.Color).ToList();
            var window = new Views.GradientStudioWindow(initial);
            var colors = await window.ShowDialog<List<Color>?>(owner);

            if (colors is null || colors.Count == 0)
            {
                _shell.StatusMessage = "Gradient studio closed.";
                return;
            }

            _shell.PushHistory();
            foreach (var color in colors)
                _shell.Palette.Add(_shell.CreateSwatch(color, ColorNamingService.GetClosestName(color)));

            _shell.StatusMessage = $"Added {colors.Count} gradient color{(colors.Count == 1 ? "" : "s")} to the palette — Ctrl+Z undoes it.";
        }
    }
}
