using System.Net;
using System.Net.Http.Json;
using Dnd.Application.ChangeRequests;
using Dnd.Application.Characters;

namespace Dnd.Api.Tests;

[Collection(CatalogCollection.Name)]
public class CharacterEndpointsTests(CatalogApiFactory factory)
{
    private static readonly object Wizard5 = new
    {
        classes = new[] { new { classIndex = "wizard", level = 5 } },
        baseAbilities = new { str = 8, dex = 14, con = 12, @int = 18, wis = 10, cha = 10 },
        applyRacialBonuses = false,
    };

    // ---- Creation and visibility ---------------------------------------------------------------

    [Fact]
    public async Task Member_creates_own_draft_with_default_scores()
    {
        var s = await factory.CreateCampaignScenarioAsync();

        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "  Aria  " });

        Assert.Equal("Aria", character.Name);
        Assert.Equal("Draft", character.Status);
        Assert.Equal(s.Player.Id, character.OwnerUserId);
        Assert.Equal("Player User", character.OwnerDisplayName);
        Assert.All(new[] { character.BaseStr, character.BaseDex, character.BaseCon, character.BaseInt, character.BaseWis, character.BaseCha }, v => Assert.Equal(10, v));
        Assert.Equal(0, character.Sheet.Abilities["str"].Modifier);
        Assert.Empty(character.PendingChangeRequests);
    }

    [Fact]
    public async Task Outsider_cannot_create_or_list_characters()
    {
        var s = await factory.CreateCampaignScenarioAsync();

        var create = await s.Outsider.Client.PostAsJsonAsync(CharactersUrl(s.CampaignId), new { name = "Intruso" });
        var list = await s.Outsider.Client.GetAsync(CharactersUrl(s.CampaignId));

        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
    }

    [Fact]
    public async Task Create_requires_a_name()
    {
        var s = await factory.CreateCampaignScenarioAsync();

        var response = await s.Player.Client.PostAsJsonAsync(CharactersUrl(s.CampaignId), new { name = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadProblemAsync()).HasFieldError("name"));
    }

    [Fact]
    public async Task Owner_user_id_absent_makes_the_player_the_owner()
    {
        var s = await factory.CreateCampaignScenarioAsync();

        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Propio" });

        Assert.Equal(s.Player.Id, character.OwnerUserId);
    }

    [Theory]
    [InlineData(CampaignScenario.DmRole, false)]
    [InlineData(CampaignScenario.DmRole, true)]
    [InlineData(CampaignScenario.OwnerRole, false)]
    [InlineData(CampaignScenario.OwnerRole, true)]
    public async Task Dm_cannot_own_a_character(string role, bool explicitSelf)
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var actor = s.As(role);

        object body = explicitSelf ? new { name = "Propio", ownerUserId = actor.Id } : new { name = "Propio" };
        var response = await actor.Client.PostAsJsonAsync(CharactersUrl(s.CampaignId), body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.True(problem.HasFieldError("ownerUserId"));
        Assert.Equal(
            "Un DM no tiene personajes propios: crea un PNJ o asígnalo a un jugador.",
            problem.GetProperty("errors").GetProperty("ownerUserId")[0].GetString());
        Assert.Empty(await ListAsync(s.Dm, s.CampaignId));
    }

    [Theory]
    [InlineData(CampaignScenario.DmRole, CampaignScenario.OwnerRole)]
    [InlineData(CampaignScenario.OwnerRole, CampaignScenario.DmRole)]
    public async Task Dm_cannot_create_a_character_for_another_dm(string actorRole, string ownerRole)
    {
        var s = await factory.CreateCampaignScenarioAsync();

        var response = await s.As(actorRole).Client.PostAsJsonAsync(
            CharactersUrl(s.CampaignId), new { name = "Ajeno", ownerUserId = s.As(ownerRole).Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.True(problem.HasFieldError("ownerUserId"));
        Assert.Equal(
            "El dueño debe ser un jugador de la campaña.",
            problem.GetProperty("errors").GetProperty("ownerUserId")[0].GetString());
        Assert.Empty(await ListAsync(s.Dm, s.CampaignId));
    }

    [Fact]
    public async Task Dm_creates_a_character_for_another_member()
    {
        var s = await factory.CreateCampaignScenarioAsync();

        var character = await CreateAsync(s.Dm, s.CampaignId, new { name = "Para el jugador", ownerUserId = s.Player.Id });

        Assert.Equal(s.Player.Id, character.OwnerUserId);
        var detail = await s.Player.Client.GetAsync(CharacterUrl(character.Id));
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    }

    [Fact]
    public async Task Dm_cannot_assign_a_character_to_a_non_member()
    {
        var s = await factory.CreateCampaignScenarioAsync();

        var response = await s.Dm.Client.PostAsJsonAsync(CharactersUrl(s.CampaignId), new { name = "Ajeno", ownerUserId = s.Outsider.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadProblemAsync()).HasFieldError("ownerUserId"));
    }

    [Fact]
    public async Task Explicit_null_owner_creates_an_npc_only_the_dm_sees_in_full()
    {
        var s = await factory.CreateCampaignScenarioAsync();

        var npc = await CreateAsync(s.Dm, s.CampaignId, new { name = "Tabernero", ownerUserId = (Guid?)null });

        Assert.Null(npc.OwnerUserId);
        Assert.Null(npc.OwnerDisplayName);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.GetAsync(CharacterUrl(npc.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Owner.Client.GetAsync(CharacterUrl(npc.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Player.Client.GetAsync(CharacterUrl(npc.Id))).StatusCode);

        var summary = Assert.Single(await ListAsync(s.Player, s.CampaignId), c => c.Id == npc.Id);
        Assert.Null(summary.HitPointsCurrent);
        Assert.Null(summary.HitPointsMax);
    }

    [Fact]
    public async Task Player_cannot_create_an_npc_or_a_character_for_someone_else()
    {
        var s = await factory.CreateCampaignScenarioAsync();

        var npc = await s.Player.Client.PostAsJsonAsync(CharactersUrl(s.CampaignId), new { name = "PNJ", ownerUserId = (Guid?)null });
        var other = await s.Player.Client.PostAsJsonAsync(CharactersUrl(s.CampaignId), new { name = "Otro", ownerUserId = s.Dm.Id });
        var self = await s.Player.Client.PostAsJsonAsync(CharactersUrl(s.CampaignId), new { name = "Yo", ownerUserId = s.Player.Id });

        Assert.Equal(HttpStatusCode.Forbidden, npc.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        Assert.Equal(HttpStatusCode.Created, self.StatusCode);
    }

    // ---- Owner reassignment ----------------------------------------------------------------------

    [Theory]
    [InlineData(CampaignScenario.DmRole)]
    [InlineData(CampaignScenario.OwnerRole)]
    public async Task Dm_hands_an_npc_to_a_player_and_turns_it_back_into_an_npc(string role)
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var dm = s.As(role);
        var npc = await CreateAsync(dm, s.CampaignId, new { name = "Escudero", ownerUserId = (Guid?)null });

        var handed = await PutOwnerAsync(dm, npc.Id, new { ownerUserId = s.Player.Id });

        Assert.Equal(s.Player.Id, handed.OwnerUserId);
        Assert.Equal("Player User", handed.OwnerDisplayName);
        Assert.Equal(s.Player.Id, (await GetDetailAsync(s.Player, npc.Id)).OwnerUserId);
        Assert.Equal(s.Player.Id, Assert.Single(await ListAsync(s.Player, s.CampaignId)).OwnerUserId);

        var back = await PutOwnerAsync(dm, npc.Id, new { ownerUserId = (Guid?)null });

        Assert.Null(back.OwnerUserId);
        Assert.Null(back.OwnerDisplayName);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Player.Client.GetAsync(CharacterUrl(npc.Id))).StatusCode);
        Assert.Empty(await ListChangeRequestsAsync(s.Dm, s.CampaignId, null));
    }

    [Fact]
    public async Task Dm_reassigns_a_character_between_players()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var second = await factory.CreateSignedInUserAsync("Second Player");
        await s.Owner.AddMemberAsync(s.CampaignId, second, CampaignScenario.PlayerRole);
        var hero = await CreateAsync(s.Player, s.CampaignId, new { name = "Heredado" });

        var moved = await PutOwnerAsync(s.Dm, hero.Id, new { ownerUserId = second.Id });

        Assert.Equal(second.Id, moved.OwnerUserId);
        Assert.Equal(HttpStatusCode.OK, (await second.Client.GetAsync(CharacterUrl(hero.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Player.Client.GetAsync(CharacterUrl(hero.Id))).StatusCode);
    }

    [Fact]
    public async Task Owner_reassignment_rejects_dms_non_members_and_a_missing_owner()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await CreateAsync(s.Player, s.CampaignId, new { name = "Firme" });

        var toDm = await s.Dm.Client.PutAsJsonAsync(OwnerUrl(hero.Id), new { ownerUserId = s.Dm.Id });
        var toOwner = await s.Dm.Client.PutAsJsonAsync(OwnerUrl(hero.Id), new { ownerUserId = s.Owner.Id });
        var toOutsider = await s.Dm.Client.PutAsJsonAsync(OwnerUrl(hero.Id), new { ownerUserId = s.Outsider.Id });
        var missing = await s.Dm.Client.PutAsJsonAsync(OwnerUrl(hero.Id), new { });

        foreach (var response in new[] { toDm, toOwner, toOutsider, missing })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.True((await response.ReadProblemAsync()).HasFieldError("ownerUserId"));
        }

        Assert.Equal(s.Player.Id, (await GetDetailAsync(s.Dm, hero.Id)).OwnerUserId);
    }

    [Fact]
    public async Task Player_cannot_reassign_a_character()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await CreateAsync(s.Player, s.CampaignId, new { name = "Mío" });

        var toNpc = await s.Player.Client.PutAsJsonAsync(OwnerUrl(hero.Id), new { ownerUserId = (Guid?)null });
        var outsider = await s.Outsider.Client.PutAsJsonAsync(OwnerUrl(hero.Id), new { ownerUserId = (Guid?)null });

        Assert.Equal(HttpStatusCode.Forbidden, toNpc.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsider.StatusCode);
        Assert.Equal(s.Player.Id, (await GetDetailAsync(s.Player, hero.Id)).OwnerUserId);
    }

    [Fact]
    public async Task Member_who_is_neither_owner_nor_dm_gets_403_on_the_detail()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var other = await factory.CreateSignedInUserAsync("Other Player");
        await s.Owner.AddMemberAsync(s.CampaignId, other, CampaignScenario.PlayerRole);
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Privado" });

        var asOther = await other.Client.GetAsync(CharacterUrl(character.Id));
        var asOutsider = await s.Outsider.Client.GetAsync(CharacterUrl(character.Id));

        Assert.Equal(HttpStatusCode.Forbidden, asOther.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, asOutsider.StatusCode);
    }

    [Fact]
    public async Task Summary_hides_hit_points_from_other_players()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var other = await factory.CreateSignedInUserAsync("Other Player");
        await s.Owner.AddMemberAsync(s.CampaignId, other, CampaignScenario.PlayerRole);
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Bruna" });
        await PatchSheetAsync(s.Player, character.Id, new
        {
            raceIndex = "dwarf",
            subraceIndex = "hill-dwarf",
            classes = new[] { new { classIndex = "barbarian", level = 1 } },
            baseAbilities = new { str = 16, dex = 12, con = 14, @int = 8, wis = 10, cha = 10 },
            applyRacialBonuses = false,
        });

        var asOther = Assert.Single(await ListAsync(other, s.CampaignId));
        var asOwner = Assert.Single(await ListAsync(s.Player, s.CampaignId));
        var asDm = Assert.Single(await ListAsync(s.Dm, s.CampaignId));

        Assert.Null(asOther.HitPointsCurrent);
        Assert.Null(asOther.HitPointsMax);
        Assert.Equal("Dwarf", asOther.RaceName);
        var characterClass = Assert.Single(asOther.Classes);
        Assert.Equal("Barbarian", characterClass.ClassName);
        Assert.Equal(1, asOther.Level);
        Assert.Equal(14, asOwner.HitPointsMax);
        Assert.Equal(14, asOwner.HitPointsCurrent);
        Assert.Equal(14, asDm.HitPointsMax);
    }

    // ---- Sheet edits and approval ----------------------------------------------------------------

    [Fact]
    public async Task Owner_edits_a_draft_directly()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Borrador" });

        var response = await s.Player.Client.PatchAsJsonAsync(SheetUrl(character.Id), new
        {
            name = "Nuevo nombre",
            raceIndex = "dwarf",
            classes = new[] { new { classIndex = "barbarian", level = 1 } },
            baseAbilities = new { str = 16, dex = 12, con = 14, @int = 8, wis = 10, cha = 10 },
            proficiencies = new[] { new { type = "SavingThrow", key = "str", expertise = false } },
            overrides = new[] { new { field = "speed", value = 35, note = "Botas" } },
            copperPieces = 1500,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        Assert.Equal("Nuevo nombre", detail.Name);
        Assert.Equal("dwarf", detail.RaceIndex);
        Assert.Equal("Dwarf", detail.RaceName);
        Assert.Equal(16, detail.Sheet.Abilities["con"].Score); // 14 + 2 (dwarf)
        Assert.Equal(15, detail.Sheet.HitPointsMax); // 12 + 3
        Assert.Equal(15, detail.HitPointsCurrent);
        Assert.Equal(5, detail.Sheet.SavingThrows["str"].Value);
        Assert.True(detail.Sheet.SavingThrows["str"].Proficient);
        Assert.Equal(35, detail.Sheet.Speed);
        Assert.Contains("speed", detail.Sheet.OverriddenFields);
        Assert.Equal(1500, detail.CopperPieces);
        var hitDice = Assert.Single(detail.Sheet.HitDice);
        Assert.Equal(12, hitDice.Die);
    }

    [Fact]
    public async Task Explicit_null_clears_the_race_and_absent_fields_do_not_change()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Sin raza" });
        await PatchSheetAsync(s.Player, character.Id, new { raceIndex = "dwarf", subraceIndex = "hill-dwarf", alignment = "Lawful Good" });

        var unchanged = await PatchSheetAsync(s.Player, character.Id, new { notes = "Notas" });
        Assert.Equal("dwarf", unchanged.RaceIndex);
        Assert.Equal("hill-dwarf", unchanged.SubraceIndex);
        Assert.Equal("Lawful Good", unchanged.Alignment);

        var cleared = await PatchSheetAsync(s.Player, character.Id, new { raceIndex = (string?)null, alignment = (string?)null });
        Assert.Null(cleared.RaceIndex);
        Assert.Null(cleared.SubraceIndex);
        Assert.Null(cleared.Alignment);
        Assert.Equal("Notas", cleared.Notes);
    }

    [Fact]
    public async Task Sheet_patch_is_validated()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Validado" });

        var scores = await s.Player.Client.PatchAsJsonAsync(SheetUrl(character.Id), new { baseAbilities = new { str = 31, dex = 10, con = 10, @int = 10, wis = 10, cha = 0 } });
        var level = await s.Player.Client.PatchAsJsonAsync(SheetUrl(character.Id), new { classes = new[] { new { classIndex = "wizard", level = 21 } } });
        var field = await s.Player.Client.PatchAsJsonAsync(SheetUrl(character.Id), new { overrides = new[] { new { field = "luck", value = 1 } } });
        var type = await s.Player.Client.PatchAsJsonAsync(SheetUrl(character.Id), new { proficiencies = new[] { new { type = "Magic", key = "x", expertise = false } } });
        var unknownClass = await s.Player.Client.PatchAsJsonAsync(SheetUrl(character.Id), new { classes = new[] { new { classIndex = "gunslinger", level = 1 } } });
        var wrongSubrace = await s.Player.Client.PatchAsJsonAsync(SheetUrl(character.Id), new { raceIndex = "elf", subraceIndex = "hill-dwarf" });

        Assert.True((await scores.ReadProblemAsync()).HasFieldError("baseAbilities.str"));
        Assert.Equal(HttpStatusCode.BadRequest, level.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, field.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, type.StatusCode);
        Assert.True((await unknownClass.ReadProblemAsync()).HasFieldError("classes"));
        Assert.True((await wrongSubrace.ReadProblemAsync()).HasFieldError("subraceIndex"));
    }

    [Fact]
    public async Task Edit_after_activation_needs_approval_and_the_dm_approval_applies_it()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Activo" });
        await ActivateAsync(s.Dm, character.Id);

        var response = await s.Player.Client.PatchAsJsonAsync(SheetUrl(character.Id), new { name = "Renombrado", raceIndex = (string?)null, copperPieces = 50 });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var request = (await response.Content.ReadFromJsonAsync<ChangeRequestDto>())!;
        Assert.Equal("EditSheet", request.Type);
        Assert.Equal("Pending", request.Status);
        Assert.Equal("Activo", request.CharacterName);
        Assert.Equal("Player User", request.RequestedByDisplayName);
        Assert.Equal("Renombrado", request.Payload.GetProperty("name").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, request.Payload.GetProperty("raceIndex").ValueKind);
        Assert.False(request.Payload.TryGetProperty("notes", out _));

        var pending = await GetDetailAsync(s.Player, character.Id);
        Assert.Equal("Activo", pending.Name);
        Assert.Single(pending.PendingChangeRequests);

        var approved = await ApproveAsync(s.Dm, request.Id);
        Assert.Equal("Approved", approved.Status);
        Assert.Equal("Dm User", approved.ResolvedByDisplayName);
        Assert.NotNull(approved.ResolvedAt);

        var after = await GetDetailAsync(s.Player, character.Id);
        Assert.Equal("Renombrado", after.Name);
        Assert.Equal(50, after.CopperPieces);
        Assert.Empty(after.PendingChangeRequests);
    }

    [Fact]
    public async Task Dm_edits_an_active_character_directly()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Activo" });
        await ActivateAsync(s.Dm, character.Id);

        var response = await s.Dm.Client.PatchAsJsonAsync(SheetUrl(character.Id), new { name = "Cambiado por el DM" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Player_who_is_not_dm_cannot_approve()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Pidiendo" });
        var request = await SubmitAsync(s.Player, character.Id);

        var response = await s.Player.Client.PostAsJsonAsync($"/api/v1/change-requests/{request.Id}/approve", new { comment = "yo mismo" });
        var reject = await s.Player.Client.PostAsJsonAsync($"/api/v1/change-requests/{request.Id}/reject", new { comment = "no" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reject.StatusCode);
    }

    [Fact]
    public async Task Submit_and_approve_activates_the_character_at_full_hit_points()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Listo" });
        await PatchSheetAsync(s.Player, character.Id, new { classes = new[] { new { classIndex = "fighter", level = 2 } } });

        var request = await SubmitAsync(s.Player, character.Id);
        Assert.Equal("Activate", request.Type);
        var duplicate = await s.Player.Client.PostAsync($"{CharacterUrl(character.Id)}/submit", null);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        await ApproveAsync(s.Dm, request.Id);

        var detail = await GetDetailAsync(s.Player, character.Id);
        Assert.Equal("Active", detail.Status);
        Assert.Equal(16, detail.HitPointsCurrent); // 10 + 6
    }

    [Fact]
    public async Task Approving_twice_returns_409()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Dos veces" });
        var request = await SubmitAsync(s.Player, character.Id);
        await ApproveAsync(s.Dm, request.Id);

        var again = await s.Dm.Client.PostAsJsonAsync($"/api/v1/change-requests/{request.Id}/approve", new { });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Direct_activation_approves_pending_activation_requests()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Directo" });
        var request = await SubmitAsync(s.Player, character.Id);

        await ActivateAsync(s.Dm, character.Id);

        var after = await GetChangeRequestAsync(s.Player, request.Id);
        Assert.Equal("Approved", after.Status);
        var player = await s.Player.Client.PostAsync($"{CharacterUrl(character.Id)}/activate", null);
        Assert.Equal(HttpStatusCode.Forbidden, player.StatusCode);
    }

    [Fact]
    public async Task Reject_requires_a_comment_and_keeps_the_sheet()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Rechazado" });
        await ActivateAsync(s.Dm, character.Id);
        var request = await PatchSheetForApprovalAsync(s.Player, character.Id, new { name = "Otro" });

        var empty = await s.Dm.Client.PostAsJsonAsync($"/api/v1/change-requests/{request.Id}/reject", new { comment = "" });
        var rejected = await s.Dm.Client.PostAsJsonAsync($"/api/v1/change-requests/{request.Id}/reject", new { comment = "Demasiado oro" });

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var dto = (await rejected.Content.ReadFromJsonAsync<ChangeRequestDto>())!;
        Assert.Equal("Rejected", dto.Status);
        Assert.Equal("Demasiado oro", dto.Comment);
        Assert.Equal("Rechazado", (await GetDetailAsync(s.Player, character.Id)).Name);
    }

    [Fact]
    public async Task Only_the_requester_cancels_a_request()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Cancelable" });
        var request = await SubmitAsync(s.Player, character.Id);

        var byDm = await s.Dm.Client.PostAsync($"/api/v1/change-requests/{request.Id}/cancel", null);
        var byOutsider = await s.Outsider.Client.PostAsync($"/api/v1/change-requests/{request.Id}/cancel", null);
        var byRequester = await s.Player.Client.PostAsync($"/api/v1/change-requests/{request.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.Forbidden, byDm.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, byOutsider.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byRequester.StatusCode);
        Assert.Equal("Cancelled", (await byRequester.Content.ReadFromJsonAsync<ChangeRequestDto>())!.Status);
    }

    [Fact]
    public async Task Change_request_list_shows_all_to_dms_and_own_ones_to_players()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var other = await factory.CreateSignedInUserAsync("Other Player");
        await s.Owner.AddMemberAsync(s.CampaignId, other, CampaignScenario.PlayerRole);
        var mine = await CreateAsync(s.Player, s.CampaignId, new { name = "Mío" });
        var theirs = await CreateAsync(other, s.CampaignId, new { name = "Suyo" });
        var myRequest = await SubmitAsync(s.Player, mine.Id);
        var theirRequest = await SubmitAsync(other, theirs.Id);
        await ApproveAsync(s.Dm, theirRequest.Id);

        var all = await ListChangeRequestsAsync(s.Dm, s.CampaignId, null);
        var pending = await ListChangeRequestsAsync(s.Dm, s.CampaignId, "Pending");
        var own = await ListChangeRequestsAsync(s.Player, s.CampaignId, null);
        var invalid = await s.Dm.Client.GetAsync($"/api/v1/campaigns/{s.CampaignId}/change-requests?status=Whatever");

        Assert.Equal(2, all.Count);
        Assert.Equal(myRequest.Id, Assert.Single(pending).Id);
        Assert.Equal(myRequest.Id, Assert.Single(own).Id);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Player.Client.GetAsync($"/api/v1/change-requests/{theirRequest.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.GetAsync($"/api/v1/change-requests/{theirRequest.Id}")).StatusCode);
    }

    // ---- Calculated values -----------------------------------------------------------------------

    [Fact]
    public async Task Barbarian_level_3_has_three_rages()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Furia" });

        var detail = await PatchSheetAsync(s.Player, character.Id, new { classes = new[] { new { classIndex = "barbarian", level = 3 } } });

        var rage = Assert.Single(detail.Resources, r => r.Key == "rage");
        Assert.Equal(3, rage.Max);
        Assert.True(rage.IsAuto);
        Assert.Equal("LongRest", rage.Recharge);
    }

    [Fact]
    public async Task Wizard_level_5_has_slots_4_3_2_and_spell_names()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Maga" });
        await PatchSheetAsync(s.Player, character.Id, Wizard5);

        var detail = await PatchSheetAsync(s.Player, character.Id, new
        {
            spells = new[]
            {
                new { spellIndex = "magic-missile", classIndex = "wizard", isPrepared = true, alwaysPrepared = false },
                new { spellIndex = "fire-bolt", classIndex = "wizard", isPrepared = false, alwaysPrepared = false },
            },
        });

        Assert.Equal([1, 2, 3], detail.SpellSlots.Select(x => x.Level));
        Assert.Equal([4, 3, 2], detail.SpellSlots.Select(x => x.Max));
        var casting = Assert.Single(detail.Sheet.Spellcasting);
        Assert.Equal(15, casting.SaveDc);
        Assert.Equal(7, casting.AttackBonus);
        var missile = Assert.Single(detail.Spells, x => x.SpellIndex == "magic-missile");
        Assert.Equal("Magic Missile", missile.SpellName);
        Assert.Equal(1, missile.SpellLevel);
        Assert.Equal(0, Assert.Single(detail.Spells, x => x.SpellIndex == "fire-bolt").SpellLevel);
    }

    [Fact]
    public async Task Unknown_spell_is_rejected()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Maga" });

        var response = await s.Player.Client.PatchAsJsonAsync(SheetUrl(character.Id), new
        {
            spells = new[] { new { spellIndex = "not-a-spell", classIndex = "wizard", isPrepared = true } },
        });

        Assert.True((await response.ReadProblemAsync()).HasFieldError("spells"));
    }

    [Fact]
    public async Task Warlock_level_3_has_two_pact_slots_as_level_0()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Brujo" });

        var detail = await PatchSheetAsync(s.Player, character.Id, new { classes = new[] { new { classIndex = "warlock", level = 3 } } });

        var pact = Assert.Single(detail.SpellSlots);
        Assert.Equal(0, pact.Level);
        Assert.Equal(2, pact.Max);
        Assert.Equal(2, detail.Sheet.PactSlotLevel);

        var spent = await PostAsync(s.Player, $"{CharacterUrl(character.Id)}/spell-slots/0/spend", new { amount = 2 });
        Assert.Equal(2, Assert.Single(spent.SpellSlots).Used);
        var rested = await PostAsync(s.Dm, $"{CharacterUrl(character.Id)}/rest/short", new { hitDice = new Dictionary<string, int>() });
        Assert.Equal(0, Assert.Single(rested.SpellSlots).Used);
    }

    // ---- Combat tracking -------------------------------------------------------------------------

    [Fact]
    public async Task Spending_a_slot_without_any_left_returns_400()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Maga" });
        await PatchSheetAsync(s.Player, character.Id, Wizard5);

        var spent = await PostAsync(s.Player, $"{CharacterUrl(character.Id)}/spell-slots/3/spend", new { amount = 2 });
        Assert.Equal(2, Assert.Single(spent.SpellSlots, x => x.Level == 3).Used);

        var none = await s.Player.Client.PostAsync($"{CharacterUrl(character.Id)}/spell-slots/3/spend", null);
        var noSlotsOfLevel = await s.Player.Client.PostAsJsonAsync($"{CharacterUrl(character.Id)}/spell-slots/4/spend", new { amount = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noSlotsOfLevel.StatusCode);
    }

    [Fact]
    public async Task Long_rest_restores_hit_points_and_slots()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Cansada" });
        await PatchSheetAsync(s.Player, character.Id, Wizard5);
        var active = await ActivateAsync(s.Dm, character.Id);
        var max = active.Sheet.HitPointsMax;

        var hurt = await PatchCombatAsync(s.Player, character.Id, new { hitPointsCurrent = 3, temporaryHitPoints = 4, exhaustionLevel = 2, deathSaveFailures = 1 });
        Assert.Equal(3, hurt.HitPointsCurrent);
        await PostAsync(s.Player, $"{CharacterUrl(character.Id)}/spell-slots/1/spend", new { amount = 3 });
        await PostAsync(s.Player, $"{CharacterUrl(character.Id)}/concentration", new { spellIndex = "shield" });

        var rested = await PostAsync(s.Dm, $"{CharacterUrl(character.Id)}/rest/long", null);

        Assert.Equal(max, rested.HitPointsCurrent);
        Assert.Equal(4, rested.TemporaryHitPoints);
        Assert.Equal(1, rested.ExhaustionLevel);
        Assert.Equal(0, rested.DeathSaveFailures);
        Assert.Null(rested.ConcentratingOnSpellIndex);
        Assert.All(rested.SpellSlots, x => Assert.Equal(0, x.Used));
    }

    [Fact]
    public async Task Short_rest_spends_hit_dice()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Guerrero" });
        await PatchSheetAsync(s.Player, character.Id, new { classes = new[] { new { classIndex = "fighter", level = 3 } } });
        await ActivateAsync(s.Dm, character.Id);
        await PatchCombatAsync(s.Player, character.Id, new { hitPointsCurrent = 1 });

        var rested = await PostAsync(s.Dm, $"{CharacterUrl(character.Id)}/rest/short", new { hitDice = new Dictionary<string, int> { ["fighter"] = 2 } });
        var tooMany = await s.Dm.Client.PostAsJsonAsync($"{CharacterUrl(character.Id)}/rest/short", new { hitDice = new Dictionary<string, int> { ["fighter"] = 2 } });

        Assert.Equal(2, rested.HitDiceUsed["fighter"]);
        Assert.Equal(1, Assert.Single(rested.Sheet.HitDice).Remaining);
        Assert.InRange(rested.HitPointsCurrent, 3, 21);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
    }

    [Fact]
    public async Task Combat_update_sets_conditions_and_inspiration()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Envenenado" });

        var updated = await PatchCombatAsync(s.Dm, character.Id, new { conditions = new[] { new { index = "poisoned", note = "1 hora" } }, inspiration = true });

        var condition = Assert.Single(updated.Conditions);
        Assert.Equal("poisoned", condition.Index);
        Assert.Equal("1 hora", condition.Note);
        Assert.True(updated.Inspiration);
        var other = await factory.CreateSignedInUserAsync();
        await s.Owner.AddMemberAsync(s.CampaignId, other, CampaignScenario.PlayerRole);
        var forbidden = await other.Client.PatchAsJsonAsync($"{CharacterUrl(character.Id)}/combat", new { inspiration = false });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Manual_resources_can_be_added_spent_and_deleted_but_auto_ones_cannot_be_deleted()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Monje" });
        var monk = await PatchSheetAsync(s.Player, character.Id, new { classes = new[] { new { classIndex = "monk", level = 4 } } });
        var ki = Assert.Single(monk.Resources, r => r.Key == "ki");

        var created = await s.Player.Client.PostAsJsonAsync($"{CharacterUrl(character.Id)}/resources", new { name = "Varita", max = 7, recharge = "Dawn" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var wand = (await created.Content.ReadFromJsonAsync<CharacterResourceDto>())!;
        Assert.False(wand.IsAuto);

        var spent = await PostAsync(s.Player, $"{CharacterUrl(character.Id)}/resources/{wand.Id}/spend", new { amount = 7 });
        Assert.Equal(7, Assert.Single(spent.Resources, r => r.Id == wand.Id).Used);
        var overspend = await s.Player.Client.PostAsync($"{CharacterUrl(character.Id)}/resources/{wand.Id}/spend", null);
        Assert.Equal(HttpStatusCode.BadRequest, overspend.StatusCode);
        var restored = await PostAsync(s.Player, $"{CharacterUrl(character.Id)}/resources/{wand.Id}/restore", new { amount = 2 });
        Assert.Equal(5, Assert.Single(restored.Resources, r => r.Id == wand.Id).Used);

        var badRecharge = await s.Player.Client.PostAsJsonAsync($"{CharacterUrl(character.Id)}/resources", new { name = "X", max = 1, recharge = "Weekly" });
        Assert.Equal(HttpStatusCode.BadRequest, badRecharge.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.DeleteAsync($"{CharacterUrl(character.Id)}/resources/{ki.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await s.Player.Client.DeleteAsync($"{CharacterUrl(character.Id)}/resources/{wand.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Player.Client.PostAsync($"{CharacterUrl(character.Id)}/resources/{wand.Id}/spend", null)).StatusCode);
    }

    [Fact]
    public async Task Only_a_dm_restores_an_auto_class_resource()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Monje" });
        var monk = await PatchSheetAsync(s.Player, character.Id, new { classes = new[] { new { classIndex = "monk", level = 4 } } });
        var ki = Assert.Single(monk.Resources, r => r.Key == "ki");
        Assert.True(ki.IsAuto);
        await PostAsync(s.Player, $"{CharacterUrl(character.Id)}/resources/{ki.Id}/spend", new { amount = 2 });

        var forbidden = await s.Player.Client.PostAsJsonAsync($"{CharacterUrl(character.Id)}/resources/{ki.Id}/restore", new { amount = 1 });
        var restored = await PostAsync(s.Dm, $"{CharacterUrl(character.Id)}/resources/{ki.Id}/restore", new { amount = 1 });
        var byOwner = await PostAsync(s.Owner, $"{CharacterUrl(character.Id)}/resources/{ki.Id}/restore", new { amount = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(1, Assert.Single(restored.Resources, r => r.Id == ki.Id).Used);
        Assert.Equal(0, Assert.Single(byOwner.Resources, r => r.Id == ki.Id).Used);
    }

    [Fact]
    public async Task A_player_restores_sorcery_points_with_font_of_magic()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Hechicera" });
        var sorcerer = await PatchSheetAsync(s.Player, character.Id, new { classes = new[] { new { classIndex = "sorcerer", level = 3 } } });
        var points = Assert.Single(sorcerer.Resources, r => r.Key == "sorcery-points");
        Assert.True(points.IsAuto);
        await PostAsync(s.Player, $"{CharacterUrl(character.Id)}/resources/{points.Id}/spend", new { amount = 3 });

        var restored = await PostAsync(s.Player, $"{CharacterUrl(character.Id)}/resources/{points.Id}/restore", new { amount = 2 });

        Assert.Equal(1, Assert.Single(restored.Resources, r => r.Id == points.Id).Used);
    }

    // ---- Deletion --------------------------------------------------------------------------------

    [Fact]
    public async Task Owner_deletes_a_draft_but_only_a_dm_deletes_an_active_character()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var draft = await CreateAsync(s.Player, s.CampaignId, new { name = "Borrable" });
        var active = await CreateAsync(s.Player, s.CampaignId, new { name = "Activo" });
        await SubmitAsync(s.Player, active.Id);
        await ActivateAsync(s.Dm, active.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await s.Player.Client.DeleteAsync(CharacterUrl(draft.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Player.Client.DeleteAsync(CharacterUrl(active.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await s.Dm.Client.DeleteAsync(CharacterUrl(active.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Dm.Client.GetAsync(CharacterUrl(active.Id))).StatusCode);
        Assert.Empty(await ListChangeRequestsAsync(s.Dm, s.CampaignId, null));
    }

    [Fact]
    public async Task Deleting_a_campaign_deletes_its_characters()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await CreateAsync(s.Player, s.CampaignId, new { name = "Huérfano" });
        await PatchSheetAsync(s.Player, character.Id, new { classes = new[] { new { classIndex = "barbarian", level = 2 } } });
        await SubmitAsync(s.Player, character.Id);

        var response = await s.Owner.Client.DeleteAsync(s.Url);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Owner.Client.GetAsync(CharacterUrl(character.Id))).StatusCode);
    }

    // ---- Helpers ---------------------------------------------------------------------------------

    private static string CharactersUrl(Guid campaignId) => $"/api/v1/campaigns/{campaignId}/characters";

    private static string CharacterUrl(Guid id) => $"/api/v1/characters/{id}";

    private static string SheetUrl(Guid id) => $"{CharacterUrl(id)}/sheet";

    private static string OwnerUrl(Guid id) => $"{CharacterUrl(id)}/owner";

    private static async Task<CharacterDetailDto> PutOwnerAsync(SignedInUser actor, Guid id, object body)
    {
        var response = await actor.Client.PutAsJsonAsync(OwnerUrl(id), body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    private static async Task<CharacterDetailDto> CreateAsync(SignedInUser actor, Guid campaignId, object body)
    {
        var response = await actor.Client.PostAsJsonAsync(CharactersUrl(campaignId), body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var character = (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        Assert.Equal($"/api/v1/characters/{character.Id}", response.Headers.Location?.OriginalString);
        return character;
    }

    private static async Task<IReadOnlyList<CharacterSummaryDto>> ListAsync(SignedInUser actor, Guid campaignId)
    {
        var response = await actor.Client.GetAsync(CharactersUrl(campaignId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<CharacterSummaryDto>>())!;
    }

    private static async Task<CharacterDetailDto> GetDetailAsync(SignedInUser actor, Guid id)
    {
        var response = await actor.Client.GetAsync(CharacterUrl(id));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    private static async Task<CharacterDetailDto> PatchSheetAsync(SignedInUser actor, Guid id, object patch)
    {
        var response = await actor.Client.PatchAsJsonAsync(SheetUrl(id), patch);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    private static async Task<ChangeRequestDto> PatchSheetForApprovalAsync(SignedInUser actor, Guid id, object patch)
    {
        var response = await actor.Client.PatchAsJsonAsync(SheetUrl(id), patch);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChangeRequestDto>())!;
    }

    private static async Task<CharacterDetailDto> PatchCombatAsync(SignedInUser actor, Guid id, object body)
    {
        var response = await actor.Client.PatchAsJsonAsync($"{CharacterUrl(id)}/combat", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    private static async Task<CharacterDetailDto> PostAsync(SignedInUser actor, string url, object? body)
    {
        var response = body is null ? await actor.Client.PostAsync(url, null) : await actor.Client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    private static async Task<CharacterDetailDto> ActivateAsync(SignedInUser dm, Guid id) =>
        await PostAsync(dm, $"{CharacterUrl(id)}/activate", null);

    private static async Task<ChangeRequestDto> SubmitAsync(SignedInUser owner, Guid id)
    {
        var response = await owner.Client.PostAsync($"{CharacterUrl(id)}/submit", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChangeRequestDto>())!;
    }

    private static async Task<ChangeRequestDto> ApproveAsync(SignedInUser dm, Guid requestId)
    {
        var response = await dm.Client.PostAsync($"/api/v1/change-requests/{requestId}/approve", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChangeRequestDto>())!;
    }

    private static async Task<ChangeRequestDto> GetChangeRequestAsync(SignedInUser actor, Guid requestId)
    {
        var response = await actor.Client.GetAsync($"/api/v1/change-requests/{requestId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChangeRequestDto>())!;
    }

    private static async Task<IReadOnlyList<ChangeRequestDto>> ListChangeRequestsAsync(SignedInUser actor, Guid campaignId, string? status)
    {
        var url = $"/api/v1/campaigns/{campaignId}/change-requests" + (status is null ? string.Empty : $"?status={status}");
        var response = await actor.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<ChangeRequestDto>>())!;
    }
}
