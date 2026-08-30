using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.Text;

namespace Spectrum.Converters
{
    /// <summary>
    /// Displays a PascalCase enum value with spaces inserted before each
    /// interior capital, e.g. HarmonyType.SplitComplementary -> "Split Complementary".
    /// Used so ComboBoxes show readable labels without needing a wrapper
    /// type per enum.
    /// </summary>
    public class EnumSpacedConverter : IValueConverter
    {
        public static readonly EnumSpacedConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null) return null;

            var name = value.ToString() ?? string.Empty;
            var sb = new StringBuilder(name.Length + 4);

            for (var i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                {
                    sb.Append(' ');
                }

                sb.Append(name[i]);
            }

            return sb.ToString();
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException("EnumSpacedConverter only supports one-way display conversion.");
    }
}
