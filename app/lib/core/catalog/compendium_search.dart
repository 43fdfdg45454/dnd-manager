import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Search text shared by every tab of the compendium (already debounced by the UI).
class CompendiumSearch extends Notifier<String> {
  @override
  String build() => '';

  void set(String value) => state = value.trim();
}

final compendiumSearchProvider = NotifierProvider.autoDispose<CompendiumSearch, String>(
  CompendiumSearch.new,
);
