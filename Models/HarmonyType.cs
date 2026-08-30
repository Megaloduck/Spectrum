namespace Spectrum.Models
{
    public enum HarmonyType
    {
        // Default "no rule" state — Generate/Space just rolls independent
        // random colors rather than deriving them from one base + rule.
        Random,
        Complementary,
        Analogous,
        Triadic,
        SplitComplementary,
        Tetradic,
        Monochromatic
    }
}