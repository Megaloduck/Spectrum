using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Spectrum.ViewModels;
using System;

namespace Spectrum.Views
{
    /// <summary>
    /// Modal color picker: saturation/value spectrum plane + hue/opacity
    /// sliders + manual HEX/RGB/HSL/HSV input. Returns the picked color
    /// (or null when cancelled) through ShowDialog&lt;Color?&gt;.
    /// </summary>
    public partial class ColorPickerDialog : Window
    {
        private const double MarkerSize = 16;

        private readonly ColorPickerViewModel _vm;
        private bool _draggingPlane;

        /// <summary>Design-time only — the real dialog is created with an initial color.</summary>
        public ColorPickerDialog() : this(Colors.Black)
        {
        }

        public ColorPickerDialog(Color initial)
        {
            InitializeComponent();
            _vm = new ColorPickerViewModel(initial);
            DataContext = _vm;

            _vm.ColorChanged += UpdateMarker;

            Opened += (_, _) =>
            {
                // The plane hasn't been measured yet when Opened fires; post so layout runs first.
                Dispatcher.UIThread.Post(UpdateMarker);
            };

            // Reposition the marker whenever the plane is resized (window resize, DPI change).
            Plane.PropertyChanged += (_, e) =>
            {
                if (e.Property == BoundsProperty)
                    UpdateMarker();
            };
        }

        // ---------------- Spectrum plane ----------------

        private void OnPlanePressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Border plane) return;
            if (!e.GetCurrentPoint(plane).Properties.IsLeftButtonPressed) return;

            _draggingPlane = true;
            UpdateFromPosition(plane, e.GetPosition(plane));
            e.Pointer.Capture(plane);
            e.Handled = true;
        }

        private void OnPlaneMoved(object? sender, PointerEventArgs e)
        {
            if (!_draggingPlane || sender is not Border plane) return;
            UpdateFromPosition(plane, e.GetPosition(plane));
        }

        private void OnPlaneReleased(object? sender, PointerReleasedEventArgs e)
        {
            _draggingPlane = false;
            e.Pointer.Capture(null);
        }

        private void OnPlaneCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            _draggingPlane = false;
        }

        private void UpdateFromPosition(Border plane, Point p)
        {
            // Use the inner content size so the border thickness doesn't skew the mapping.
            var w = plane.Bounds.Width;
            var h = plane.Bounds.Height;
            if (w <= 0 || h <= 0) return;

            _vm.Saturation = Math.Clamp(p.X / w, 0, 1) * 100;
            _vm.Value = (1 - Math.Clamp(p.Y / h, 0, 1)) * 100;
        }

        private void UpdateMarker()
        {
            var w = Plane.Bounds.Width;
            var h = Plane.Bounds.Height;
            if (w <= 0 || h <= 0) return;

            var s = Math.Clamp(_vm.Saturation / 100.0, 0, 1);
            var v = Math.Clamp(_vm.Value / 100.0, 0, 1);

            Canvas.SetLeft(Marker, Clamp(s * w - MarkerSize / 2, 0, w - MarkerSize));
            Canvas.SetTop(Marker, Clamp((1 - v) * h - MarkerSize / 2, 0, h - MarkerSize));
        }

        private static double Clamp(double value, double min, double max) =>
            value < min ? min : value > max ? max : value;

        // ---------------- Manual inputs ----------------

        private void OnInputKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            Commit(sender as TextBox);
            e.Handled = true;
        }

        private void OnInputLostFocus(object? sender, RoutedEventArgs e) => Commit(sender as TextBox);

        private void Commit(TextBox? box)
        {
            if (box is null) return;

            var text = box.Text ?? string.Empty;

            switch (box.Name)
            {
                case nameof(HexBox): _vm.TryApplyHex(text); break;
                case nameof(RgbBox): _vm.TryApplyRgb(text); break;
                case nameof(HslBox): _vm.TryApplyHsl(text); break;
                case nameof(HsvBox): _vm.TryApplyHsv(text); break;
            }
        }

        // ---------------- Result ----------------

        private void OnConfirm(object? sender, RoutedEventArgs e) => Close(_vm.CurrentColor);

        private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    }
}
