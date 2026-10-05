import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Counter bumped whenever everything cached for the previous server must be
/// discarded (server switch). Long-lived data providers watch it in `build` so
/// they reload from the new server.
class AppSessionEpoch extends Notifier<int> {
  @override
  int build() => 0;

  void bump() => state++;
}

final appSessionEpochProvider = NotifierProvider<AppSessionEpoch, int>(AppSessionEpoch.new);
