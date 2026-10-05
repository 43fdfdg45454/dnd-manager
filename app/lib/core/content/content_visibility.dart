/// Who can see a lore entry, map or pin.
enum ContentVisibility {
  players('Players', 'Jugadores'),
  dmOnly('DmOnly', 'Solo DM');

  const ContentVisibility(this.apiValue, this.label);

  final String apiValue;

  /// Spanish label shown in the UI.
  final String label;

  static ContentVisibility fromApi(Object? value) => ContentVisibility.values.firstWhere(
    (v) => v.apiValue.toLowerCase() == value.toString().toLowerCase(),
    orElse: () => ContentVisibility.players,
  );

  bool get isDmOnly => this == ContentVisibility.dmOnly;
}
