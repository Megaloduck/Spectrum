using Avalonia.Media;
using System;

namespace Spectrum.Services
{
    public readonly record struct HsvColor(double H, double S, double V);
    public readonly record struct LabColor(double L, double A, double B);
    public readonly record struct LchColor(double L, double C, double H);
    public readonly record struct OklabColor(double L, double A, double B);
    public readonly record struct OklchColor(double L, double C, double H);

    /// <summary>
    /// Pure color math for §4 "Color Science (All Local Math)".
    /// Everything here is deterministic, allocation-light, dependency-free
    /// static code operating on Avalonia's Color — deliberately UI-free so it
    /// can be unit tested without a window (see README note on tests).
    /// </summary>
    public static class ColorMathService
    {
        // ---------------- HEX ----------------

        public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        public static bool TryParseHex(string? input, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(input)) return false;

            var text = input.Trim().TrimStart('#').Trim();
            if (text.Length == 3) // #RGB shorthand
                text = $"{text[0]}{text[0]}{text[1]}{text[1]}{text[2]}{text[2]}";
            if (text.Length == 8) // RRGGBBAA
            {
                if (!TryParseHexBytes(text, out var r, out var g, out var b)) return false;
                if (byte.TryParse(text.Substring(6, 2), System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out var a))
                {
                    color = Color.FromArgb(a, r, g, b);
                    return true;
                }
                return false;
            }
            if (text.Length != 6) return false;

            if (!TryParseHexBytes(text, out var rr, out var gg, out var bb)) return false;
            color = Color.FromRgb(rr, gg, bb);
            return true;
        }

        private static bool TryParseHexBytes(string text, out byte r, out byte g, out byte b)
        {
            var ok =
                byte.TryParse(text.Substring(0, 2), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out r) &
                byte.TryParse(text.Substring(2, 2), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out g) &
                byte.TryParse(text.Substring(4, 2), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out b);
            return ok;
        }

        // ---------------- HSV ----------------

        public static HsvColor RgbToHsv(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var delta = max - min;

            double h;
            if (delta < 1e-9) h = 0;
            else if (max == r) h = 60 * (((g - b) / delta) % 6);
            else if (max == g) h = 60 * ((b - r) / delta + 2);
            else h = 60 * ((r - g) / delta + 4);
            if (h < 0) h += 360;

            var s = max <= 1e-9 ? 0 : delta / max;
            return new HsvColor(h, s, max);
        }

        public static Color HsvToRgb(HsvColor hsv, byte alpha = 255)
        {
            var h = ((hsv.H % 360) + 360) % 360;
            var s = Math.Clamp(hsv.S, 0, 1);
            var v = Math.Clamp(hsv.V, 0, 1);

            var c = v * s;
            var x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            var m = v - c;

            double r, g, b;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return Color.FromArgb(alpha,
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((b + m) * 255));
        }

        // ---------------- HSL (delegates to the existing hue-wheel helpers) ----------------

        public static Color HslToRgb(double h, double s, double l, byte alpha = 255) =>
            ColorHarmonyService.FromHsl(h, s, l, alpha);

        public static (double H, double S, double L) RgbToHsl(Color c) => ColorHarmonyService.ToHsl(c);

        // ---------------- CIELAB / LCH (D65) ----------------

        private static readonly (double X, double Y, double Z) D65 = (0.95047, 1.00000, 1.08883);

        public static LabColor RgbToLab(Color c)
        {
            var (r, g, b) = SrgbToLinear(c);

            var x = 0.4124564 * r + 0.3575761 * g + 0.1804375 * b;
            var y = 0.2126729 * r + 0.7151522 * g + 0.0721750 * b;
            var z = 0.0193339 * r + 0.1191920 * g + 0.9503041 * b;

            static double F(double t) =>
                t > 216.0 / 24389.0 ? Math.Cbrt(t) : (24389.0 / 27.0 * t + 16) / 116;

            var fx = F(x / D65.X);
            var fy = F(y / D65.Y);
            var fz = F(z / D65.Z);

            return new LabColor(116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
        }

        public static Color LabToRgb(LabColor lab, byte alpha = 255)
        {
            var fy = (lab.L + 16) / 116;
            var fx = fy + lab.A / 500;
            var fz = fy - lab.B / 200;

            static double FInv(double t) =>
                t > 6.0 / 29.0 ? t * t * t : 3 * (6.0 / 29.0) * (6.0 / 29.0) * (t - 4.0 / 29.0);

            var x = D65.X * FInv(fx);
            var y = D65.Y * FInv(fy);
            var z = D65.Z * FInv(fz);

            var r = 3.2404542 * x - 1.5371385 * y - 0.4985314 * z;
            var g = -0.9692660 * x + 1.8760108 * y + 0.0415560 * z;
            var b = 0.0556434 * x - 0.2040259 * y + 1.0572252 * z;

            return Color.FromArgb(alpha,
                ToByte(LinearToSrgb(r)),
                ToByte(LinearToSrgb(g)),
                ToByte(LinearToSrgb(b)));
        }

        public static LchColor LabToLch(LabColor lab)
        {
            var c = Math.Sqrt(lab.A * lab.A + lab.B * lab.B);
            var h = Math.Atan2(lab.B, lab.A) * 180.0 / Math.PI;
            if (h < 0) h += 360;
            return new LchColor(lab.L, c, h);
        }

        public static LabColor LchToLab(LchColor lch) =>
            new(lch.L, lch.C * Math.Cos(lch.H * Math.PI / 180), lch.C * Math.Sin(lch.H * Math.PI / 180));

        // ---------------- OKLab / OKLCH (Björn Ottosson's reference matrices) ----------------

        public static OklabColor RgbToOklab(Color c)
        {
            var (r, g, b) = SrgbToLinear(c);

            var l = 0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b;
            var m = 0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b;
            var s = 0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b;

            var l_ = Math.Cbrt(l);
            var m_ = Math.Cbrt(m);
            var s_ = Math.Cbrt(s);

            return new OklabColor(
                0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_,
                1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_,
                0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_);
        }

        public static Color OklabToRgb(OklabColor lab, byte alpha = 255)
        {
            var l_ = lab.L + 0.3963377774 * lab.A + 0.2158037573 * lab.B;
            var m_ = lab.L - 0.1055613458 * lab.A - 0.0638541728 * lab.B;
            var s_ = lab.L - 0.0894841775 * lab.A - 1.2914855480 * lab.B;

            var l = l_ * l_ * l_;
            var m = m_ * m_ * m_;
            var s = s_ * s_ * s_;

            var r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
            var g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
            var b = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;

            return Color.FromArgb(alpha,
                ToByte(LinearToSrgb(r)),
                ToByte(LinearToSrgb(g)),
                ToByte(LinearToSrgb(b)));
        }

        public static OklchColor RgbToOklch(Color c) => LabToOklch(RgbToOklab(c));

        public static OklchColor LabToOklch(OklabColor lab)
        {
            var chroma = Math.Sqrt(lab.A * lab.A + lab.B * lab.B);
            var h = Math.Atan2(lab.B, lab.A) * 180.0 / Math.PI;
            if (h < 0) h += 360;
            return new OklchColor(lab.L, chroma, h);
        }

        public static OklabColor OklchToLab(OklchColor lch) =>
            new(lch.L, lch.C * Math.Cos(lch.H * Math.PI / 180), lch.C * Math.Sin(lch.H * Math.PI / 180));

        public static Color OklchToRgb(OklchColor lch, byte alpha = 255) =>
            OklabToRgb(OklchToLab(lch), alpha);

        // ---------------- HWB ----------------

        /// <summary>HWB (hue, whiteness, blackness) — the CSS hw() space. Whiteness
        /// and blackness are fractions of the minimum / maximum RGB channel.</summary>
        public static (double H, double W, double B) RgbToHwb(Color c)
        {
            var hsv = RgbToHsv(c);
            var w = Math.Min(c.R, Math.Min(c.G, c.B)) / 255.0;
            var b = 1.0 - Math.Max(c.R, Math.Max(c.G, c.B)) / 255.0;
            return (hsv.H, w, b);
        }

        // ---------------- CMYK ----------------

        /// <summary>Naive sRGB→CMYK (no ICC profile — a screen approximation of print
        /// ink, which is exactly what a reference readout should be labelled as).</summary>
        public static (double C, double M, double Y, double K) RgbToCmyk(Color c)
        {
            var r = c.R / 255.0;
            var g = c.G / 255.0;
            var b = c.B / 255.0;

            var k = 1.0 - Math.Max(r, Math.Max(g, b));
            if (k >= 1.0 - 1e-9) return (0, 0, 0, 1);

            var inv = 1.0 - k;
            return ((1 - r - k) / inv, (1 - g - k) / inv, (1 - b - k) / inv, k);
        }

        // ---------------- Perceptual difference ----------------

        /// <summary>CIE76 ΔE between two colors — a number below ~2.3 is imperceptible,
        /// below ~10 reads as "the same color" for UI purposes. Used by the color-
        /// vision collision detector to find palette pairs that collapse.</summary>
        public static double DeltaE(Color a, Color b)
        {
            var la = RgbToLab(a);
            var lb = RgbToLab(b);
            var dl = la.L - lb.L;
            var da = la.A - lb.A;
            var db = la.B - lb.B;
            return Math.Sqrt(dl * dl + da * da + db * db);
        }

        // ---------------- OKLCH mixing (perceptually even ramps) ----------------

        /// <summary>Interpolates two colors in OKLCH instead of sRGB, which avoids the
        /// muddy gray midpoint that RGB mixing produces between complementary hues.
        /// Hue takes the shortest path; a near-zero-chroma endpoint borrows the other
        /// endpoint's hue so white/gray ends don't collapse the ramp's color.</summary>
        public static Color MixOklch(Color a, Color b, double t)
        {
            t = Math.Clamp(t, 0, 1);

            var oa = RgbToOklch(a);
            var ob = RgbToOklch(b);

            double hue;
            if (oa.C < 1e-4 && ob.C < 1e-4) hue = 0;
            else if (oa.C < 1e-4) hue = ob.H;
            else if (ob.C < 1e-4) hue = oa.H;
            else
            {
                var shortest = ((ob.H - oa.H + 540) % 360) - 180;
                hue = ((oa.H + shortest * t) % 360 + 360) % 360;
            }

            var lch = new OklchColor(
                oa.L + (ob.L - oa.L) * t,
                oa.C + (ob.C - oa.C) * t,
                hue);

            var mixed = OklchToRgb(lch);
            return Color.FromArgb(
                (byte)Math.Round(a.A + (b.A - a.A) * t),
                mixed.R, mixed.G, mixed.B);
        }

        /// <summary>Lighter steps in OKLCH: L climbs toward 1 and chroma relaxes toward
        /// 0 while the hue holds — a Tailwind-style 50…400 ramp without the wash-out.</summary>
        public static System.Collections.Generic.List<Color> TintsOklch(Color baseColor, int count = 5)
        {
            var ok = RgbToOklch(baseColor);
            var result = new System.Collections.Generic.List<Color>();
            for (var i = count - 1; i >= 0; i--)
            {
                var t = i / (double)count;
                result.Add(OklchToRgb(new OklchColor(
                    ok.L + (1.0 - ok.L) * t,
                    ok.C * (1.0 - t),
                    ok.H), baseColor.A));
            }
            return result;
        }

        /// <summary>Darker steps in OKLCH: L sinks toward 0 (chroma fades with it,
        /// matching how real pigments lose hue in shadow).</summary>
        public static System.Collections.Generic.List<Color> ShadesOklch(Color baseColor, int count = 5)
        {
            var ok = RgbToOklch(baseColor);
            var result = new System.Collections.Generic.List<Color>();
            for (var i = 0; i < count; i++)
            {
                var t = i / (double)count;
                result.Add(OklchToRgb(new OklchColor(
                    ok.L * (1.0 - t),
                    ok.C * (1.0 - t),
                    ok.H), baseColor.A));
            }
            return result;
        }

        /// <summary>Desaturated steps at constant lightness — chroma relaxes toward 0
        /// while L and hue stay put, so tones never drift lighter or darker.</summary>
        public static System.Collections.Generic.List<Color> TonesOklch(Color baseColor, int count = 5)
        {
            var ok = RgbToOklch(baseColor);
            var result = new System.Collections.Generic.List<Color>();
            for (var i = 0; i < count; i++)
            {
                var t = i / (double)count;
                result.Add(OklchToRgb(new OklchColor(ok.L, ok.C * (1.0 - t), ok.H), baseColor.A));
            }
            return result;
        }

        /// <summary>A full light→dark strip around the base color (tints, base, shades),
        /// rendered as the ramp preview in Analyze.</summary>
        public static System.Collections.Generic.List<Color> RampOklch(Color baseColor, int steps = 4)
        {
            var result = TintsOklch(baseColor, steps);
            result.Add(baseColor);
            result.AddRange(ShadesOklch(baseColor, steps));
            return result;
        }

        // ---------------- Palette generation helpers (§4 tints/shades) ----------------

        /// <summary>count lighter steps (mix toward white), including the base as the last item.</summary>
        public static System.Collections.Generic.List<Color> Tints(Color baseColor, int count = 5)
        {
            var result = new System.Collections.Generic.List<Color>();
            for (var i = count - 1; i >= 0; i--)
                result.Add(Mix(baseColor, Colors.White, i / (double)count));
            return result;
        }

        /// <summary>count darker steps (mix toward black), including the base as the first item.</summary>
        public static System.Collections.Generic.List<Color> Shades(Color baseColor, int count = 5)
        {
            var result = new System.Collections.Generic.List<Color>();
            for (var i = 0; i < count; i++)
                result.Add(Mix(baseColor, Colors.Black, i / (double)count));
            return result;
        }

        /// <summary>count desaturated steps (mix toward gray).</summary>
        public static System.Collections.Generic.List<Color> Tones(Color baseColor, int count = 5)
        {
            var gray = (byte)Math.Round(0.2126 * baseColor.R + 0.7152 * baseColor.G + 0.0722 * baseColor.B);
            var grayColor = Color.FromRgb(gray, gray, gray);
            var result = new System.Collections.Generic.List<Color>();
            for (var i = 0; i < count; i++)
                result.Add(Mix(baseColor, grayColor, i / (double)count));
            return result;
        }

        public static Color Mix(Color a, Color b, double t)
        {
            t = Math.Clamp(t, 0, 1);
            return Color.FromArgb(
                (byte)Math.Round(a.A + (b.A - a.A) * t),
                (byte)Math.Round(a.R + (b.R - a.R) * t),
                (byte)Math.Round(a.G + (b.G - a.G) * t),
                (byte)Math.Round(a.B + (b.B - a.B) * t));
        }

        // ---------------- Internals ----------------

        private static (double R, double G, double B) SrgbToLinear(Color c)
        {
            static double Lin(byte ch)
            {
                var s = ch / 255.0;
                return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }
            return (Lin(c.R), Lin(c.G), Lin(c.B));
        }

        private static double LinearToSrgb(double v) =>
            v <= 0.0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055;

        private static byte ToByte(double v) => (byte)Math.Clamp(Math.Round(v * 255), 0, 255);
    }
}
