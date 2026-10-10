import '../../core/realtime/realtime_events.dart';

/// The DM forced a rest on the party (the kind of rest is not sent).
final class PartyRest extends UnknownCampaignEvent {
  const PartyRest({required super.campaignId, super.characterId, super.entityId, super.at})
    : super(rawType: type);

  static const type = 'party.rest';
}

/// A DM granted the next level to [characterId] (sent to the campaign and to
/// the owner of the character).
final class LevelUpGranted extends UnknownCampaignEvent {
  const LevelUpGranted({required super.campaignId, super.characterId, super.entityId, super.at})
    : super(rawType: type);

  static const type = 'levelUp.granted';
}

/// The D&D 5e event behind [event] ([PartyRest], [LevelUpGranted]), or
/// [event] itself when it is not one of them.
CampaignEvent dnd5eEventOf(CampaignEvent event) => switch (event) {
  PartyRest() || LevelUpGranted() => event,
  UnknownCampaignEvent(rawType: PartyRest.type) => PartyRest(
    campaignId: event.campaignId,
    characterId: event.characterId,
    entityId: event.entityId,
    at: event.at,
  ),
  UnknownCampaignEvent(rawType: LevelUpGranted.type) => LevelUpGranted(
    campaignId: event.campaignId,
    characterId: event.characterId,
    entityId: event.entityId,
    at: event.at,
  ),
  _ => event,
};
