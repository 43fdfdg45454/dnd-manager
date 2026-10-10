/// Id of the game system of campaigns that do not say otherwise (and of every
/// campaign cached before the server sent `systemId`).
const defaultGameSystemId = 'dnd5e';

/// A game system registered in the server (`GET /api/v1/systems`).
class GameSystem {
  const GameSystem({
    required this.id,
    required this.name,
    this.version = '',
    this.isDefault = false,
  });

  factory GameSystem.fromJson(Map<String, dynamic> json) => GameSystem(
    id: json['id'] as String,
    name: json['name'] as String? ?? json['id'] as String,
    version: json['version'] as String? ?? '',
    isDefault: json['isDefault'] as bool? ?? false,
  );

  final String id;
  final String name;
  final String version;
  final bool isDefault;
}

/// What the app shows while the list of systems is not known (loading, or
/// offline without a cached copy): D&D 5e, the only system of older servers.
const fallbackGameSystems = [
  GameSystem(id: defaultGameSystemId, name: 'Dungeons & Dragons 5e', isDefault: true),
];

/// Display name of the system [id] among [systems] (or [fallbackGameSystems]);
/// the id itself when it is unknown.
String gameSystemName(List<GameSystem>? systems, String id) {
  for (final system in [...?systems, ...fallbackGameSystems]) {
    if (system.id == id) return system.name;
  }
  return id;
}
