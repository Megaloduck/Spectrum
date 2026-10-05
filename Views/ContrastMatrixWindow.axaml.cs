using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Spectrum.Models;
using Spectrum.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HAlign = Avalonia.Layout.HorizontalAlignment;
using VAlign = Avalonia.Layout.VerticalAlignment;

namespace Spectrum.Views
{
    /// <summary>
    /// §4/§8 "Contrast matrix": an N×N grid of every text-on-background pair in
    /// the palette. Rows are backgrounds, columns are text; each cell shows the
    /// WCAG ratio color-coded by pass level, with APCA Lc in the tooltip.
    /// Snapshots the palette on open (same convention as CompareWindow).
    /// </summary>
    public partial class ContrastMatrixWindow : Window
    {
        private const double CellWidth = 82;
        private const double CellHeight = 48;
        private const double HeaderWidth = 118;
        private const double HeaderHeight = 66;

        private readonly List<(Color Color, string Name)> _colors = new();

        /// <summary>Design-time only — the real window is created with a palette.</summary>
        public ContrastMatrixWindow()
        {
            InitializeComponent();
        }

        public ContrastMatrixWindow(PaletteDto? palette) : this()
        {
            if (palette is not null)
            {
                _colors.AddRange(palette.Swatches.Select(s =>
                    (Color.FromArgb(s.A, s.R, s.G, s.B), s.Name)));
            }

            if (_colors.Count == 0)
            {
                EmptyHint.IsVisible = true;
                SummaryText.Text = "No colors to compare.";
                return;
            }

            BuildMatrix();
        }

        // ---------------- Grid construction ----------------

        private void BuildMatrix()
        {
            var n = _colors.Count;
            var grid = MatrixGrid;

            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(HeaderWidth)));
            for (var i = 0; i < n; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(CellWidth)));

            grid.RowDefinitions.Add(new RowDefinition(new GridLength(HeaderHeight)));
            for (var i = 0; i < n; i++)
                grid.RowDefinitions.Add(new RowDefinition(new GridLength(CellHeight)));

            // Corner marker explaining the two axes.
            var corner = new TextBlock
            {
                Text = "bg ↓\ntext →",
                FontSize = 10,
                Opacity = 0.65,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VAlign.Center,
                HorizontalAlignment = HAlign.Center,
            };
            Place(corner, 0, 0);

            for (var i = 0; i < n; i++)
            {
                Place(BuildHeader(_colors[i].Color, _colors[i].Name), 0, i + 1);
                Place(BuildHeader(_colors[i].Color, _colors[i].Name), i + 1, 0);
            }

            var passAa = 0;
            var passAaa = 0;
            var total = 0;

            for (var row = 0; row < n; row++)
            {
                for (var col = 0; col < n; col++)
                {
                    var bg = _colors[row].Color;
                    var text = _colors[col].Color;
                    Place(BuildCell(bg, text, diagonal: row == col), row + 1, col + 1);

                    if (row == col) continue; // a color against itself is always 1:1
                    total++;
                    var ratio = ContrastService.ContrastRatio(text, bg);
                    if (ratio >= 4.5) passAa++;
                    if (ratio >= 7.0) passAaa++;
                }
            }

            SummaryText.Text =
                FormattableString.Invariant(
                    $"{passAa}/{total} pairs pass AA · {passAaa} pass AAA · {n} color{(n == 1 ? "" : "s")}");
        }

        private void Place(Control control, int row, int col)
        {
            Grid.SetRow(control, row);
            Grid.SetColumn(control, col);
            MatrixGrid.Children.Add(control);
        }

        private static Border BuildHeader(Color color, string name)
        {
            var readable = ReadableOn(color);

            var stack = new StackPanel
            {
                Spacing = 1,
                VerticalAlignment = VAlign.Center,
                Margin = new Thickness(6, 0),
            };
            stack.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(readable),
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HAlign.Center,
            });
            stack.Children.Add(new TextBlock
            {
                Text = ColorMathService.ToHex(color),
                FontSize = 9,
                Opacity = 0.85,
                Foreground = new SolidColorBrush(readable),
                HorizontalAlignment = HAlign.Center,
            });

            var header = new Border
            {
                Background = new SolidColorBrush(color),
                CornerRadius = new CornerRadius(6),
                BorderBrush = new SolidColorBrush(readable) { Opacity = 0.25 },
                BorderThickness = new Thickness(1),
                Child = stack,
                Margin = new Thickness(3),
            };
            ToolTip.SetTip(header, FormattableString.Invariant($"{name} · {ColorMathService.ToHex(color)}"));
            return header;
        }

        private static Border BuildCell(Color bg, Color text, bool diagonal)
        {
            var ratio = ContrastService.ContrastRatio(text, bg);
            var apca = ContrastService.Apca(text, bg);
            var rating = ContrastService.Rate(ratio);
            var ratingLarge = ContrastService.Rate(ratio, largeText: true);

            string label;
            Color fill;
            Color foreground;

            if (diagonal)
            {
                label = "—";
                fill = Color.FromRgb(0xEC, 0xEC, 0xEC);
                foreground = Color.FromRgb(0x8A, 0x8A, 0x92);
            }
            else
            {
                // Traffic-light pastels: dark text on them stays clearly legible.
                (label, fill, foreground) = rating switch
                {
                    "AAA" => (FormattableString.Invariant($"{ratio:N1}:1"),
                              Color.FromRgb(0xB7, 0xE4, 0xC7), Color.FromRgb(0x0B, 0x3D, 0x22)),
                    "AA" => (FormattableString.Invariant($"{ratio:N1}:1"),
                             Color.FromRgb(0xFF, 0xF3, 0xCD), Color.FromRgb(0x66, 0x4D, 0x03)),
                    _ => (FormattableString.Invariant($"{ratio:N1}:1"),
                          Color.FromRgb(0xF8, 0xD7, 0xDA), Color.FromRgb(0x58, 0x15, 0x1C)),
                };
            }

            var cell = new Border
            {
                Width = CellWidth - 6,
                Height = CellHeight - 6,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(fill),
                Margin = new Thickness(3),
                Child = new TextBlock
                {
                    Text = label,
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(foreground),
                    HorizontalAlignment = HAlign.Center,
                    VerticalAlignment = VAlign.Center,
                },
            };

            if (!diagonal)
            {
                ToolTip.SetTip(cell, string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0} text on {1}\nWCAG {2:N2}:1 — {3} normal / {4} large text\nAPCA Lc {5:N0}",
                    ColorMathService.ToHex(text), ColorMathService.ToHex(bg),
                    ratio, rating, ratingLarge, apca));
            }

            return cell;
        }

        // ---------------- Actions ----------------

        private async void OnCopyCsv(object? sender, RoutedEventArgs e)
        {
            if (_colors.Count == 0) return;

            var sb = new StringBuilder();
            sb.Append("text \\ bg");
            foreach (var bg in _colors)
                sb.Append(FormattableString.Invariant($",{ColorMathService.ToHex(bg.Color)}"));
            sb.AppendLine();

            foreach (var text in _colors)
            {
                sb.Append(ColorMathService.ToHex(text.Color));
                foreach (var bg in _colors)
                {
                    sb.Append(FormattableString.Invariant(
                        $",{ContrastService.ContrastRatio(text.Color, bg.Color):F2}"));
                }
                sb.AppendLine();
            }

            await ClipboardHelper.SetTextAsync(sb.ToString());
            SummaryText.Text = "Matrix copied to CSV — text rows vs background columns.";
        }

        private void OnClose(object? sender, RoutedEventArgs e) => Close();

        // ---------------- Shared helpers ----------------

        /// <summary>Picks dark or light ink that stays readable on <paramref name="color"/>.</summary>
        private static Color ReadableOn(Color color) =>
            (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0 > 0.55
                ? Color.FromRgb(0x1E, 0x20, 0x25)
                : Colors.White;
    }
}
