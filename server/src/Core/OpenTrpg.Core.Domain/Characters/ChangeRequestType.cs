namespace OpenTrpg.Core.Domain.Characters;

public enum ChangeRequestType
{
    Activate,
    EditSheet,
    AddItem,
    RemoveItem,
    CustomItem,
    AdjustMoney,
    Other,

    /// <summary>A player changes the beast of the animal companion (payload <c>{ beastIndex, name }</c>).</summary>
    Companion,
}
