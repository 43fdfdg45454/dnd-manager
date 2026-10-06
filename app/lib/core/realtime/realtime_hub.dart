import 'realtime_events.dart';

/// State of the realtime connection shown in the campaign app bar.
enum RealtimeStatus {
  /// No network (or no session): the app does not even try.
  offline,

  /// Not connected: never started, stopped, or the last attempt failed (it
  /// is retried after a while).
  disconnected,

  /// First connection in progress.
  connecting,

  /// Connected and receiving the events of the campaign.
  connected,

  /// The connection dropped and is being restored automatically.
  reconnecting,
}

/// Connection to the campaign hub of the server (`/hubs/campaign`). One per
/// app; it follows one campaign at a time. Implemented over SignalR by
/// `SignalRRealtimeHub` and by a fake in the tests.
abstract interface class RealtimeHub {
  /// Events of the campaign joined (and the user's own, such as secret messages).
  Stream<CampaignEvent> get events;

  /// Emits every change of [status].
  Stream<RealtimeStatus> get statusChanges;

  RealtimeStatus get status;

  /// The campaign the hub was last asked to follow ([connect]), until
  /// [disconnect]. Set synchronously, before the connection completes.
  String? get campaignId;

  /// Opens the connection (if needed) and joins [campaignId], leaving the
  /// previous campaign. Throws when the server cannot be reached or rejects
  /// the user.
  Future<void> connect(String campaignId);

  /// Opens a throwaway connection to the hub and answers with the transport it
  /// negotiated (`WebSockets`, `ServerSentEvents` or `LongPolling`), trying them
  /// in that order. Throws when none of them connects. Used by the connection
  /// diagnostics; it does not touch the campaign connection.
  Future<String> probe();

  /// Closes the connection.
  Future<void> disconnect();

  /// Closes the connection and the streams (the hub cannot be used again).
  Future<void> dispose();
}
