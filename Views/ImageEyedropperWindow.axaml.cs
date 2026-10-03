using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Spectrum.Services;
using System;

namespace Spectrum.Views
{
    /// <summary>
    /// In-app eyedropper (§3 "pick from loaded image"): hover shows the exact
    /// pixel under the cursor with its hex + auto-name; clicking returns the
    /// color to the caller. Owns and disposes the bitmap it was given.
    /// </summary>
    public partial class ImageEyedropperWindow : Window
    {
        private readonly Avalonia.Media.Imaging.Bitmap _bitmap;
        private readonly ImageSampler? _sampler;
        private bool _resolved;

        /// <summary>Design-time only — the real window is created with an image.</summary>
        public ImageEyedropperWindow()
            : this(new Avalonia.Media.Imaging.WriteableBitmap(
                new PixelSize(1, 1), new Vector(96, 96),
                Avalonia.Platform.PixelFormats.Bgra8888,
                Avalonia.Platform.AlphaFormat.Unpremul))
        {
        }

        public ImageEyedropperWindow(Avalonia.Media.Imaging.Bitmap bitmap)
        {
            InitializeComponent();

            _bitmap = bitmap;
            _sampler = ImageSampler.FromBitmap(bitmap);
            ImageControl.Source = bitmap;

            Closed += (_, _) => _bitmap.Dispose();
            KeyDown += OnKeyDown;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || _resolved) return;
            _resolved = true;
            Close(null);
        }

        /// <summary>Maps a point inside the Image control to source-pixel coordinates,
        /// accounting for the Uniform stretch's letterboxing.</summary>
        private (int X, int Y)? SourcePoint(Point position)
        {
            if (_sampler is null) return null;

            var bounds = ImageControl.Bounds.Size;
            if (bounds.Width <= 0 || bounds.Height <= 0) return null;

            var scale = Math.Min(bounds.Width / _sampler.Width, bounds.Height / _sampler.Height);
            if (scale <= 0) return null;

            var drawWidth = _sampler.Width * scale;
            var drawHeight = _sampler.Height * scale;
            var offsetX = (bounds.Width - drawWidth) / 2;
            var offsetY = (bounds.Height - drawHeight) / 2;

            var srcX = (int)((position.X - offsetX) / scale);
            var srcY = (int)((position.Y - offsetY) / scale);

            if (srcX < 0 || srcY < 0 || srcX >= _sampler.Width || srcY >= _sampler.Height) return null;
            return (srcX, srcY);
        }

        private void OnImagePointerMoved(object? sender, PointerEventArgs e)
        {
            var point = SourcePoint(e.GetPosition(ImageControl));
            if (point is null) return;

            var (x, y) = point.Value;
            var color = _sampler!.Sample(x, y);
            ShowSample(color, x, y);
        }

        private void OnImagePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = SourcePoint(e.GetPosition(ImageControl));
            if (point is null || _resolved) return;

            var color = _sampler!.Sample(point.Value.X, point.Value.Y);
            _resolved = true;
            Close(color);
        }

        private void ShowSample(Color color, int x, int y)
        {
            SampleSwatch.Background = new SolidColorBrush(color);
            HexText.Text = ColorMathService.ToHex(color);
            CoordText.Text = $"x {x} · y {y} · rgb({color.R}, {color.G}, {color.B})";
            NameText.Text = ColorNamingService.GetClosestName(color);
        }
    }
}
