using System.Net;
using System.Net.Http.Json;
using OpenTrpg.Core.Application.ChangeRequests;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.Personality;

/// <summary>Personality tables of the SRD backgrounds and the personality of a character (phase 22).</summary>
[Collection(CatalogCollection.Name)]
public class PersonalityEndpointsTests(CatalogApiFactory factory)
{
    [Fact]
    public async Task The_srd_acolyte_has_eight_traits_and_six_ideals_bonds_and_flaws_with_alignments()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var backgrounds = await s.Player.Client.GetFromJsonAsync<List<BackgroundDto>>("/api/v1/systems/dnd5e/catalog/backgrounds");
        var acolyte = Assert.Single(backgrounds!, b => b.Index == "acolyte");

        var personality = acolyte.Personality!;
        Assert.Equal((8, 6, 6, 6), (personality.Traits.Count, personality.Ideals.Count, personality.Bonds.Count, personality.Flaws.Count));
        Assert.Equal(["Lawful", "Good", "Chaotic", "Lawful", "Lawful", "Any"], personality.Ideals.Select(i => i.Alignment));
        Assert.StartsWith("Tradition.", personality.Ideals[0].Text, StringComparison.Ordinal);
        Assert.All(personality.Traits.Concat(personality.Bonds).Concat(personality.Flaws), t => Assert.False(string.IsNullOrWhiteSpace(t)));
        Assert.Empty(acolyte.OptionalTables);
    }

    [Fact]
    public async Task The_owner_of_a_draft_writes_the_personality_directly()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId);

        var response = await s.Player.Client.PatchAsJsonAsync(SheetUrl(hero.Id), Personality("Rasgo uno.\nRasgo dos."));
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var detail = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Equal(
            ("Rasgo uno.\nRasgo dos.", "Un ideal de ejemplo.", "Un vínculo de ejemplo.", "Un defecto de ejemplo.", "Especialidad: Ejemplo"),
            (detail.PersonalityTraits, detail.Ideals, detail.Bonds, detail.Flaws, detail.BackgroundDetail));

        // Absent fields do not change; an empty text clears.
        await s.Player.Client.PatchAsJsonAsync(SheetUrl(hero.Id), new { flaws = "" });
        detail = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Equal(("", "Un ideal de ejemplo."), (detail.Flaws, detail.Ideals));
    }

    [Fact]
    public async Task The_owner_of_an_active_character_asks_the_dm_and_the_dm_writes_directly()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateActiveCharacterAsync(s.Dm, s.CampaignId);

        var owner = await s.Player.Client.PatchAsJsonAsync(SheetUrl(hero.Id), Personality("Rasgo pedido."));
        Assert.Equal(HttpStatusCode.Accepted, owner.StatusCode);
        var request = (await owner.Content.ReadFromJsonAsync<ChangeRequestDto>())!;
        Assert.Equal("EditSheet", request.Type);
        Assert.Equal("", (await s.Player.GetCharacterAsync(hero.Id)).PersonalityTraits);

        var approve = await s.Dm.Client.PostAsJsonAsync($"/api/v1/change-requests/{request.Id}/approve", new { });
        Assert.True(approve.IsSuccessStatusCode, await approve.Content.ReadAsStringAsync());
        Assert.Equal("Rasgo pedido.", (await s.Player.GetCharacterAsync(hero.Id)).PersonalityTraits);

        var dm = await s.Dm.Client.PatchAsJsonAsync(SheetUrl(hero.Id), new { bonds = "Vínculo del DM." });
        Assert.Equal(HttpStatusCode.OK, dm.StatusCode);
        Assert.Equal("Vínculo del DM.", (await s.Player.GetCharacterAsync(hero.Id)).Bonds);
    }

    [Fact]
    public async Task Personality_texts_are_limited()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId);

        var tooLong = await s.Player.Client.PatchAsJsonAsync(SheetUrl(hero.Id), new { ideals = new string('a', 1001) });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        var detailTooLong = await s.Player.Client.PatchAsJsonAsync(SheetUrl(hero.Id), new { backgroundDetail = new string('a', 201) });
        Assert.Equal(HttpStatusCode.BadRequest, detailTooLong.StatusCode);
    }

    private static object Personality(string traits) => new
    {
        personalityTraits = traits,
        ideals = "Un ideal de ejemplo.",
        bonds = "Un vínculo de ejemplo.",
        flaws = "Un defecto de ejemplo.",
        backgroundDetail = "Especialidad: Ejemplo",
    };

    private static string SheetUrl(Guid id) => $"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/sheet";
}
