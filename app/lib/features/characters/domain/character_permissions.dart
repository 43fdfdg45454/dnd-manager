import '../../../core/characters/models.dart';
import '../../campaigns/domain/campaign_models.dart';

/// What the signed-in user ([myUserId], [myRole] in the campaign) may do with a character.
class CharacterPermissions {
  const CharacterPermissions({
    required this.character,
    required this.myUserId,
    required this.myRole,
  });

  final CharacterDetail character;
  final String myUserId;

  /// Null while the campaign is still loading: treated as a plain player.
  final CampaignRole? myRole;

  bool get isDm => myRole?.isAtLeastDm ?? false;

  bool get isOwner => character.ownerUserId != null && character.ownerUserId == myUserId;

  bool get isDraft => character.status == CharacterStatus.draft;

  /// The DM edits directly in any state; the owner edits in Draft or sends a request.
  bool get canEdit => isDm || isOwner;

  /// Owner (not a DM, who can activate directly) of a Draft without a pending request.
  bool get canSubmit => isOwner && !isDm && isDraft && !character.hasPendingActivation;

  bool get canActivate => isDm && isDraft;

  bool get canDelete => isDm || (isOwner && isDraft);
}
