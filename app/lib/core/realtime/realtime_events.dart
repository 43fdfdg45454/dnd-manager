import 'package:flutter/foundation.dart';

typedef _EventFactory = CampaignEvent Function({
  required String campaignId,
  String? characterId,
  String? entityId,
  DateTime? at,
});

/// Realtime event of a campaign (SignalR client method `campaignEvent`). It
/// only carries ids: the app fetches again over HTTP whatever changed.
@immutable
sealed class CampaignEvent {
  const CampaignEvent({required this.campaignId, this.characterId, this.entityId, this.at});

  /// Parses `{ type, campaignId, characterId, entityId, at }` (keys matched
  /// ignoring case). A `type` the core does not know gives an
  /// [UnknownCampaignEvent].
  factory CampaignEvent.fromJson(Map<Object?, Object?> json) {
    final fields = {for (final e in json.entries) '${e.key}'.toLowerCase(): e.value};
    String? text(String key) => switch (fields[key.toLowerCase()]) {
      final String value when value.isNotEmpty => value,
      _ => null,
    };
    final type = text('type') ?? '';
    final campaignId = text('campaignId') ?? '';
    final characterId = text('characterId');
    final entityId = text('entityId');
    final rawAt = text('at');
    final at = rawAt == null ? null : DateTime.tryParse(rawAt);
    final make = _byType[type];
    if (make == null) {
      return UnknownCampaignEvent(
        rawType: type,
        campaignId: campaignId,
        characterId: characterId,
        entityId: entityId,
        at: at,
        data: Map.unmodifiable(json),
      );
    }
    return make(campaignId: campaignId, characterId: characterId, entityId: entityId, at: at);
  }

  static const Map<String, _EventFactory> _byType = {
    MessageReceived.type: MessageReceived.new,
    CharacterUpdated.type: CharacterUpdated.new,
    PartyStashUpdated.type: PartyStashUpdated.new,
    ShopUpdated.type: ShopUpdated.new,
    ChangeRequestUpdated.type: ChangeRequestUpdated.new,
    ChangeRequestResolved.type: ChangeRequestResolved.new,
    InvitationReceived.type: InvitationReceived.new,
    MembersUpdated.type: MembersUpdated.new,
    SessionUpdated.type: SessionUpdated.new,
    RestRequestUpdated.type: RestRequestUpdated.new,
    MembershipRemoved.type: MembershipRemoved.new,
  };

  final String campaignId;

  /// Character concerned, when there is one.
  final String? characterId;

  /// Other entity concerned (message, shop, change request, session...).
  final String? entityId;

  /// When it happened on the server (null if missing or unreadable).
  final DateTime? at;

  /// Whether the event belongs to [id] (ids compared ignoring case).
  bool isFor(String id) => campaignId.toLowerCase() == id.toLowerCase();
}

/// A secret message for the user (sent to that user only); [entityId] is the message.
final class MessageReceived extends CampaignEvent {
  const MessageReceived({required super.campaignId, super.characterId, super.entityId, super.at});

  static const type = 'message.received';
}

/// Combat state, rest, sheet, inventory or an approved request of a character changed.
final class CharacterUpdated extends CampaignEvent {
  const CharacterUpdated({required super.campaignId, super.characterId, super.entityId, super.at});

  static const type = 'character.updated';
}

/// The common gold or the items of the party stash changed.
final class PartyStashUpdated extends CampaignEvent {
  const PartyStashUpdated({required super.campaignId, super.characterId, super.entityId, super.at});

  static const type = 'party.stash.updated';
}

/// A shop ([entityId]) changed: stock, prices or opened/closed.
final class ShopUpdated extends CampaignEvent {
  const ShopUpdated({required super.campaignId, super.characterId, super.entityId, super.at});

  static const type = 'shop.updated';
}

/// A change request ([entityId]) was created, approved or rejected.
final class ChangeRequestUpdated extends CampaignEvent {
  const ChangeRequestUpdated({
    required super.campaignId,
    super.characterId,
    super.entityId,
    super.at,
  });

  static const type = 'changeRequest.updated';
}

/// A DM approved or rejected a request ([entityId]) of the user (sent to the
/// requester only).
final class ChangeRequestResolved extends CampaignEvent {
  const ChangeRequestResolved({
    required super.campaignId,
    super.characterId,
    super.entityId,
    super.at,
  });

  static const type = 'changeRequest.resolved';
}

/// The user was invited to the campaign ([entityId] is the invitation; sent to
/// that user only).
final class InvitationReceived extends CampaignEvent {
  const InvitationReceived({
    required super.campaignId,
    super.characterId,
    super.entityId,
    super.at,
  });

  static const type = 'invitation.received';
}

/// Someone accepted or declined an invitation, or one was cancelled.
final class MembersUpdated extends CampaignEvent {
  const MembersUpdated({required super.campaignId, super.characterId, super.entityId, super.at});

  static const type = 'members.updated';
}

/// A game session ([entityId]) was created, edited, answered or removed.
final class SessionUpdated extends CampaignEvent {
  const SessionUpdated({required super.campaignId, super.characterId, super.entityId, super.at});

  static const type = 'session.updated';
}

/// A rest request ([entityId]) of a character was created, approved, rejected
/// or cancelled.
final class RestRequestUpdated extends CampaignEvent {
  const RestRequestUpdated({
    required super.campaignId,
    super.characterId,
    super.entityId,
    super.at,
  });

  static const type = 'restRequest.updated';
}

/// The user was removed from the campaign or left it (sent to that user only).
final class MembershipRemoved extends CampaignEvent {
  const MembershipRemoved({required super.campaignId, super.characterId, super.entityId, super.at});

  static const type = 'membership.removed';
}

/// An event type the core does not know: the events of a game system (D&D
/// 5e: `party.rest`, `levelUp.granted`) or of a newer server. The realtime
/// link hands it to the game system of the campaign
/// (`GameSystemUi.onRealtimeEvent`), which may rebuild its own event from it;
/// a system event can also extend this class directly. Ignored when nobody
/// knows it.
class UnknownCampaignEvent extends CampaignEvent {
  const UnknownCampaignEvent({
    required this.rawType,
    required super.campaignId,
    super.characterId,
    super.entityId,
    super.at,
    this.data = const {},
  });

  /// The `type` received.
  final String rawType;

  /// The whole event as received (keys as sent).
  final Map<Object?, Object?> data;
}
