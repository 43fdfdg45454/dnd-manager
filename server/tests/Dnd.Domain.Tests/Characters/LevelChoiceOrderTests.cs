using Dnd.Domain.Catalog;
using Dnd.Domain.Characters;

namespace Dnd.Domain.Tests.Characters;

/// <summary>Order of the choices of a level and option costs (phase 25, block 5).</summary>
public class LevelChoiceOrderTests
{
    private static LevelChoiceRule Rule(string key, LevelChoiceKind kind, string? after = null, string? subclass = null) => new()
    {
        Id = LevelChoiceRule.IdFor("cleric", subclass, 1, key),
        ClassIndex = "cleric",
        SubclassIndex = subclass,
        Level = 1,
        Key = key,
        Name = key,
        Kind = kind,
        Choose = 1,
        After = after,
    };

    private static List<string> Keys(IEnumerable<LevelChoiceRule> rules) => rules.Select(r => r.Key).ToList();

    [Fact]
    public void Expertise_goes_after_the_choices_that_give_skills()
    {
        var rules = new[]
        {
            Rule("pericia", LevelChoiceKind.Expertise, subclass: "dominio-ejemplo"),
            Rule("subclass", LevelChoiceKind.Subclass),
            Rule("idiomas", LevelChoiceKind.Language),
            Rule("habilidades", LevelChoiceKind.Skill, subclass: "dominio-ejemplo"),
        };

        Assert.Equal(["subclass", "idiomas", "habilidades", "pericia"], Keys(LevelChoiceOrder.Sort(rules)));
    }

    [Fact]
    public void Without_dependencies_the_original_order_is_kept()
    {
        var rules = new[]
        {
            Rule("asi", LevelChoiceKind.AsiOrFeat),
            Rule("idiomas", LevelChoiceKind.Language),
            Rule("trucos", LevelChoiceKind.CantripsKnown),
        };

        Assert.Equal(["asi", "idiomas", "trucos"], Keys(LevelChoiceOrder.Sort(rules)));
    }

    [Fact]
    public void After_forces_the_order()
    {
        var rules = new[]
        {
            Rule("herramientas", LevelChoiceKind.Tool, after: "idiomas"),
            Rule("idiomas", LevelChoiceKind.Language),
        };

        Assert.Equal(["idiomas", "herramientas"], Keys(LevelChoiceOrder.Sort(rules)));
    }

    [Fact]
    public void A_choice_explicitly_after_the_expertise_is_not_moved_before_it()
    {
        var rules = new[]
        {
            Rule("estilo", LevelChoiceKind.OptionSet, after: "pericia"),
            Rule("habilidades", LevelChoiceKind.Skill),
            Rule("pericia", LevelChoiceKind.Expertise),
        };

        Assert.Equal(["habilidades", "pericia", "estilo"], Keys(LevelChoiceOrder.Sort(rules)));
    }

    [Fact]
    public void Unknown_keys_and_cycles_do_not_lose_choices()
    {
        var unknown = new[] { Rule("a", LevelChoiceKind.Tool, after: "no-existe"), Rule("b", LevelChoiceKind.Language) };
        Assert.Equal(["a", "b"], Keys(LevelChoiceOrder.Sort(unknown)));

        var cycle = new[] { Rule("a", LevelChoiceKind.Tool, after: "b"), Rule("b", LevelChoiceKind.Language, after: "a"), Rule("c", LevelChoiceKind.Custom) };
        var sorted = Keys(LevelChoiceOrder.Sort(cycle));
        Assert.Equal(3, sorted.Count);
        Assert.Equal(["a", "b", "c"], sorted.Order().ToList());
    }

    [Theory]
    [InlineData("""{"resource":"ki","amount":2}""", "ki", 2)]
    [InlineData("""{"Resource":" sorcery-points ","Amount":20}""", "sorcery-points", 20)]
    public void Costs_are_parsed(string json, string resource, int amount)
    {
        var option = new OptionDefinition { Index = "x", SetId = "s", Name = "X", CostJson = json };
        Assert.Equal(new OptionCost(resource, amount), option.Cost);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("""{"resource":"ki","amount":0}""")]
    [InlineData("""{"resource":"ki","amount":21}""")]
    [InlineData("""{"amount":2}""")]
    public void Invalid_costs_are_ignored(string? json) => Assert.Null(LevelChoiceJson.ParseCost(json));

    [Fact]
    public void Class_resource_keys_include_every_level()
    {
        Assert.Equal("Ki", ClassResourceRules.KeysFor("monk")["ki"]);
        Assert.Contains("indomitable", ClassResourceRules.KeysFor("fighter").Keys);
        Assert.Contains("natural-recovery", ClassResourceRules.KeysFor("druid").Keys);
        Assert.Empty(ClassResourceRules.KeysFor("rogue"));
        Assert.Equal("Sorcery Points", ClassResourceRules.NameOf("sorcery-points"));
        Assert.Null(ClassResourceRules.NameOf("no-existe"));
    }
}
