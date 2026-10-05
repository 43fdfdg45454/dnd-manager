import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_error.dart';
import '../data/campaigns_repository.dart';
import '../domain/campaign_models.dart';

typedef NewMemberData = ({UserSummary user, CampaignRole role});

/// Searches active users by email or name and lets the caller pick one with a
/// role. Pops a [NewMemberData], or null if cancelled.
///
/// The `DM` role is only offered when [canAddDm] (the current user is Owner).
class AddMemberDialog extends ConsumerStatefulWidget {
  const AddMemberDialog({super.key, required this.canAddDm, this.excludedUserIds = const {}});

  final bool canAddDm;

  /// Users that are already members and must not be offered again.
  final Set<String> excludedUserIds;

  @override
  ConsumerState<AddMemberDialog> createState() => _AddMemberDialogState();
}

class _AddMemberDialogState extends ConsumerState<AddMemberDialog> {
  static const _minQueryLength = 2;

  final _controller = TextEditingController();
  Timer? _debounce;
  int _requestId = 0;
  CampaignRole _role = CampaignRole.player;
  List<UserSummary> _results = const [];
  bool _loading = false;
  String? _error;

  @override
  void dispose() {
    _debounce?.cancel();
    _controller.dispose();
    super.dispose();
  }

  void _onChanged(String value) {
    _debounce?.cancel();
    final query = value.trim();
    if (query.length < _minQueryLength) {
      // Invalidates any in-flight request so a late answer cannot repopulate the list.
      _requestId++;
      setState(() {
        _results = const [];
        _loading = false;
        _error = null;
      });
      return;
    }
    _debounce = Timer(const Duration(milliseconds: 400), () => _search(query));
  }

  Future<void> _search(String query) async {
    final requestId = ++_requestId;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final found = await ref.read(campaignsRepositoryProvider).searchUsers(query);
      if (!mounted || requestId != _requestId) return;
      setState(() {
        _results = found.where((u) => !widget.excludedUserIds.contains(u.id)).toList();
        _loading = false;
      });
    } catch (error) {
      if (!mounted || requestId != _requestId) return;
      setState(() {
        _results = const [];
        _loading = false;
        _error = describeApiError(error);
      });
    }
  }

  Widget _resultsView() {
    final query = _controller.text.trim();
    if (_error != null) {
      return Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error));
    }
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (query.length < _minQueryLength) {
      return const Text('Escribe al menos 2 caracteres para buscar.');
    }
    if (_results.isEmpty && _debounce?.isActive != true) {
      return const Text('No se encontraron usuarios.');
    }
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        for (final user in _results)
          ListTile(
            key: Key('search-result-${user.id}'),
            contentPadding: EdgeInsets.zero,
            leading: CircleAvatar(
              child: Text(user.displayName.isEmpty ? '?' : user.displayName[0].toUpperCase()),
            ),
            title: Text(user.displayName),
            subtitle: Text(user.email),
            onTap: () => Navigator.of(context).pop<NewMemberData>((user: user, role: _role)),
          ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    final roles = [if (widget.canAddDm) CampaignRole.dm, CampaignRole.player];
    return AlertDialog(
      title: const Text('Añadir miembro'),
      content: SizedBox(
        width: double.maxFinite,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              TextField(
                key: const Key('member-search'),
                controller: _controller,
                autofocus: true,
                onChanged: _onChanged,
                textInputAction: TextInputAction.search,
                decoration: const InputDecoration(
                  hintText: 'Buscar por correo o nombre',
                  prefixIcon: Icon(Icons.search),
                  border: OutlineInputBorder(),
                ),
              ),
              const SizedBox(height: 12),
              if (roles.length > 1) ...[
                SegmentedButton<CampaignRole>(
                  key: const Key('member-role'),
                  showSelectedIcon: false,
                  segments: [for (final r in roles) ButtonSegment(value: r, label: Text(r.label))],
                  selected: {_role},
                  onSelectionChanged: (selection) => setState(() => _role = selection.first),
                ),
                const SizedBox(height: 12),
              ] else
                Text('Se añadirá como ${CampaignRole.player.label}.'),
              _resultsView(),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancelar')),
      ],
    );
  }
}
