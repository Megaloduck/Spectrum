using Avalonia.Media;

namespace Spectrum.Models
{
    /// <summary>One entry in the recently-picked-colors history (§3).</summary>
    public class RecentPick
    {
        public RecentPick(Color color)
        {
            Color = color;
            Brush = new SolidColorBrush(color);
            Hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            Name = Services.ColorNamingService.GetClosestName(color);
        }

        public Color Color { get; }
        public IBrush Brush { get; }
        public string Hex { get; }
        public string Name { get; }
    }
}
