import '../../systems/domain/game_system.dart';

/// Role of a user inside one campaign, as sent by the API.
///
/// Declared from highest to lowest privilege so [index] can be compared.
enum CampaignRole {
  owner('Owner', 'Dueño'),
  dm('DM', 'DM'),
  player('Player', 'Jugador');

  const CampaignRole(this.apiValue, this.label);

  /// Value used on the wire.
  final String apiValue;

  /// Spanish label shown in the UI.
  final String label;

  static CampaignRole fromApi(String value) =>
      CampaignRole.values.firstWhere((r) => r.apiValue == value, orElse: () => CampaignRole.player);

  /// True when this role is [minimum] or higher (Owner > DM > Player).
  bool atLeast(CampaignRole minimum) => index <= minimum.index;

  /// Owner or DM.
  bool get isAtLeastDm => atLeast(CampaignRole.dm);

  bool get isOwner => this == CampaignRole.owner;
}

/// Result of `GET /users/search`.
class UserSummary {
  const UserSummary({required this.id, required this.displayName, required this.email});

  factory UserSummary.fromJson(Map<String, dynamic> json) => UserSummary(
    id: json['id'] as String,
    displayName: json['displayName'] as String,
    email: json['email'] as String,
  );

  final String id;
  final String displayName;
  final String email;
}

class Member {
  const Member({
    required this.userId,
    required this.displayName,
    required this.email,
    required this.role,
    required this.joinedAt,
  });

  factory Member.fromJson(Map<String, dynamic> json) => Member(
    userId: json['userId'] as String,
    displayName: json['displayName'] as String,
    email: json['email'] as String,
    role: CampaignRole.fromApi(json['role'] as String),
    joinedAt: DateTime.parse(json['joinedAt'] as String),
  );

  final String userId;
  final String displayName;
  final String email;
  final CampaignRole role;
  final DateTime joinedAt;

  Member copyWith({CampaignRole? role}) => Member(
    userId: userId,
    displayName: displayName,
    email: email,
    role: role ?? this.role,
    joinedAt: joinedAt,
  );
}

class CampaignSummary {
  const CampaignSummary({
    required this.id,
    required this.name,
    required this.description,
    required this.ownerId,
    required this.ownerDisplayName,
    required this.myRole,
    required this.memberCount,
    required this.createdAt,
    this.systemId = defaultGameSystemId,
  });

  factory CampaignSummary.fromJson(Map<String, dynamic> json) => CampaignSummary(
    id: json['id'] as String,
    name: json['name'] as String,
    description: json['description'] as String? ?? '',
    ownerId: json['ownerId'] as String,
    ownerDisplayName: json['ownerDisplayName'] as String,
    myRole: CampaignRole.fromApi(json['myRole'] as String),
    memberCount: json['memberCount'] as int,
    createdAt: DateTime.parse(json['createdAt'] as String),
    systemId: json['systemId'] as String? ?? defaultGameSystemId,
  );

  final String id;
  final String name;
  final String description;
  final String ownerId;
  final String ownerDisplayName;
  final CampaignRole myRole;
  final int memberCount;
  final DateTime createdAt;

  /// Game system of the campaign (`dnd5e` when the server does not send it).
  final String systemId;
}

/// Used when the server does not send the campaign's time zone.
const defaultCampaignTimeZone = 'UTC';

/// Server default: reminders 24 h and 2 h before a session.
const defaultReminderOffsets = <int>[1440, 120];

class CampaignDetail {
  const CampaignDetail({
    required this.id,
    required this.name,
    required this.description,
    required this.ownerId,
    required this.ownerDisplayName,
    required this.myRole,
    required this.members,
    required this.createdAt,
    required this.updatedAt,
    this.timeZoneId = defaultCampaignTimeZone,
    this.reminderOffsetsMinutes = defaultReminderOffsets,
    this.playersCanTakeFromStash = false,
    this.systemId = defaultGameSystemId,
  });

  factory CampaignDetail.fromJson(Map<String, dynamic> json) => CampaignDetail(
    id: json['id'] as String,
    name: json['name'] as String,
    description: json['description'] as String? ?? '',
    ownerId: json['ownerId'] as String,
    ownerDisplayName: json['ownerDisplayName'] as String,
    myRole: CampaignRole.fromApi(json['myRole'] as String),
    members: (json['members'] as List<dynamic>)
        .map((e) => Member.fromJson(e as Map<String, dynamic>))
        .toList(),
    createdAt: DateTime.parse(json['createdAt'] as String),
    updatedAt: DateTime.parse(json['updatedAt'] as String),
    timeZoneId: json['timeZoneId'] as String? ?? defaultCampaignTimeZone,
    reminderOffsetsMinutes: [
      for (final o in (json['reminderOffsetsMinutes'] as List<dynamic>? ?? defaultReminderOffsets))
        (o as num).toInt(),
    ],
    playersCanTakeFromStash: json['playersCanTakeFromStash'] as bool? ?? false,
    systemId: json['systemId'] as String? ?? defaultGameSystemId,
  );

  final String id;
  final String name;
  final String description;
  final String ownerId;
  final String ownerDisplayName;
  final CampaignRole myRole;
  final List<Member> members;
  final DateTime createdAt;
  final DateTime updatedAt;

  /// IANA identifier of the zone in which the sessions of the campaign are scheduled.
  final String timeZoneId;

  /// Minutes before a session at which reminder emails are sent.
  final List<int> reminderOffsetsMinutes;

  /// Whether players take items from the party stash (and give them back) by themselves.
  final bool playersCanTakeFromStash;

  /// Game system of the campaign (`dnd5e` when the server does not send it); it never changes.
  final String systemId;

  CampaignDetail copyWith({
    String? name,
    String? description,
    String? ownerId,
    String? ownerDisplayName,
    CampaignRole? myRole,
    List<Member>? members,
    String? timeZoneId,
    List<int>? reminderOffsetsMinutes,
    bool? playersCanTakeFromStash,
  }) => CampaignDetail(
    id: id,
    name: name ?? this.name,
    description: description ?? this.description,
    ownerId: ownerId ?? this.ownerId,
    ownerDisplayName: ownerDisplayName ?? this.ownerDisplayName,
    myRole: myRole ?? this.myRole,
    members: members ?? this.members,
    createdAt: createdAt,
    updatedAt: updatedAt,
    timeZoneId: timeZoneId ?? this.timeZoneId,
    reminderOffsetsMinutes: reminderOffsetsMinutes ?? this.reminderOffsetsMinutes,
    playersCanTakeFromStash: playersCanTakeFromStash ?? this.playersCanTakeFromStash,
    systemId: systemId,
  );
}

/// What the signed-in user (with role `myRole` and id `myUserId`) may do to a [Member].
abstract final class MemberPermissions {
  /// Only the Owner changes roles (DM <-> Player); the Owner row is fixed.
  static bool canChangeRole(CampaignRole myRole, Member member) =>
      myRole.isOwner && !member.role.isOwner;

  /// Owner removes anyone but themselves; a DM removes Players.
  static bool canRemove(CampaignRole myRole, String myUserId, Member member) {
    if (member.userId == myUserId || member.role.isOwner) return false;
    return switch (myRole) {
      CampaignRole.owner => true,
      CampaignRole.dm => member.role == CampaignRole.player,
      CampaignRole.player => false,
    };
  }
}

/// Pending invitation of a campaign, as its DMs see it.
class CampaignInvitation {
  const CampaignInvitation({
    required this.id,
    required this.userId,
    required this.displayName,
    required this.email,
    required this.role,
    required this.invitedByDisplayName,
    required this.createdAt,
  });

  factory CampaignInvitation.fromJson(Map<String, dynamic> json) => CampaignInvitation(
    id: json['id'] as String,
    userId: json['userId'] as String,
    displayName: json['displayName'] as String? ?? '',
    email: json['email'] as String? ?? '',
    role: CampaignRole.fromApi(json['role'] as String),
    invitedByDisplayName: json['invitedByDisplayName'] as String? ?? '',
    createdAt: DateTime.parse(json['createdAt'] as String),
  );

  final String id;
  final String userId;
  final String displayName;
  final String email;
  final CampaignRole role;
  final String invitedByDisplayName;
  final DateTime createdAt;
}

/// Pending invitation of the signed-in user to a campaign.
class MyInvitation {
  const MyInvitation({
    required this.id,
    required this.campaignId,
    required this.campaignName,
    required this.role,
    required this.invitedByDisplayName,
    required this.createdAt,
  });

  factory MyInvitation.fromJson(Map<String, dynamic> json) => MyInvitation(
    id: json['id'] as String,
    campaignId: json['campaignId'] as String,
    campaignName: json['campaignName'] as String? ?? '',
    role: CampaignRole.fromApi(json['role'] as String),
    invitedByDisplayName: json['invitedByDisplayName'] as String? ?? '',
    createdAt: DateTime.parse(json['createdAt'] as String),
  );

  final String id;
  final String campaignId;
  final String campaignName;
  final CampaignRole role;
  final String invitedByDisplayName;
  final DateTime createdAt;
}
