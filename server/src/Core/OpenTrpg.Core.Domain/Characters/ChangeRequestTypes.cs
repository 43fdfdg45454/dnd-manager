namespace OpenTrpg.Core.Domain.Characters;

/// <summary>
/// Values of <see cref="ChangeRequest.Type"/> handled by the core. A game system registers its own types
/// (for example a sheet edit) and applies them; every type is stored as text as it always was.
/// </summary>
public static class ChangeRequestTypes
{
    /// <summary>Longest type a request can store.</summary>
    public const int MaxLength = 16;

    public const string Activate = "Activate";
    public const string AddItem = "AddItem";
    public const string RemoveItem = "RemoveItem";
    public const string CustomItem = "CustomItem";
    public const string AdjustMoney = "AdjustMoney";
    public const string Other = "Other";

    /// <summary>The types the core applies itself.</summary>
    public static IReadOnlyList<string> Core { get; } = [Activate, AddItem, RemoveItem, CustomItem, AdjustMoney, Other];
}
