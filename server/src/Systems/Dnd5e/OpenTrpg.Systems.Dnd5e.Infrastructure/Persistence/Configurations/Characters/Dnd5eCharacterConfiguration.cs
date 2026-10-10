using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Files;
using OpenTrpg.Core.Domain.Items;
using OpenTrpg.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenTrpg.Core.Infrastructure;
using OpenTrpg.Core.Infrastructure.Persistence;
using OpenTrpg.Core.Infrastructure.Persistence.Configurations;
using OpenTrpg.Core.Infrastructure.Persistence.Configurations.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Persistence.Configurations.Characters;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Persistence.Configurations.Characters;

/// <summary>
/// The D&amp;D 5e part of a character, in its own table <c>Dnd5eCharacters</c> keyed by the core
/// <see cref="Character"/> (<c>CharacterId</c>, required 1:1, deleted with it), with the child tables of the sheet.
/// </summary>
internal sealed class Dnd5eCharacterConfiguration : IEntityTypeConfiguration<Dnd5eCharacter>
{
    public const string TableName = "Dnd5eCharacters";

    public void Configure(EntityTypeBuilder<Dnd5eCharacter> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("CharacterId").ValueGeneratedNever();
        builder.HasOne(x => x.Character).WithOne().HasForeignKey<Dnd5eCharacter>(x => x.Id).IsRequired().OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.RaceIndex).HasMaxLength(Dnd5eCharacter.IndexMaxLength);
        builder.Property(x => x.SubraceIndex).HasMaxLength(Dnd5eCharacter.IndexMaxLength);
        builder.Property(x => x.BackgroundIndex).HasMaxLength(Dnd5eCharacter.IndexMaxLength);
        builder.Property(x => x.Alignment).HasMaxLength(Dnd5eCharacter.AlignmentMaxLength);
        builder.Property(x => x.HpMode).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.SpellPreparationReason).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.ConditionsJson).IsRequired();
        builder.Property(x => x.ConcentratingOnSpellIndex).HasMaxLength(Dnd5eCharacter.IndexMaxLength);
        builder.Property(x => x.HitDiceUsedJson).IsRequired();
        builder.Property(x => x.BackgroundDetail).HasMaxLength(Dnd5eCharacter.BackgroundDetailMaxLength).IsRequired();

        // Shortcuts to the core character.
        builder.Ignore(x => x.CampaignId);
        builder.Ignore(x => x.OwnerUserId);
        builder.Ignore(x => x.Name);
        builder.Ignore(x => x.Status);
        builder.Ignore(x => x.Items);
        builder.Ignore(x => x.CreatedAt);

        // Calculated (read-only) views over the stored data.
        builder.Ignore(x => x.OrderedClasses);
        builder.Ignore(x => x.BaseAbilities);
        builder.Ignore(x => x.TotalLevel);
        builder.Ignore(x => x.Conditions);
        builder.Ignore(x => x.HitDiceUsed);
        builder.Ignore(x => x.RestRollsPending);
        builder.Ignore(x => x.OriginChoices);

        // Level-up granted by a DM (phase 16b); the granting user is kept like the resolver of a request.
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.LevelGrantedByUserId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.LevelGrantedByUserId);

        ConfigureChildren<CharacterClassLevel>(builder, nameof(Dnd5eCharacter.Classes), "_classes");
        ConfigureChildren<CharacterProficiency>(builder, nameof(Dnd5eCharacter.Proficiencies), "_proficiencies");
        ConfigureChildren<CharacterSpell>(builder, nameof(Dnd5eCharacter.Spells), "_spells");
        ConfigureChildren<SpellSlotState>(builder, nameof(Dnd5eCharacter.SpellSlots), "_spellSlots");
        ConfigureChildren<CharacterResource>(builder, nameof(Dnd5eCharacter.Resources), "_resources");
        ConfigureChildren<CharacterOverride>(builder, nameof(Dnd5eCharacter.Overrides), "_overrides");
        ConfigureChildren<CharacterChoice>(builder, nameof(Dnd5eCharacter.Choices), "_choices");
    }

    private static void ConfigureChildren<TChild>(EntityTypeBuilder<Dnd5eCharacter> builder, string navigationName, string fieldName)
        where TChild : class
    {
        builder.HasMany<TChild>(navigationName)
            .WithOne()
            .HasForeignKey("CharacterId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        var navigation = builder.Metadata.FindNavigation(navigationName)!;
        navigation.SetField(fieldName);
        navigation.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CharacterClassLevelConfiguration : IEntityTypeConfiguration<CharacterClassLevel>
{
    public void Configure(EntityTypeBuilder<CharacterClassLevel> builder)
    {
        builder.ToTable("CharacterClassLevels");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ClassIndex).HasMaxLength(Dnd5eCharacter.IndexMaxLength).IsRequired();
        builder.Property(x => x.SubclassIndex).HasMaxLength(Dnd5eCharacter.IndexMaxLength);
        builder.HasIndex(x => new { x.CharacterId, x.ClassIndex }).IsUnique();
    }
}

internal sealed class CharacterChoiceConfiguration : IEntityTypeConfiguration<CharacterChoice>
{
    public void Configure(EntityTypeBuilder<CharacterChoice> builder)
    {
        builder.ToTable("CharacterChoices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        // Null for origin choices (race, background; level 0).
        builder.Property(x => x.ClassIndex).HasMaxLength(Dnd5eCharacter.IndexMaxLength);
        builder.Property(x => x.Key).HasMaxLength(Dnd5eCharacter.IndexMaxLength).IsRequired();
        builder.Property(x => x.SelectedJson).IsRequired();
        builder.Ignore(x => x.IsOrigin);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Ignore(x => x.Selection);
        builder.HasIndex(x => new { x.CharacterId, x.ClassIndex, x.Key });
    }
}

internal sealed class CharacterProficiencyConfiguration : IEntityTypeConfiguration<CharacterProficiency>
{
    public void Configure(EntityTypeBuilder<CharacterProficiency> builder)
    {
        builder.ToTable("CharacterProficiencies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Key).HasMaxLength(Dnd5eCharacter.IndexMaxLength).IsRequired();
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(x => new { x.CharacterId, x.Type, x.Key }).IsUnique();
    }
}

internal sealed class CharacterSpellConfiguration : IEntityTypeConfiguration<CharacterSpell>
{
    public void Configure(EntityTypeBuilder<CharacterSpell> builder)
    {
        builder.ToTable("CharacterSpells");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.SpellIndex).HasMaxLength(Dnd5eCharacter.IndexMaxLength).IsRequired();
        builder.Property(x => x.ClassIndex).HasMaxLength(Dnd5eCharacter.IndexMaxLength).IsRequired();
        builder.HasIndex(x => new { x.CharacterId, x.SpellIndex, x.ClassIndex }).IsUnique();
    }
}

internal sealed class SpellSlotStateConfiguration : IEntityTypeConfiguration<SpellSlotState>
{
    public void Configure(EntityTypeBuilder<SpellSlotState> builder)
    {
        builder.ToTable("CharacterSpellSlots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.CharacterId, x.Level }).IsUnique();
    }
}

internal sealed class CharacterResourceConfiguration : IEntityTypeConfiguration<CharacterResource>
{
    public void Configure(EntityTypeBuilder<CharacterResource> builder)
    {
        builder.ToTable("CharacterResources");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Key).HasMaxLength(Dnd5eCharacter.IndexMaxLength);
        builder.Property(x => x.Name).HasMaxLength(CharacterResource.NameMaxLength).IsRequired();
        builder.Property(x => x.Recharge).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Ignore(x => x.Remaining);
        builder.Ignore(x => x.RollOnRest);
        builder.Ignore(x => x.Rolls);
        builder.Property(x => x.RollRest).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.RollsJson).IsRequired().HasDefaultValue("[]");
        builder.HasIndex(x => x.CharacterId);
    }
}

internal sealed class CharacterOverrideConfiguration : IEntityTypeConfiguration<CharacterOverride>
{
    public void Configure(EntityTypeBuilder<CharacterOverride> builder)
    {
        builder.ToTable("CharacterOverrides");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Field).HasMaxLength(Dnd5eCharacter.IndexMaxLength + 16).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(CharacterOverride.NoteMaxLength);
        builder.HasIndex(x => new { x.CharacterId, x.Field }).IsUnique();
    }
}

internal sealed class CharacterCompanionConfiguration : IEntityTypeConfiguration<CharacterCompanion>
{
    public void Configure(EntityTypeBuilder<CharacterCompanion> builder)
    {
        builder.ToTable("CharacterCompanions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.BeastIndex).HasMaxLength(CharacterCompanion.BeastIndexMaxLength).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(CharacterCompanion.NameMaxLength).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        // At most one companion per character.
        builder.HasOne<Dnd5eCharacter>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.CharacterId).IsUnique();
    }
}
