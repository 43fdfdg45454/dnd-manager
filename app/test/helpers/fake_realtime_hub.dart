import 'dart:async';

import 'package:dnd_companion/core/realtime/realtime_events.dart';
import 'package:dnd_companion/core/realtime/realtime_hub.dart';
import 'package:dnd_companion/core/realtime/realtime_provider.dart';
import 'package:flutter_riverpod/misc.dart' show Override;

/// In-memory [RealtimeHub]: records the calls and lets the test [emit] events
/// and status changes. [failConnect] makes `connect` throw.
class FakeRealtimeHub implements RealtimeHub {
  FakeRealtimeHub({this.failConnect = false});

  bool failConnect;

  /// Campaign ids passed to [connect], in order.
  final List<String> connects = [];
  int disconnects = 0;

  final _events = StreamController<CampaignEvent>.broadcast();
  final _statuses = StreamController<RealtimeStatus>.broadcast();
  RealtimeStatus _status = RealtimeStatus.disconnected;
  String? _campaignId;

  @override
  Stream<CampaignEvent> get events => _events.stream;

  @override
  Stream<RealtimeStatus> get statusChanges => _statuses.stream;

  @override
  RealtimeStatus get status => _status;

  @override
  String? get campaignId => _campaignId;

  @override
  Future<void> connect(String campaignId) async {
    connects.add(campaignId);
    _campaignId = campaignId;
    if (failConnect) {
      setStatus(RealtimeStatus.disconnected);
      throw StateError('Fake connection failure');
    }
    setStatus(RealtimeStatus.connected);
  }

  @override
  Future<void> disconnect() async {
    disconnects++;
    _campaignId = null;
    setStatus(RealtimeStatus.disconnected);
  }

  @override
  Future<void> dispose() async {
    await _events.close();
    await _statuses.close();
  }

  /// Simulates a status change of the connection (e.g. a drop).
  void setStatus(RealtimeStatus status) {
    if (status == _status) return;
    _status = status;
    _statuses.add(status);
  }

  /// Delivers [event] as if the server had sent it.
  void emit(CampaignEvent event) => _events.add(event);

  /// Delivers the raw `campaignEvent` payload [json].
  void emitJson(Map<String, Object?> json) => emit(CampaignEvent.fromJson(json));
}

/// Override of [realtimeHubProvider] with [hub] (a fresh fake by default).
Override fakeRealtimeOverride([FakeRealtimeHub? hub]) =>
    realtimeHubProvider.overrideWithValue(hub ?? FakeRealtimeHub());
