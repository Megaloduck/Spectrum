using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Spectrum.Converters
{
    /// <summary>Turns an Avalonia Color into an immutable SolidColorBrush for
    /// Background=/Fill= bindings. Cached because the reference views (collision
    /// chips, ramp steps, chart bars) create many short-lived brush bindings and
    /// new'ing a brush per binding churns the render tree on every refresh.
    /// SolidColorBrush is frozen after creation, so one brush per color is safe
    /// to share across the whole visual tree.</summary>
    public sealed class BrushCache : IValueConverter
    {
        public static readonly BrushCache Instance = new();

        private static readonly Dictionary<Color, SolidColorBrush> Cache = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not Color color) return null;

            if (!Cache.TryGetValue(color, out var brush))
            {
                // Avalonia has no WPF-style Freeze(); a shared immutable-by-
                // convention SolidColorBrush per color is the standard pattern.
                brush = new SolidColorBrush(color);
                Cache[color] = brush;
            }

            return brush;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException("BrushCache only supports one-way color-to-brush conversion.");
    }
}
