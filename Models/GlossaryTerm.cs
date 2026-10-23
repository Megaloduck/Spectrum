namespace Spectrum.Models
{
    /// <summary>One entry in the color glossary: a term and its plain-language
    /// definition. Static reference content, independent of the active palette.</summary>
    public sealed record GlossaryTerm(string Term, string Definition);
}
