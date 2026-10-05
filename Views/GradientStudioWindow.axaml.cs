using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Spectrum.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace Spectrum.Views
{
    /// <summary>
    /// §8 "Gradient studio": modal editor for a multi-stop linear/radial
    /// gradient seeded from the current palette. Exports CSS (clipboard),
    /// SVG and PNG (local files only), or returns its stop colors so the
    /// ViewModel can append them to the palette under one undo step.
    /// </summary>
    public partial class GradientStudioWindow : Window
    {
        private readonly ObservableCollection<Stop> _stops = new();

        private GradientKind _kind = GradientKind.Linear;
        private double _angle = 90;
        private bool _ready;

        /// <summary>One row of the stop list: color + position, bound in the item template.</summary>
        private sealed class Stop : INotifyPropertyChanged
        {
            private readonly GradientStudioWindow _owner;
            private Color _color;
            private double _position;
            private int _index;
            private int _count = 1;

            public Stop(GradientStudioWindow owner, Color color, double position)
            {
                _owner = owner;
                _color = color;
                _position = position;
            }

            public Color Color
            {
                get => _color;
                set
                {
                    if (_color == value) return;
                    _color = value;
                    Raise(nameof(Color));
                    Raise(nameof(Hex));
                    Raise(nameof(Info));
                    Raise(nameof(Brush));
                    _owner.Refresh();
                }
            }

            public double Position
            {
                get => _position;
                set
                {
                    var clamped = Math.Clamp(value, 0, 100);
                    if (Math.Abs(_position - clamped) < 1e-9) return;
                    _position = clamped;
                    Raise(nameof(Position));
                    Raise(nameof(Info));
                    _owner.Refresh();
                }
            }

            public string Hex => ColorFormatService.Format(_color, Models.CopyFormat.Hex);

            public string Info =>
                FormattableString.Invariant($"{Hex} · {Math.Round(_position):N0}%");

            public IBrush Brush => new SolidColorBrush(_color);

            public bool IsNotFirst => _index > 0;
            public bool IsNotLast => _index < _count - 1;

            /// <summary>Mirrors the row's place in the list so the move buttons
            /// can disable themselves at the edges.</summary>
            internal void SetIndex(int index, int count)
            {
                if (_index == index && _count == count) return;
                _index = index;
                _count = count;
                Raise(nameof(IsNotFirst));
                Raise(nameof(IsNotLast));
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            private void Raise(string name) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>Design-time only — the real window is created with colors.</summary>
        public GradientStudioWindow()
        {
            InitializeComponent();
            StopsList.ItemsSource = _stops;
            _stops.CollectionChanged += (_, _) => UpdateEdgeStates();
            _ready = true;
            Refresh();
        }

        public GradientStudioWindow(IReadOnlyList<Color> initial) : this()
        {
            if (initial.Count >= 2)
            {
                var step = 100.0 / (initial.Count - 1);
                for (var i = 0; i < initial.Count; i++)
                    _stops.Add(new Stop(this, initial[i], Math.Round(i * step, 4)));
            }
            else if (initial.Count == 1)
            {
                // One color: fade it toward white so there is something to edit.
                _stops.Add(new Stop(this, initial[0], 0));
                _stops.Add(new Stop(this, ColorMathService.Mix(initial[0], Colors.White, 0.6), 100));
            }
            else
            {
                _stops.Add(new Stop(this, Color.FromRgb(0xFF, 0x00, 0x00), 0));
                _stops.Add(new Stop(this, Color.FromRgb(0x00, 0x00, 0xFF), 100));
            }

            Refresh();
        }

        // ---------------- Current state ----------------

        private List<GradientColorStop> CurrentStops() =>
            _stops.Select(s => new GradientColorStop(s.Color, s.Position)).ToList();

        private void UpdateEdgeStates()
        {
            for (var i = 0; i < _stops.Count; i++)
                _stops[i].SetIndex(i, _stops.Count);
        }

        /// <summary>Rebuilds the preview brush and the live CSS readout.</summary>
        private void Refresh()
        {
            if (!_ready || PreviewBorder is null) return;

            PreviewBorder.Background = BuildBrush();
            CssText.Text = _stops.Count >= 2
                ? GradientService.ToCss(CurrentStops(), _kind, _angle)
                : "Add at least two stops to form a gradient.";
        }

        private IBrush BuildBrush()
        {
            var stops = CurrentStops();
            if (stops.Count == 0) return Brushes.Transparent;

            if (_kind == GradientKind.Linear)
            {
                var (x1, y1, x2, y2) = GradientService.GradientLine(_angle);
                var brush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(x1 / 100, y1 / 100, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(x2 / 100, y2 / 100, RelativeUnit.Relative),
                };
                foreach (var s in stops)
                    brush.GradientStops.Add(new GradientStop(s.Color, s.Position / 100));
                return brush;
            }

            var radial = new RadialGradientBrush();
            foreach (var s in stops)
                radial.GradientStops.Add(new GradientStop(s.Color, s.Position / 100));
            return radial;
        }

        /// <summary>Keeps the list in visual (position) order so evenly-space and
        /// move operations reason over the order the user actually sees.</summary>
        private void SortRows()
        {
            var sorted = _stops.OrderBy(s => s.Position).ToList();
            for (var i = 0; i < sorted.Count; i++)
            {
                var from = _stops.IndexOf(sorted[i]);
                if (from != i) _stops.Move(from, i);
            }
        }

        // ---------------- Kind / angle ----------------

        private void OnKindChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (KindCombo is null) return;
            _kind = KindCombo.SelectedIndex == 1 ? GradientKind.Radial : GradientKind.Linear;

            if (AngleSlider is not null)
                AngleSlider.IsEnabled = _kind == GradientKind.Linear;
            if (AngleLabel is not null)
                AngleLabel.Opacity = _kind == GradientKind.Linear ? 1.0 : 0.4;

            Refresh();
        }

        private void OnAngleChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            _angle = e.NewValue;
            if (AngleLabel is not null)
                AngleLabel.Text = FormattableString.Invariant($"{GradientService.Angle(_angle)}°");
            Refresh();
        }

        // ---------------- Stop operations ----------------

        private void OnStopSwatchClick(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Control { DataContext: Stop stop }) return;
            _ = EditColorAsync(stop);
            e.Handled = true;
        }

        private async System.Threading.Tasks.Task EditColorAsync(Stop stop)
        {
            var dialog = new ColorPickerDialog(stop.Color);
            var picked = await dialog.ShowDialog<Color?>(this);
            if (picked is null) return;
            stop.Color = picked.Value;
        }

        internal void OnEvenlySpace(object? sender, RoutedEventArgs e)
        {
            if (_stops.Count < 2) return;
            SortRows();

            var step = 100.0 / (_stops.Count - 1);
            for (var i = 0; i < _stops.Count; i++)
                _stops[i].Position = Math.Round(i * step, 4);

            HintText.Text = string.Empty;
            Refresh();
        }

        internal void OnReverse(object? sender, RoutedEventArgs e)
        {
            if (_stops.Count < 2) return;
            SortRows();

            // Mirroring each stop across the 0–100 range plays the color
            // sequence back the other way (colors stay attached to their rows).
            foreach (var stop in _stops)
                stop.Position = 100 - stop.Position;

            SortRows();
            HintText.Text = string.Empty;
            Refresh();
        }

        internal void OnAddStop(object? sender, RoutedEventArgs e)
        {
            SortRows();

            if (_stops.Count == 0)
            {
                _stops.Add(new Stop(this, Colors.White, 100));
            }
            else
            {
                var mix = _stops.Count == 1
                    ? _stops[0].Color
                    : ColorMathService.Mix(_stops[0].Color, _stops[^1].Color, 0.5);
                _stops.Add(new Stop(this, mix, 50));
                OnEvenlySpace(sender, e);
                HintText.Text = string.Empty;
                return;
            }

            Refresh();
        }

        private void OnRemoveStop(object? sender, RoutedEventArgs e)
        {
            if (sender is not Control { DataContext: Stop stop }) return;

            if (_stops.Count <= 2)
            {
                HintText.Text = "A gradient needs at least two stops.";
                return;
            }

            _stops.Remove(stop);
            HintText.Text = string.Empty;
            Refresh();
        }

        private void OnMoveUp(object? sender, RoutedEventArgs e)
        {
            if (sender is not Control { DataContext: Stop stop }) return;
            SortRows();

            var i = _stops.IndexOf(stop);
            if (i <= 0) return;

            var above = _stops[i - 1];
            var p = stop.Position;
            stop.Position = above.Position;
            above.Position = p;
            SortRows();
        }

        private void OnMoveDown(object? sender, RoutedEventArgs e)
        {
            if (sender is not Control { DataContext: Stop stop }) return;
            SortRows();

            var i = _stops.IndexOf(stop);
            if (i < 0 || i >= _stops.Count - 1) return;

            var below = _stops[i + 1];
            var p = stop.Position;
            stop.Position = below.Position;
            below.Position = p;
            SortRows();
        }

        // ---------------- Export actions ----------------

        private async void OnCopyCss(object? sender, RoutedEventArgs e)
        {
            if (_stops.Count < 2)
            {
                HintText.Text = "Add at least two stops first.";
                return;
            }

            await ClipboardHelper.SetTextAsync(GradientService.ToCss(CurrentStops(), _kind, _angle));
            HintText.Text = "CSS copied to clipboard.";
        }

        private async void OnSaveSvg(object? sender, RoutedEventArgs e)
        {
            if (_stops.Count < 2)
            {
                HintText.Text = "Add at least two stops first.";
                return;
            }

            var path = await PaletteFileService.PickSaveFileAsync("gradient", "svg", "SVG image (*.svg)");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                await File.WriteAllTextAsync(path, GradientService.ToSvg(CurrentStops(), 1200, 600, _kind, _angle));
                HintText.Text = $"Saved {System.IO.Path.GetFileName(path)}.";
            }
            catch (Exception ex)
            {
                HintText.Text = $"Couldn't save SVG: {ex.Message}";
            }
        }

        private async void OnSavePng(object? sender, RoutedEventArgs e)
        {
            if (_stops.Count < 2)
            {
                HintText.Text = "Add at least two stops first.";
                return;
            }

            var path = await PaletteFileService.PickSaveFileAsync("gradient", "png", "PNG image (*.png)");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                const int width = 1200;
                const int height = 600;
                using var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
                using (var dc = bitmap.CreateDrawingContext())
                {
                    dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                    dc.DrawRectangle(BuildBrush(), null, new Rect(0, 0, width, height));
                }

                using var stream = File.Create(path);
                bitmap.Save(stream);
                HintText.Text = $"Saved {System.IO.Path.GetFileName(path)}.";
            }
            catch (Exception ex)
            {
                HintText.Text = $"Couldn't render PNG: {ex.Message}";
            }
        }

        /// <summary>Closes the dialog handing the stop colors back to the caller.</summary>
        private void OnAddToPalette(object? sender, RoutedEventArgs e)
        {
            if (_stops.Count == 0)
            {
                Close(null);
                return;
            }

            SortRows();
            Close(_stops.Select(s => s.Color).ToList());
        }

        private void OnClose(object? sender, RoutedEventArgs e) => Close(null);
    }
}
