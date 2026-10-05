using Avalonia.Media;
using Spectrum.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Spectrum.Services
{
    /// <summary>Gradient family supported by the studio (conic is out of scope —
    /// Avalonia has no conic brush to preview it with).</summary>
    public enum GradientKind
    {
        Linear,
        Radial,
    }

    /// <summary>One gradient stop: its color and position in percent (0–100).</summary>
    public readonly record struct GradientColorStop(Color Color, double Position);

    /// <summary>
    /// §8 "Gradient studio": pure math + serializers for multi-stop gradients.
    /// Deliberately UI-free and culture-invariant (CSS/SVG output must read
    /// identically on every machine) so it can be unit tested headless.
    /// </summary>
    public static class GradientService
    {
        /// <summary>Clamps positions into 0–100 and sorts by position (stable —
        /// stops sharing a position keep their insertion order).</summary>
        public static List<GradientColorStop> Normalize(IEnumerable<GradientColorStop> stops) =>
            stops.Select(s => new GradientColorStop(s.Color, Math.Clamp(s.Position, 0, 100)))
                 .OrderBy(s => s.Position)
                 .ToList();

        /// <summary>Redistributes the stops evenly across 0–100, preserving their
        /// current (position-sorted) color order.</summary>
        public static List<GradientColorStop> EvenlySpace(IEnumerable<GradientColorStop> stops)
        {
            var list = Normalize(stops);
            if (list.Count <= 1) return list;

            var step = 100.0 / (list.Count - 1);
            return list.Select((s, i) => new GradientColorStop(s.Color, Math.Round(i * step, 4))).ToList();
        }

        /// <summary>Reverses the gradient: colors play back in the opposite
        /// direction (positions mirrored so the visual sequence flips).</summary>
        public static List<GradientColorStop> Reverse(IEnumerable<GradientColorStop> stops) =>
            Normalize(stops)
                .AsEnumerable()
                .Reverse()
                .Select(s => new GradientColorStop(s.Color, 100 - s.Position))
                .ToList();

        /// <summary>CSS serialization, e.g.
        /// <c>linear-gradient(90deg, #FF0000 0%, #0000FF 100%)</c>.</summary>
        public static string ToCss(IEnumerable<GradientColorStop> stops, GradientKind kind, double angleDegrees)
        {
            var list = Normalize(stops);
            var head = kind == GradientKind.Linear
                ? FormattableString.Invariant($"linear-gradient({Angle(angleDegrees)}deg")
                : "radial-gradient(circle at 50% 50%";

            var parts = list.Select(s =>
                FormattableString.Invariant(
                    $"{ColorFormatService.Format(s.Color, CopyFormat.Hex)} {Math.Round(s.Position, 1)}%"));

            return $"{head}, {string.Join(", ", parts)})";
        }

        /// <summary>SVG serialization with an explicit width/height — the file is
        /// a standalone rect filled by the gradient.</summary>
        public static string ToSvg(IEnumerable<GradientColorStop> stops, int width, int height,
                                   GradientKind kind, double angleDegrees)
        {
            var list = Normalize(stops);
            const string id = "spectrum-gradient";

            var sb = new StringBuilder();
            sb.AppendLine(FormattableString.Invariant(
                $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">"));
            sb.AppendLine("  <defs>");

            if (kind == GradientKind.Linear)
            {
                var (x1, y1, x2, y2) = GradientLine(angleDegrees);
                sb.AppendLine(FormattableString.Invariant(
                    $"    <linearGradient id=\"{id}\" x1=\"{x1}%\" y1=\"{y1}%\" x2=\"{x2}%\" y2=\"{y2}%\">"));
            }
            else
            {
                sb.AppendLine($"    <radialGradient id=\"{id}\" cx=\"50%\" cy=\"50%\" r=\"50%\">");
            }

            foreach (var s in list)
            {
                sb.AppendLine(FormattableString.Invariant(
                    $"      <stop offset=\"{Math.Round(s.Position, 1)}%\" stop-color=\"{ColorFormatService.Format(s.Color, CopyFormat.Hex)}\" stop-opacity=\"{s.Color.A / 255.0:F2}\"/>"));
            }

            sb.AppendLine(kind == GradientKind.Linear ? "    </linearGradient>" : "    </radialGradient>");
            sb.AppendLine("  </defs>");
            sb.AppendLine(FormattableString.Invariant(
                $"  <rect width=\"{width}\" height=\"{height}\" fill=\"url(#{id})\"/>"));
            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        /// <summary>Normalizes any angle into 0–359 degrees.</summary>
        public static int Angle(double angleDegrees)
        {
            var rounded = (int)Math.Round(angleDegrees);
            return ((rounded % 360) + 360) % 360;
        }

        /// <summary>Maps a CSS angle (0deg = up, clockwise) onto a 0–100%
        /// gradient line — shared by the SVG export and the Avalonia preview brush.
        /// SVG's y axis points down, hence the negated cosine.</summary>
        public static (double X1, double Y1, double X2, double Y2) GradientLine(double angleDegrees)
        {
            var radians = Angle(angleDegrees) * Math.PI / 180.0;
            var dx = Math.Sin(radians);
            var dy = -Math.Cos(radians);
            return (Math.Round(50 - 50 * dx, 4), Math.Round(50 - 50 * dy, 4),
                    Math.Round(50 + 50 * dx, 4), Math.Round(50 + 50 * dy, 4));
        }
    }
}
