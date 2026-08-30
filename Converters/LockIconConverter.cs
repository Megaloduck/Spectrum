using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Material.Icons;
using Avalonia.Data.Converters;
using System.Globalization;

namespace Spectrum.Converters
{
    /// <summary>
    /// Picks the locked/unlocked Material icon based on ColorSwatch.IsLocked.
    /// </summary>
    public class LockIconConverter : IValueConverter
    {
        public static readonly LockIconConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? MaterialIconKind.Lock : MaterialIconKind.LockOpenVariant;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}