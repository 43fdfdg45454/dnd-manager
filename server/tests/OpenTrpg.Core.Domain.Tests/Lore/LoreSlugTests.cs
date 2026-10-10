using OpenTrpg.Core.Domain.Lore;

namespace OpenTrpg.Core.Domain.Tests.Lore;

public sealed class LoreSlugTests
{
    [Theory]
    [InlineData("La Ciudad de Phandalin", "la-ciudad-de-phandalin")]
    [InlineData("  Árbol   del Ñandú!! ", "arbol-del-nandu")]
    [InlineData("Kobold #3 (jefe)", "kobold-3-jefe")]
    [InlineData("--Hola--", "hola")]
    [InlineData("¿¿??", "entrada")]
    [InlineData("", "entrada")]
    public void From_normalizes_titles(string title, string expected)
    {
        Assert.Equal(expected, LoreSlug.From(title));
    }

    [Fact]
    public void From_never_exceeds_the_maximum_length_or_ends_with_a_hyphen()
    {
        var slug = LoreSlug.From(new string('a', 99) + " bbb");

        Assert.True(slug.Length <= LoreSlug.MaxLength);
        Assert.False(slug.EndsWith('-'));
    }

    [Fact]
    public void Attempt_adds_a_numeric_suffix_within_the_maximum_length()
    {
        Assert.Equal("ciudad", LoreSlug.Attempt("ciudad", 1));
        Assert.Equal("ciudad-2", LoreSlug.Attempt("ciudad", 2));
        Assert.Equal("ciudad-10", LoreSlug.Attempt("ciudad", 10));

        var longSlug = new string('a', LoreSlug.MaxLength);
        Assert.Equal(LoreSlug.MaxLength, LoreSlug.Attempt(longSlug, 12).Length);
        Assert.EndsWith("-12", LoreSlug.Attempt(longSlug, 12));
    }
}
