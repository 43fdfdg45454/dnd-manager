/// The D&D 5e tabs of a character page: [CharacterTab.combat] is the "Combate" view
/// and the rest are the sub-tabs of "Detalle", in their order on the bar.
enum CharacterTab {
  combat,
  summary,
  skills,
  traits,
  spells,
  inventory,
  notes;

  /// The sub-tabs of "Detalle" (every tab but [combat]).
  static const detailTabs = [summary, skills, traits, spells, inventory, notes];
}
