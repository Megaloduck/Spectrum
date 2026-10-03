using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Spectrum.Converters
{
    /// <summary>Formats a version-history timestamp for the palette versions flyout.</summary>
    public sealed class VersionTimeConverter : IValueConverter
    {
        public static readonly VersionTimeConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is DateTime utc
                ? $"{utc.ToLocalTime():MMM d, HH:mm:ss} · {Relative(utc)}"
                : string.Empty;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static string Relative(DateTime utc)
        {
            var delta = DateTime.UtcNow - utc;
            if (delta.TotalMinutes < 1) return "just now";
            if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
            if (delta.TotalHours < 24) return $"{(int)delta.TotalHours}h ago";
            return $"{(int)delta.TotalDays}d ago";
        }
    }
}
