using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using Xunit.Abstractions;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Own database: the private third-party pack walkthrough (one import, one SRD baseline taken before it).</summary>
public sealed class PrivateValdasPackApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>
/// Validates the operator's private third-party pack <c>content-packs/valdas-spire.json</c> (repository root, never
/// committed: see <c>docs/content-packs.md</c>) when it exists on this machine. Without the file the test passes without
/// checking anything.
/// <para>
/// The walkthrough (phase 35C): the pack imports without errors and is enabled in a campaign; every new class (not an
/// extension of another class, not an auxiliary level) and every new race and subrace gets a character created as the
/// creation wizard does (first valid option of every decision) and raised from level 1 to 20 through the level-up wizard
/// (first valid option of every choice: subclass, feats, spells, option sets, nested choices), resolving the forced steps
/// (spell preparation, companions) after each level; the sheet must answer 200 and have no pending or invalid choices.
/// Every auxiliary level is taken once by a caster that meets its prerequisites. Finally an SRD character of a campaign
/// that does not enable the pack must produce the same sheet as before the import.
/// </para>
/// The classes and races come from the pack itself: this file names none of them (the pack's content is private).
/// </summary>
public class PrivateValdasPackTests(PrivateValdasPackApiFactory factory, ITestOutputHelper output) : IClassFixture<PrivateValdasPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";

    /// <summary>What the walkthrough notices without failing (choices left without options because they are already had...).</summary>
    private readonly List<string> _notes = [];
    private const string CatalogUrl = "/api/v1/systems/dnd5e/catalog";
    private const string PackId = "valdas-spire";

    private static readonly string[] Abilities = ["str", "dex", "con", "int", "wis", "cha"];

    private static readonly object BaseScores = new { str = 14, dex = 14, con = 14, @int = 14, wis = 14, cha = 14 };

    /// <summary>Volatile fields left out when comparing the sheets of two different characters.</summary>
    private static readonly HashSet<string> Volatile = ["id", "campaignId", "characterId", "createdAt", "updatedAt", "ownerUserId", "ownerDisplayName", "at"];

    [Fact]
    public async Task The_private_valdas_pack_creates_and_levels_every_new_class_and_race_to_20()
    {
        if (FindPack() is not { } path)
        {
            return;
        }

        var clock = Stopwatch.StartNew();
        var json = await File.ReadAllTextAsync(path);
        var pack = JsonNode.Parse(json)!.AsObject();
        var classes = pack["classes"]!.AsArray().Select(c => c!.AsObject()).Where(c => c["extends"] is null).ToList();
        var mainClasses = classes.Where(c => c["auxiliary"]?.GetValue<bool>() != true).Select(c => c["index"]!.GetValue<string>()).ToList();
        var auxiliary = classes.Where(c => c["auxiliary"]?.GetValue<bool>() == true).Select(c => c["index"]!.GetValue<string>()).ToList();
        var origins = pack["races"]!.AsArray()
            .Select(r => r!.AsObject())
            .SelectMany(r => (r["subraces"]?.AsArray() is { Count: > 0 } subraces
                ? subraces.Select(s => (Race: r["index"]!.GetValue<string>(), Subrace: (string?)s!["index"]!.GetValue<string>()))
                : [(Race: r["index"]!.GetValue<string>(), Subrace: (string?)null)]))
            .ToList();
        Assert.Equal(10, mainClasses.Count);
        Assert.Equal(13, auxiliary.Count);
        Assert.Equal(5, origins.Select(o => o.Race).Distinct().Count());

        // The SRD baseline, before the pack exists in this instance.
        var baseline = await factory.CreateCampaignScenarioAsync();
        var srdHero = await SrdFighterAsync(baseline);
        var beforeSame = await RawDetailAsync(baseline.Player, srdHero);
        var before = Normalized(beforeSame);

        var admin = await factory.CreateAdminClientAsync();
        var response = await admin.PostAsync(PacksUrl, new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var result = (await response.Content.ReadFromJsonAsync<ContentPackImportResultDto>())!;
        Assert.Equal((PackId, 3, "dnd5e"), (result.Id, result.FormatVersion, result.SystemId));
        output.WriteLine($"Importado en {clock.Elapsed.TotalSeconds:F1} s");

        // The pack does not change SRD characters of campaigns that do not enable it: the same character, and a new one.
        Assert.Equal(beforeSame.ToJsonString(), (await RawDetailAsync(baseline.Player, srdHero)).ToJsonString());
        var other = await factory.CreateCampaignScenarioAsync();
        Assert.Equal(before, Normalized(await RawDetailAsync(other.Player, await SrdFighterAsync(other))));

        var s = await factory.CreateCampaignScenarioAsync();
        await s.EnablePacksAsync(PackId);
        var problems = new List<string>();
        var walked = 0;

        foreach (var classIndex in mainClasses)
        {
            var started = clock.Elapsed;
            var (classProblems, id) = await WalkAsync(s, $"clase {classIndex}", "human", null, classIndex, []);
            problems.AddRange(classProblems);
            if (id is { } walkedId)
            {
                // Level 1 choices of the first class (fighting style, expertise...) are answered with the origin choices.
                var answered = (await DetailAsync(s.Player, walkedId)).System().Choices.Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
                var levelOne = classes.Single(c => c["index"]!.GetValue<string>() == classIndex)["levelChoices"]?.AsArray()
                    .Select(c => c!.AsObject())
                    .Where(c => c["level"]!.GetValue<int>() == 1 && c["kind"]!.GetValue<string>() != "Subclass")
                    .Select(c => c["key"]!.GetValue<string>())
                    .Where(k => !answered.Contains(k))
                    .ToList() ?? [];
                if (levelOne.Count > 0)
                {
                    problems.Add($"clase {classIndex}: elecciones de nivel 1 sin responder en la creación: {string.Join(", ", levelOne)}.");
                }
            }

            walked++;
            output.WriteLine($"{classIndex}: {(clock.Elapsed - started).TotalSeconds:F1} s");
        }

        foreach (var (race, subrace) in origins)
        {
            var started = clock.Elapsed;
            problems.AddRange((await WalkAsync(s, $"raza {subrace ?? race}", race, subrace, "fighter", [])).Problems);
            walked++;
            output.WriteLine($"{subrace ?? race}: {(clock.Elapsed - started).TotalSeconds:F1} s");
        }

        // Auxiliary levels: an SRD bard (a caster, as some of them require) takes each one once from level 2 on.
        problems.AddRange((await WalkAsync(s, "niveles auxiliares", "human", null, "bard", auxiliary)).Problems);
        walked++;

        output.WriteLine($"{walked} personajes en {clock.Elapsed.TotalSeconds:F1} s");
        foreach (var line in _notes.Select(n => $"Nota: {n}").Concat(problems))
        {
            output.WriteLine(line);
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    // ---- Walkthrough --------------------------------------------------------------------------------------------

    /// <summary>
    /// Creates a character of <paramref name="race"/>/<paramref name="subrace"/> and <paramref name="classIndex"/> as the
    /// creation wizard does and raises it to level 20, taking each of <paramref name="auxiliary"/> once as soon as it is
    /// allowed. Returns the problems found (an exception stops the walk of this character only).
    /// </summary>
    private async Task<(List<string> Problems, Guid? Id)> WalkAsync(CampaignScenario s, string label, string race, string? subrace, string classIndex, IReadOnlyList<string> auxiliary)
    {
        var problems = new List<string>();
        var level = 1;
        Guid? walked = null;
        try
        {
            var id = await CreateAsync(s, race, subrace, classIndex, problems, label);
            walked = id;
            await CheckAsync(s, id, 1, problems, $"{label}, nivel 1");

            var pendingAuxiliary = auxiliary.ToList();
            for (level = 2; level <= 20; level++)
            {
                await GrantAsync(s, id);
                var plan = await PlanAsync(s.Player, id, null);
                string? target = null;
                if (pendingAuxiliary.Count > 0)
                {
                    target = pendingAuxiliary.FirstOrDefault(a => plan.Classes.Any(c => c.ClassIndex == a && c.Allowed));
                    var mainLeft = 20 - level + 1 - pendingAuxiliary.Count;
                    if (target is null && mainLeft <= 0)
                    {
                        // Only auxiliary levels would be left: report the ones that are never allowed.
                        foreach (var refused in pendingAuxiliary)
                        {
                            var option = plan.Classes.FirstOrDefault(c => c.ClassIndex == refused);
                            problems.Add($"{label}: el nivel auxiliar {refused} no se ofrece ({option?.Reason ?? "no está en la lista"}).");
                        }

                        pendingAuxiliary.Clear();
                    }

                    if (target is not null)
                    {
                        pendingAuxiliary.Remove(target);
                        plan = await PlanAsync(s.Player, id, target);
                    }
                }

                var detail = await DetailAsync(s.Player, id);
                var answers = Answers(plan, detail, problems, _notes, $"{label}, nivel {level} ({plan.ClassIndex} {plan.ClassLevel})");
                var applied = await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/level-up", new
                {
                    classIndex = target,
                    hitPointsRolled = 1,
                    choices = answers,
                });
                if (applied.StatusCode != HttpStatusCode.OK)
                {
                    var asked = string.Join("; ", plan.Choices.Select(c => $"{c.Key} {c.Kind} {c.Required}/{c.Choose}{(c.SubclassIndex is null ? "" : " @" + c.SubclassIndex)}{(c.Requires is null ? "" : $" <- {c.Requires.ChoiceKey}={c.Requires.Index}")}"));
                    problems.Add($"{label}, nivel {level} ({plan.ClassIndex} {plan.ClassLevel}): {(int)applied.StatusCode} {await applied.Content.ReadAsStringAsync()} | plan: {asked} | respuestas: {JsonSerializer.Serialize(answers)}");
                    return (problems, walked);
                }

                await ResolveForcedStepsAsync(s, id, problems, $"{label}, nivel {level}");
                await CheckAsync(s, id, level, problems, $"{label}, nivel {level}");
            }

            if (pendingAuxiliary.Count > 0)
            {
                problems.Add($"{label}: niveles auxiliares sin tomar: {string.Join(", ", pendingAuxiliary)}.");
            }

            var final = (await DetailAsync(s.Player, id)).System();
            _notes.Add($"{label}: {string.Join(" + ", final.Classes.Select(c => $"{c.ClassIndex} {c.Level}{(c.SubclassIndex is null ? "" : $" ({c.SubclassIndex})")}"))}, {final.Choices.Count} elecciones, {final.Feats.Count} dotes, {final.Spells.Count} conjuros.");
        }
        catch (Exception exception)
        {
            problems.Add($"{label}, nivel {level}: {exception.GetType().Name}: {exception.Message}");
        }

        return (problems, walked);
    }

    /// <summary>
    /// The creation wizard: draft, full sheet (race, subrace, first background, base scores, class saving throws, the
    /// first class skills, fixed languages, the first cantrips and spells of the class at level 1), origin choices with
    /// their first valid options, activation by the DM and its forced steps.
    /// </summary>
    private async Task<Guid> CreateAsync(CampaignScenario s, string race, string? subrace, string classIndex, List<string> problems, string label)
    {
        var detail = await GetAsync<ClassDetailDto>(s.Player.Client, $"{CatalogUrl}/classes/{classIndex}");
        var raceDetail = await GetAsync<RaceDetailDto>(s.Player.Client, $"{CatalogUrl}/races/{race}");
        var background = (await GetAsync<List<BackgroundDto>>(s.Player.Client, $"{CatalogUrl}/backgrounds")).First();
        var subraceDetail = subrace is null ? null : raceDetail.Subraces.Single(x => x.Index == subrace);

        var backgroundSkills = background.SkillProficiencies.Select(SkillIndex).ToHashSet(StringComparer.Ordinal);
        var classSkills = detail.SkillChoices.From.Select(SkillIndex).Where(k => !backgroundSkills.Contains(k)).Take(detail.SkillChoices.Choose).ToList();
        var proficiencies = detail.SavingThrows.Select(a => new { type = "SavingThrow", key = a.Trim().ToLowerInvariant()[..3], expertise = false, source = "Class" })
            .Concat(classSkills.Select(k => new { type = "Skill", key = k, expertise = false, source = "Class" }))
            .Concat(backgroundSkills.Select(k => new { type = "Skill", key = k, expertise = false, source = "Background" }))
            .Concat(raceDetail.Languages.Select(l => new { type = "Language", key = l, expertise = false, source = "Race" }))
            .ToList();

        var first = detail.Levels.FirstOrDefault(l => l.Level == 1);
        var spells = new List<object>();
        if (detail.IsSpellcaster && first is not null)
        {
            var cantrips = first.CantripsKnown ?? 0;
            var known = first.SpellsKnown ?? (detail.Spellcasting is null ? PreparedAtLevelOne(classIndex) : 0);
            if (cantrips > 0)
            {
                spells.AddRange((await SpellsAsync(s, classIndex, 0)).Take(cantrips).Select(i => new { spellIndex = i, classIndex, isPrepared = true }));
            }

            if (known > 0)
            {
                spells.AddRange((await SpellsAsync(s, classIndex, 1)).Take(known).Select(i => new { spellIndex = i, classIndex, isPrepared = true }));
            }
        }

        var subclass = detail.SubclassLevel == 1 ? detail.Subclasses.FirstOrDefault()?.Index : null;
        var character = await s.Player.CreateCharacterAsync(s.CampaignId, "Recorrido");
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(character.Id)}/sheet", new
        {
            raceIndex = race,
            subraceIndex = subrace,
            backgroundIndex = background.Index,
            applyRacialBonuses = true,
            baseAbilities = BaseScores,
            classes = new[] { new { classIndex, subclassIndex = subclass, level = 1 } },
            proficiencies,
            spells,
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, $"{label}: hoja {(int)patch.StatusCode} {await patch.Content.ReadAsStringAsync()}");

        var origin = await GetAsync<OriginChoicesDto>(s.Player.Client, OriginUrl(character.Id));
        var originAnswers = OriginAnswers(origin, await DetailAsync(s.Player, character.Id), problems, _notes, $"{label}, origen");
        if (originAnswers.Count > 0)
        {
            var saved = await s.Player.Client.PutAsJsonAsync(OriginUrl(character.Id), new { choices = originAnswers });
            Assert.True(saved.StatusCode == HttpStatusCode.OK, $"{label}: origen {(int)saved.StatusCode} {await saved.Content.ReadAsStringAsync()}");
            var complete = (await saved.Content.ReadFromJsonAsync<OriginChoicesDto>())!;
            if (!complete.Complete)
            {
                problems.Add($"{label}: las elecciones de origen siguen incompletas: {string.Join(", ", complete.Choices.Where(c => c.Required > c.Selected.Count).Select(c => c.Key))}.");
            }
        }

        var activated = await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/activate", null);
        Assert.True(activated.StatusCode == HttpStatusCode.OK, $"{label}: activar {(int)activated.StatusCode} {await activated.Content.ReadAsStringAsync()}");
        await ResolveForcedStepsAsync(s, character.Id, problems, $"{label}, creación");
        return character.Id;
    }

    /// <summary>Spells prepared at level 1 by the SRD preparing classes (the wizard's spellbook holds six).</summary>
    private static int PreparedAtLevelOne(string classIndex) => classIndex switch
    {
        "wizard" => 6,
        "cleric" or "druid" => 3,
        _ => 0,
    };

    private static async Task<List<string>> SpellsAsync(CampaignScenario s, string classIndex, int level) =>
        (await GetAsync<PagedResult<SpellSummaryDto>>(s.Player.Client, $"{CatalogUrl}/spells?class={classIndex}&level={level}&pageSize=200"))
            .Items.Select(x => x.Index).ToList();

    /// <summary>Spell preparation (keep it, or prepare the first candidates) and pending companions (the first creature allowed).</summary>
    private async Task ResolveForcedStepsAsync(CampaignScenario s, Guid id, List<string> problems, string where)
    {
        var detail = await DetailAsync(s.Player, id);
        if (detail.SpellPreparationPending)
        {
            var preparation = await GetAsync<SpellPreparationDto>(s.Player.Client, $"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/spell-preparation");
            HttpResponseMessage prepared;
            if (preparation.CanKeep)
            {
                prepared = await s.Player.Client.PostAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/spell-preparation/keep", null);
            }
            else
            {
                var request = preparation.Classes.Select(c => new
                {
                    classIndex = c.ClassIndex,
                    spells = c.Prepared.Where(p => c.Candidates.Any(x => x.Index == p))
                        .Concat(c.Candidates.Select(x => x.Index).Where(i => !c.Prepared.Contains(i)))
                        .Take(c.Max)
                        .ToArray(),
                }).ToArray();
                prepared = await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/spell-preparation", new { classes = request });
            }

            if (prepared.StatusCode != HttpStatusCode.OK)
            {
                problems.Add($"{where}: preparar conjuros {(int)prepared.StatusCode} {await prepared.Content.ReadAsStringAsync()}");
            }

            detail = await DetailAsync(s.Player, id);
        }

        var system = detail.System();
        if (system.CompanionPending && system.CompanionFeature is { } feature)
        {
            var cr = Math.Min(feature.ChallengeBudget ?? feature.MaxChallengeRating, feature.MaxChallengeRating);
            var creature = feature.Creatures.FirstOrDefault(c => c.ChallengeRating <= cr)?.Index;
            if (creature is null && feature.CreatureIndexes.Count == 0)
            {
                var beasts = await GetAsync<List<BeastSummaryDto>>(s.Player.Client, $"{CatalogUrl}/beasts?type=beast&maxCr={cr.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                creature = beasts.FirstOrDefault(b => b.ChallengeRating <= cr && (feature.Sizes.Count == 0 || feature.Sizes.Contains(b.Size, StringComparer.OrdinalIgnoreCase)))?.Index;
            }

            if (creature is null)
            {
                problems.Add($"{where}: el rasgo de compañero {feature.FeatureIndex} no tiene ninguna criatura elegible.");
                return;
            }

            var chosen = await s.Player.Client.PutAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/companion", new { beastIndex = creature, name = "Compañero" });
            if (chosen.StatusCode != HttpStatusCode.OK)
            {
                problems.Add($"{where}: elegir compañero {creature} {(int)chosen.StatusCode} {await chosen.Content.ReadAsStringAsync()}");
            }
        }
    }

    /// <summary>The sheet answers 200 at the expected level, with nothing pending and nothing invalid.</summary>
    private async Task CheckAsync(CampaignScenario s, Guid id, int level, List<string> problems, string where)
    {
        var detail = await DetailAsync(s.Player, id);
        var system = detail.System();
        if (system.Sheet.TotalLevel != level)
        {
            problems.Add($"{where}: nivel total {system.Sheet.TotalLevel}.");
        }

        foreach (var invalid in system.InvalidChoices)
        {
            problems.Add($"{where}: elección inválida {invalid.Key}/{invalid.Item.Index}: {invalid.Reason}");
        }

        var pending = new List<string>();
        if (system.PendingLevelUpTo is not null)
        {
            pending.Add("subida de nivel");
        }

        if (system.SpellPreparationPending)
        {
            pending.Add("preparación de conjuros");
        }

        if (system.CompanionPending)
        {
            pending.Add("compañero");
        }

        if (system.RestRollsPending)
        {
            pending.Add("tiradas de descanso");
        }

        if (system.CatalogMissing)
        {
            pending.Add("contenido que falta en el catálogo");
        }

        if (pending.Count > 0)
        {
            problems.Add($"{where}: pendiente {string.Join(", ", pending)}.");
        }

        var invalidChoices = await GetAsync<InvalidChoicesDto>(s.Player.Client, $"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/invalid-choices");
        if (invalidChoices.Invalid.Count > 0 && system.InvalidChoices.Count == 0)
        {
            problems.Add($"{where}: /invalid-choices lista {invalidChoices.Invalid.Count} elecciones que la hoja no muestra.");
        }
    }

    // ---- Answers ------------------------------------------------------------------------------------------------

    /// <summary>
    /// The first valid answer of every choice of the plan: the first eligible subclass and options (respecting the
    /// options that need another pick of the same level-up), for an ability improvement the first eligible option (a
    /// feat, with the first ability it may raise) or else +1 to the two lowest abilities, free texts numbered, and the
    /// choices of a picked option or feat. A required choice with fewer eligible options than it grants is a problem.
    /// </summary>
    private static List<object> Answers(LevelUpPlanDto plan, CharacterDetailDto hero, List<string> problems, List<string> notes, string where)
    {
        var scores = hero.System().Sheet.Abilities.ToDictionary(a => a.Key, a => a.Value.Score);
        var chosenSubclass = plan.Choices.FirstOrDefault(c => c.Kind == "Subclass")?.Options.FirstOrDefault(o => o.Eligible)?.Index
            ?? hero.System().Classes.FirstOrDefault(c => c.ClassIndex == plan.ClassIndex)?.SubclassIndex;
        var picked = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var taken = new HashSet<string>(StringComparer.Ordinal); // An option (or feat) is picked once per level-up.
        var answers = new List<object>();
        var pending = plan.Choices.Where(c => c.SubclassIndex is null || c.SubclassIndex == chosenSubclass).ToList();

        // Choices of an option are answered once the option is picked: loop until nothing new is answered.
        bool progress;
        do
        {
            progress = false;
            foreach (var choice in pending.ToList())
            {
                if (choice.Requires is { } requires && !(picked.TryGetValue(requires.ChoiceKey, out var parent) && parent.Contains(requires.Index)))
                {
                    continue;
                }

                pending.Remove(choice);
                progress = true;
                if (choice.Required <= 0 && choice.Choose <= 0)
                {
                    continue;
                }

                // A skill (or expertise) the character already has leaves nothing to pick: not a defect.
                if (choice.Warning is not null || choice.Required < choice.Choose && !choice.Replaces)
                {
                    (choice.Kind is "Skill" or "Expertise" ? notes : problems).Add($"{where}: {choice.Key} ({choice.Kind}) pide {choice.Choose} y solo admite {choice.Required}: {choice.Warning}");
                }

                if (choice.Required <= 0)
                {
                    continue;
                }

                if (choice.Kind == "AsiOrFeat")
                {
                    var feat = choice.Options.FirstOrDefault(o => o.Eligible && o.Requires is null && !taken.Contains(o.Index));
                    if (feat is null)
                    {
                        var lowest = scores.Where(a => a.Value < 20).OrderBy(a => a.Value).ThenBy(a => a.Key).Take(2).Select(a => a.Key).ToList();
                        answers.Add(new { key = choice.Key, selected = new { asi = lowest.ToDictionary(a => a, _ => 1) } });
                        picked[choice.Key] = [];
                        continue;
                    }

                    var ability = feat.AbilityIncrease is { } increase
                        ? (increase.From.Count > 0 ? increase.From : Abilities).Select(a => a.ToLowerInvariant()).FirstOrDefault(a => scores.GetValueOrDefault(a) < 20)
                            ?? (increase.From.Count > 0 ? increase.From[0] : "str")
                        : null;
                    var removed = feat.RemoveSavingThrows is { } remove && remove.From.Count != remove.Choose
                        ? (remove.From.Count > 0 ? remove.From : hero.System().Proficiencies.Where(p => p.Type == "SavingThrow").Select(p => p.Key)).Take(remove.Choose).ToArray()
                        : null;
                    answers.Add(new { key = choice.Key, selected = FeatAnswer(feat.Index, ability, removed) });
                    picked[choice.Key] = [feat.Index];
                    taken.Add(feat.Index);
                    continue;
                }

                if (choice.FreeText)
                {
                    var texts = Enumerable.Range(1, choice.Required).Select(n => $"Texto libre {n}").ToArray();
                    answers.Add(new { key = choice.Key, selected = texts });
                    picked[choice.Key] = [.. texts];
                    continue;
                }

                var picks = new List<string>();
                foreach (var option in choice.Options.Where(o => o.Eligible && (choice.SetId is null || !taken.Contains(o.Index))))
                {
                    if (picks.Count == choice.Required)
                    {
                        break;
                    }

                    if (option.Requires is { } needs && !(picked.TryGetValue(needs.ChoiceKey, out var others) && others.Contains(needs.Index)))
                    {
                        continue;
                    }

                    picks.Add(option.Index);
                }

                if (picks.Count < choice.Required)
                {
                    problems.Add($"{where}: {choice.Key} ({choice.Kind}) requiere {choice.Required} y solo hay {picks.Count} opciones elegibles.");
                }

                answers.Add(new { key = choice.Key, selected = picks.ToArray() });
                picked[choice.Key] = picks;
                if (choice.SetId is not null)
                {
                    taken.UnionWith(picks);
                }
            }
        }
        while (progress && pending.Count > 0);

        return answers;
    }

    /// <summary>
    /// The first valid answer of every origin choice (required or not), as the wizard's origin and language steps send them,
    /// including the level-1 choices of the first class ("class." keys): the choices of an option once it is picked, the
    /// options that need another pick of the same request, and an option of a set taken once.
    /// </summary>
    private static List<object> OriginAnswers(OriginChoicesDto plan, CharacterDetailDto hero, List<string> problems, List<string> notes, string where)
    {
        var scores = hero.System().Sheet.Abilities.ToDictionary(a => a.Key, a => a.Value.Score);
        var answers = new List<object>();
        var picked = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var taken = new HashSet<string>(StringComparer.Ordinal);
        bool Met(OptionRequirementDto? requires) => requires is null || (picked.TryGetValue(requires.ChoiceKey, out var parent) && parent.Contains(requires.Index));
        var pending = plan.Choices.Where(c => c.Choose > 0).ToList();
        bool progress;
        do
        {
            progress = false;
            foreach (var choice in pending.ToList())
            {
                if (!Met(choice.Requires))
                {
                    continue;
                }

                pending.Remove(choice);
                progress = true;
                if (choice.Source == OriginChoicesPlanner.ClassSource && choice.Required < choice.Choose)
                {
                    (choice.Kind is "Skill" or "Expertise" ? notes : problems).Add($"{where}: {choice.Key} ({choice.Kind}) pide {choice.Choose} y solo admite {choice.Required}: {choice.Warning}");
                }

                if (choice.Kind is "Feat" or "AsiOrFeat")
                {
                    var feat = choice.Options.FirstOrDefault(o => o.Eligible && Met(o.Requires) && !taken.Contains(o.Index));
                    if (feat is null)
                    {
                        if (choice.Kind == "AsiOrFeat" && choice.Required > 0)
                        {
                            var lowest = scores.Where(a => a.Value < 20).OrderBy(a => a.Value).ThenBy(a => a.Key).Take(2).Select(a => a.Key).ToList();
                            answers.Add(new { key = choice.Key, selected = new { asi = lowest.ToDictionary(a => a, _ => 1) } });
                        }
                        else if (choice.Required > 0)
                        {
                            problems.Add($"{where}: {choice.Key} no tiene ninguna dote elegible.");
                        }

                        picked[choice.Key] = [];
                        continue;
                    }

                    var ability = feat.AbilityIncrease is { } increase
                        ? (increase.From.Count > 0 ? increase.From : Abilities).Select(a => a.ToLowerInvariant()).FirstOrDefault(a => scores.GetValueOrDefault(a) < 20)
                        : null;
                    var removed = feat.RemoveSavingThrows is { } remove && remove.From.Count != remove.Choose
                        ? (remove.From.Count > 0 ? remove.From : hero.System().Proficiencies.Where(p => p.Type == "SavingThrow").Select(p => p.Key)).Take(remove.Choose).ToArray()
                        : null;
                    answers.Add(new { key = choice.Key, selected = FeatAnswer(feat.Index, ability, removed) });
                    picked[choice.Key] = [feat.Index];
                    taken.Add(feat.Index);
                    continue;
                }

                if (choice.FreeText)
                {
                    var texts = Enumerable.Range(1, choice.Choose).Select(n => $"Texto libre {n}").ToArray();
                    answers.Add(new { key = choice.Key, selected = texts });
                    picked[choice.Key] = [.. texts];
                    continue;
                }

                var count = choice.Source == OriginChoicesPlanner.ClassSource ? choice.Required : choice.Choose;
                var picks = choice.Options
                    .Where(o => o.Eligible && Met(o.Requires) && (choice.SetId is null || !taken.Contains(o.Index)))
                    .Take(count)
                    .Select(o => o.Index)
                    .ToList();
                if (picks.Count < choice.Required)
                {
                    problems.Add($"{where}: {choice.Key} ({choice.Kind}) requiere {choice.Required} y solo hay {picks.Count} opciones elegibles.");
                }

                answers.Add(new { key = choice.Key, selected = picks.ToArray() });
                picked[choice.Key] = picks;
                if (choice.SetId is not null)
                {
                    taken.UnionWith(picks);
                }
            }
        }
        while (progress && pending.Count > 0);

        return answers;
    }

    /// <summary>A feat answer with only the fields that apply (the server refuses unknown or empty ones).</summary>
    private static Dictionary<string, object> FeatAnswer(string feat, string? ability, string[]? removed)
    {
        var answer = new Dictionary<string, object> { ["feat"] = feat };
        if (ability is not null)
        {
            answer["ability"] = ability;
        }

        if (removed is not null)
        {
            answer["removedSavingThrows"] = removed;
        }

        return answer;
    }

    // ---- SRD baseline -------------------------------------------------------------------------------------------

    /// <summary>A level 5 human champion fighter with the first background, through the full sheet.</summary>
    private static async Task<Guid> SrdFighterAsync(CampaignScenario s)
    {
        var character = await s.Player.CreateCharacterAsync(s.CampaignId, "Guerrera");
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(character.Id)}/sheet", new
        {
            raceIndex = "human",
            backgroundIndex = "acolyte",
            applyRacialBonuses = true,
            baseAbilities = new { str = 15, dex = 14, con = 13, @int = 12, wis = 10, cha = 8 },
            classes = new[] { new { classIndex = "fighter", subclassIndex = "champion", level = 5 } },
            proficiencies = new[]
            {
                new { type = "SavingThrow", key = "str", expertise = false, source = "Class" },
                new { type = "SavingThrow", key = "con", expertise = false, source = "Class" },
                new { type = "Skill", key = "athletics", expertise = false, source = "Class" },
                new { type = "Skill", key = "perception", expertise = false, source = "Class" },
            },
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.ActivateAsync(character.Id)).StatusCode);
        return character.Id;
    }

    private static async Task<JsonObject> RawDetailAsync(SignedInUser actor, Guid id)
    {
        var response = await actor.Client.GetAsync(ItemTestHelpers.CharacterUrl(id));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
    }

    /// <summary>The detail without the fields that differ between two characters built the same way.</summary>
    private static string Normalized(JsonObject detail)
    {
        var copy = detail.DeepClone();
        Strip(copy);
        return copy.ToJsonString();
    }

    private static void Strip(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (Volatile.Contains(key))
                    {
                        obj.Remove(key);
                    }
                    else
                    {
                        Strip(obj[key]);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Strip(item);
                }

                break;
        }
    }

    // ---- Helpers ------------------------------------------------------------------------------------------------

    private static string SkillIndex(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.StartsWith("Skill:", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed["Skill:".Length..].Trim();
        }

        return string.Join('-', trimmed.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string OriginUrl(Guid id) => $"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/origin-choices";

    private static async Task<CharacterDetailDto> DetailAsync(SignedInUser actor, Guid id) => await actor.GetCharacterAsync(id);

    private static async Task GrantAsync(CampaignScenario s, Guid characterId)
    {
        var granted = await s.Dm.Client.PostAsJsonAsync($"/api/v1/systems/dnd5e/campaigns/{s.CampaignId}/party/grant-level", new { characterIds = new[] { characterId } });
        Assert.True(granted.StatusCode == HttpStatusCode.OK, $"Conceder nivel: {(int)granted.StatusCode} {await granted.Content.ReadAsStringAsync()}");
    }

    private static async Task<LevelUpPlanDto> PlanAsync(SignedInUser actor, Guid id, string? classIndex)
    {
        var query = classIndex is null ? string.Empty : $"?classIndex={classIndex}";
        return await GetAsync<LevelUpPlanDto>(actor.Client, $"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/level-up{query}");
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(JsonSerializerOptions.Web))!;
    }

    /// <summary>Walks up from the test binaries to the repository root and returns the pack path when the file exists.</summary>
    private static string? FindPack()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "content-packs", $"{PackId}.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
