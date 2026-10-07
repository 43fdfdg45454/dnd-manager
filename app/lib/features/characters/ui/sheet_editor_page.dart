import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/api_error.dart';
import '../../../core/ui/offline_widgets.dart';
import '../../../core/ui/spell_category.dart';
import '../../catalog/data/catalog_controllers.dart';
import '../../catalog/data/catalog_repository.dart';
import '../../catalog/data/models.dart' show ClassSummary, titleFromIndex;
import '../../catalog/domain/catalog_format.dart';
import '../../catalog/ui/detail_widgets.dart' show SectionTitle;
import '../../campaigns/data/campaigns_controller.dart';
import '../data/characters_controller.dart';
import '../data/models.dart';
import '../domain/character_format.dart';
import 'character_tabs.dart' show titleFromSpellIndex;
import 'point_buy_dialog.dart';
import 'spell_picker_page.dart';

/// Semi-automatic sheet editor. Only the fields the user changed are sent, so a
/// change request shows exactly what the player wants to change.
class SheetEditorPage extends ConsumerWidget {
  const SheetEditorPage({super.key, required this.characterId});

  final String characterId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(characterControllerProvider(characterId));
    return detail.when(
      skipLoadingOnReload: true,
      loading: () => Scaffold(
        appBar: AppBar(title: const Text('Editar hoja')),
        body: const Center(child: CircularProgressIndicator()),
      ),
      error: (error, _) => Scaffold(
        appBar: AppBar(title: const Text('Editar hoja')),
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(describeCharacterError(error), textAlign: TextAlign.center),
                const SizedBox(height: 12),
                FilledButton.icon(
                  onPressed: () => ref.invalidate(characterControllerProvider(characterId)),
                  icon: const Icon(Icons.refresh),
                  label: const Text('Reintentar'),
                ),
              ],
            ),
          ),
        ),
      ),
      data: (character) => SheetEditorForm(character: character),
    );
  }
}

class _ClassRow {
  _ClassRow({this.classIndex, this.subclassIndex, this.level = 1});

  String? classIndex;
  String? subclassIndex;
  int level;
}

class _OverrideRow {
  _OverrideRow({this.field, int? value, String? note})
    : valueController = TextEditingController(text: value?.toString() ?? ''),
      noteController = TextEditingController(text: note ?? '');

  String? field;
  final TextEditingController valueController;
  final TextEditingController noteController;

  void dispose() {
    valueController.dispose();
    noteController.dispose();
  }
}

/// The editor form for [character]. Exposed so it can be tested without the
/// loading wrapper.
class SheetEditorForm extends ConsumerStatefulWidget {
  const SheetEditorForm({super.key, required this.character});

  final CharacterDetail character;

  @override
  ConsumerState<SheetEditorForm> createState() => _SheetEditorFormState();
}

class _SheetEditorFormState extends ConsumerState<SheetEditorForm> {
  final _formKey = GlobalKey<FormState>();
  late final CharacterDetail _initial = widget.character;

  late final _nameController = TextEditingController(text: _initial.name);
  late final _notesController = TextEditingController(text: _initial.notes);
  late final _backstoryController = TextEditingController(text: _initial.backstory);
  late final _traitsController = TextEditingController(text: _initial.personalityTraits);
  late final _idealsController = TextEditingController(text: _initial.ideals);
  late final _bondsController = TextEditingController(text: _initial.bonds);
  late final _flawsController = TextEditingController(text: _initial.flaws);
  late final _backgroundDetailController = TextEditingController(text: _initial.backgroundDetail);
  late final _goldController = TextEditingController(text: copperToGoldText(_initial.copperPieces));
  late final Map<String, TextEditingController> _baseControllers = {
    for (final k in abilityKeys)
      k: TextEditingController(text: '${_initial.baseAbilities[k] ?? 10}'),
  };

  late String? _raceIndex = _initial.raceIndex;
  late String? _subraceIndex = _initial.subraceIndex;
  late String? _backgroundIndex = _initial.backgroundIndex;
  late String? _alignment = _initial.alignment;
  late bool _applyRacial = _initial.applyRacialBonuses;
  late HpMode _hpMode = _initial.hpMode;

  late final List<_ClassRow> _classes = [
    for (final c in _initial.classes)
      _ClassRow(classIndex: c.classIndex, subclassIndex: c.subclassIndex, level: c.level),
  ];

  late final Set<String> _skillProf = {
    for (final p in _initial.proficiencies)
      if (p.type == ProficiencyType.skill) p.key,
  };
  late final Set<String> _skillExpertise = {
    for (final p in _initial.proficiencies)
      if (p.type == ProficiencyType.skill && p.expertise) p.key,
  };
  late final Set<String> _saveProf = {
    for (final p in _initial.proficiencies)
      if (p.type == ProficiencyType.savingThrow) p.key,
  };

  /// Armor, weapon, tool and language proficiencies are kept as they are.
  late final List<CharacterProficiency> _otherProficiencies = [
    for (final p in _initial.proficiencies)
      if (p.type != ProficiencyType.skill && p.type != ProficiencyType.savingThrow) p,
  ];

  late final List<CharacterSpell> _spells = [..._initial.spells];

  late final List<_OverrideRow> _overrides = [
    for (final o in _initial.overrides) _OverrideRow(field: o.field, value: o.value, note: o.note),
  ];

  bool _saving = false;

  @override
  void dispose() {
    _nameController.dispose();
    _notesController.dispose();
    _backstoryController.dispose();
    _traitsController.dispose();
    _idealsController.dispose();
    _bondsController.dispose();
    _flawsController.dispose();
    _backgroundDetailController.dispose();
    _goldController.dispose();
    for (final c in _baseControllers.values) {
      c.dispose();
    }
    for (final o in _overrides) {
      o.dispose();
    }
    super.dispose();
  }

  // -- Derived values -------------------------------------------------------

  int get _totalLevel => _classes.fold(0, (sum, c) => sum + c.level);

  /// Skills of the sheet (18); falls back to the SRD list for a fresh character.
  List<({String index, String label, String ability})> get _skills {
    final fromSheet = _initial.sheet.skills;
    if (fromSheet.isNotEmpty) {
      return [
        for (final s in fromSheet)
          (index: s.index, label: skillLabel(s.index, s.name), ability: s.ability),
      ];
    }
    return [for (final e in skillLabels.entries) (index: e.key, label: e.value, ability: '')];
  }

  // -- Actions --------------------------------------------------------------

  Future<void> _pointBuy() async {
    final result = await showDialog<Map<String, int>>(
      context: context,
      builder: (_) => const PointBuyDialog(),
    );
    if (result == null) return;
    setState(() {
      for (final k in abilityKeys) {
        _baseControllers[k]!.text = '${result[k]}';
      }
    });
  }

  void _onClassChosen(int row, String classIndex) {
    setState(() {
      _classes[row]
        ..classIndex = classIndex
        ..subclassIndex = null;
    });
    if (row == 0) _applyMainClassSaves(classIndex);
  }

  /// Marks the saving throws of the main class (still editable afterwards).
  Future<void> _applyMainClassSaves(String classIndex) async {
    try {
      final detail = await ref.read(catalogRepositoryProvider).classDetail(classIndex);
      if (!mounted || _classes.isEmpty || _classes.first.classIndex != classIndex) return;
      setState(() {
        _saveProf
          ..clear()
          ..addAll(detail.savingThrows.map(abilityKeyOf).where(abilityKeys.contains));
      });
    } catch (_) {
      // Saving throws stay as they were; the user can mark them by hand.
    }
  }

  void _addClass(List<ClassSummary> catalog) {
    final used = {for (final c in _classes) c.classIndex};
    final free = catalog.where((c) => !used.contains(c.index)).firstOrNull;
    final row = _ClassRow(classIndex: free?.index);
    setState(() => _classes.add(row));
    if (_classes.length == 1 && free != null) _applyMainClassSaves(free.index);
  }

  Future<void> _addSpells(List<ClassSummary> catalog) async {
    final casters = <SpellPickerClass>[
      for (final row in _classes)
        if (row.classIndex != null &&
            (catalog.where((c) => c.index == row.classIndex).firstOrNull?.isSpellcaster ?? false))
          (
            classIndex: row.classIndex!,
            className: catalog.firstWhere((c) => c.index == row.classIndex).name,
            level: row.level,
          ),
    ];
    if (casters.isEmpty) {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          const SnackBar(content: Text('Añade primero una clase que lance conjuros.')),
        );
      return;
    }
    final picked = await Navigator.of(context).push<List<CharacterSpell>>(
      MaterialPageRoute(
        builder: (_) =>
            SpellPickerPage(classes: casters, chosen: {for (final s in _spells) s.spellIndex}),
      ),
    );
    if (picked == null || picked.isEmpty) return;
    setState(() => _spells.addAll(picked));
  }

  void _addOverride() {
    final used = {for (final o in _overrides) o.field};
    final free = overrideFields.where((f) => !used.contains(f)).firstOrNull;
    if (free == null) return;
    setState(() => _overrides.add(_OverrideRow(field: free)));
  }

  void _showMessage(String message) {
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(message)));
  }

  String? _structuralError() {
    final indexes = _classes.map((c) => c.classIndex).toList();
    if (indexes.contains(null)) return 'Elige una clase en cada fila o quita la fila vacía.';
    if (indexes.toSet().length != indexes.length) return 'No repitas la misma clase.';
    if (_totalLevel > 20) return 'El nivel total no puede superar 20.';
    final fields = _overrides.map((o) => o.field).toList();
    if (fields.toSet().length != fields.length) return 'No repitas el mismo valor modificado.';
    if (_hpMode == HpMode.manual && !fields.contains('hitPointsMax')) {
      return 'El modo manual de puntos de golpe requiere un valor modificado de '
          '"Puntos de golpe máximos".';
    }
    return null;
  }

  // -- Patch ------------------------------------------------------------------

  List<CharacterProficiency> _currentProficiencies() {
    final sources = {
      for (final p in _initial.proficiencies) '${p.type.apiValue}:${p.key}': p.source,
    };
    return [
      for (final s in _skills)
        if (_skillProf.contains(s.index))
          CharacterProficiency(
            type: ProficiencyType.skill,
            key: s.index,
            expertise: _skillExpertise.contains(s.index),
            source: sources['Skill:${s.index}'] ?? ProficiencySource.manual,
          ),
      for (final k in abilityKeys)
        if (_saveProf.contains(k))
          CharacterProficiency(
            type: ProficiencyType.savingThrow,
            key: k,
            source: sources['SavingThrow:$k'] ?? ProficiencySource.manual,
          ),
      ..._otherProficiencies,
    ];
  }

  /// Order-independent signature of a proficiency list.
  static Set<String> _proficiencySignature(Iterable<CharacterProficiency> list) => {
    for (final p in list) '${p.type.apiValue}:${p.key}:${p.expertise}',
  };

  SheetPatch _buildPatch() {
    final c = _initial;
    final clear = <String>{};

    String? nullable(String key, String? current, String? initial) {
      if (current == initial) return null;
      if (current == null) clear.add(key);
      return current;
    }

    final name = _nameController.text.trim();
    final raceIndex = nullable('raceIndex', _raceIndex, c.raceIndex);
    final subraceIndex = nullable('subraceIndex', _subraceIndex, c.subraceIndex);
    final backgroundIndex = nullable('backgroundIndex', _backgroundIndex, c.backgroundIndex);
    final alignment = nullable('alignment', _alignment, c.alignment);

    final base = {for (final k in abilityKeys) k: int.parse(_baseControllers[k]!.text.trim())};
    final baseChanged = abilityKeys.any((k) => base[k] != c.baseAbilities[k]);

    final classes = [
      for (final r in _classes)
        SheetPatchClass(classIndex: r.classIndex!, subclassIndex: r.subclassIndex, level: r.level),
    ];
    final classesChanged =
        classes.length != c.classes.length ||
        [for (var i = 0; i < classes.length; i++) i].any(
          (i) =>
              classes[i].classIndex != c.classes[i].classIndex ||
              classes[i].subclassIndex != c.classes[i].subclassIndex ||
              classes[i].level != c.classes[i].level,
        );

    final proficiencies = _currentProficiencies();
    final proficienciesChanged = !_sameSet(
      _proficiencySignature(proficiencies),
      _proficiencySignature(c.proficiencies),
    );

    String spellSig(CharacterSpell s) =>
        '${s.spellIndex}:${s.classIndex}:${s.isPrepared}:${s.alwaysPrepared}';
    final spellsChanged =
        !_sameSet(_spells.map(spellSig).toSet(), c.spells.map(spellSig).toSet()) ||
        _spells.length != c.spells.length;

    final overrides = [
      for (final o in _overrides)
        CharacterOverride(
          field: o.field!,
          value: int.parse(o.valueController.text.trim()),
          note: o.noteController.text.trim().isEmpty ? null : o.noteController.text.trim(),
        ),
    ];
    String overrideSig(CharacterOverride o) => '${o.field}:${o.value}:${o.note ?? ''}';
    final overridesChanged =
        !_sameSet(overrides.map(overrideSig).toSet(), c.overrides.map(overrideSig).toSet()) ||
        overrides.length != c.overrides.length;

    final copper = goldTextToCopper(_goldController.text)!;

    return SheetPatch(
      name: name == c.name ? null : name,
      raceIndex: raceIndex,
      subraceIndex: subraceIndex,
      backgroundIndex: backgroundIndex,
      alignment: alignment,
      applyRacialBonuses: _applyRacial == c.applyRacialBonuses ? null : _applyRacial,
      hpMode: _hpMode == c.hpMode ? null : _hpMode,
      baseAbilities: baseChanged ? base : null,
      classes: classesChanged ? classes : null,
      proficiencies: proficienciesChanged ? proficiencies : null,
      spells: spellsChanged ? _spells : null,
      overrides: overridesChanged ? overrides : null,
      notes: _notesController.text == c.notes ? null : _notesController.text,
      backstory: _backstoryController.text == c.backstory ? null : _backstoryController.text,
      personalityTraits: _changed(_traitsController, c.personalityTraits),
      ideals: _changed(_idealsController, c.ideals),
      bonds: _changed(_bondsController, c.bonds),
      flaws: _changed(_flawsController, c.flaws),
      backgroundDetail: _changed(_backgroundDetailController, c.backgroundDetail),
      copperPieces: copper == c.copperPieces ? null : copper,
      clear: clear,
    );
  }

  /// Trimmed text of [controller], or null when it did not change.
  static String? _changed(TextEditingController controller, String initial) {
    final text = controller.text.trim();
    return text == initial.trim() ? null : text;
  }

  static bool _sameSet(Set<String> a, Set<String> b) => a.length == b.length && a.containsAll(b);

  Future<void> _save() async {
    if (_saving) return;
    if (!_formKey.currentState!.validate()) return;
    final problem = _structuralError();
    if (problem != null) {
      _showMessage(problem);
      return;
    }
    final patch = _buildPatch();
    if (patch.isEmpty) {
      _showMessage('No hay cambios que guardar.');
      context.pop();
      return;
    }
    final messenger = ScaffoldMessenger.of(context);
    final router = GoRouter.of(context);
    setState(() => _saving = true);
    try {
      final result = await ref
          .read(characterControllerProvider(_initial.id).notifier)
          .saveSheet(patch);
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            content: Text(switch (result) {
              Saved() => 'Hoja guardada.',
              PendingApproval() => 'Enviado al DM para aprobación',
            }),
          ),
        );
      if (mounted) router.pop();
    } catch (error) {
      messenger
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(describeCharacterError(error))));
      if (mounted) setState(() => _saving = false);
    }
  }

  // -- Build ------------------------------------------------------------------

  @override
  Widget build(BuildContext context) {
    final catalogClasses = ref.watch(classesProvider).value ?? const <ClassSummary>[];
    final isActive = _initial.status == CharacterStatus.active;
    // Changes go through the DM's approval only for players; the DM/Owner
    // applies them directly. Hidden until the role is known.
    final myRole = ref.watch(campaignDetailControllerProvider(_initial.campaignId)).value?.myRole;
    final needsApproval = isActive && myRole != null && !myRole.isAtLeastDm;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Editar hoja'),
        actions: [
          OfflineAware(
            builder: (context, canWrite) => TextButton(
              key: const Key('editor-save'),
              onPressed: _saving || !canWrite ? null : _save,
              child: const Text('Guardar'),
            ),
          ),
        ],
      ),
      body: Form(
        key: _formKey,
        child: SingleChildScrollView(
          padding: EdgeInsets.fromLTRB(16, 8, 16, 32 + MediaQuery.paddingOf(context).bottom),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (needsApproval)
                const Card(
                  margin: EdgeInsets.symmetric(vertical: 8),
                  child: Padding(
                    padding: EdgeInsets.all(12),
                    child: Text(
                      'Los cambios de un personaje activo se envían al DM para su aprobación.',
                    ),
                  ),
                ),
              const SectionTitle('Identidad'),
              TextFormField(
                key: const Key('editor-name'),
                controller: _nameController,
                maxLength: 100,
                textCapitalization: TextCapitalization.words,
                decoration: const InputDecoration(labelText: 'Nombre'),
                validator: (v) => (v == null || v.trim().isEmpty) ? 'Introduce un nombre' : null,
              ),
              _buildRaceSection(),
              const SizedBox(height: 8),
              _buildBackgroundAndAlignment(),
              const SectionTitle('Puntos de golpe'),
              SegmentedButton<HpMode>(
                key: const Key('editor-hp-mode'),
                showSelectedIcon: false,
                segments: [
                  for (final m in HpMode.values) ButtonSegment(value: m, label: Text(m.label)),
                ],
                selected: {_hpMode},
                onSelectionChanged: (s) => setState(() => _hpMode = s.first),
              ),
              if (_hpMode == HpMode.manual)
                const Padding(
                  padding: EdgeInsets.only(top: 8),
                  child: Text(
                    'En modo manual añade un valor modificado de "Puntos de golpe máximos".',
                  ),
                ),
              _buildAbilitiesSection(),
              _buildClassesSection(catalogClasses),
              _buildProficienciesSection(),
              _buildSpellsSection(catalogClasses),
              _buildOverridesSection(),
              const SectionTitle('Dinero'),
              TextFormField(
                key: const Key('editor-gold'),
                controller: _goldController,
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.,]'))],
                decoration: const InputDecoration(labelText: 'Dinero (gp)', suffixText: 'gp'),
                validator: (v) =>
                    goldTextToCopper(v ?? '') == null ? 'Introduce una cantidad válida' : null,
              ),
              const SectionTitle('Notas e historia'),
              TextFormField(
                key: const Key('editor-notes'),
                controller: _notesController,
                minLines: 3,
                maxLines: 8,
                textCapitalization: TextCapitalization.sentences,
                decoration: const InputDecoration(labelText: 'Notas', alignLabelWithHint: true),
              ),
              const SizedBox(height: 12),
              TextFormField(
                key: const Key('editor-backstory'),
                controller: _backstoryController,
                minLines: 3,
                maxLines: 8,
                textCapitalization: TextCapitalization.sentences,
                decoration: const InputDecoration(
                  labelText: 'Historia del personaje',
                  alignLabelWithHint: true,
                ),
              ),
              const SectionTitle('Personalidad'),
              _personalityField(
                const Key('editor-personality-traits'),
                _traitsController,
                'Rasgos de personalidad',
                personalityTextMaxLength,
              ),
              _personalityField(
                const Key('editor-ideals'),
                _idealsController,
                'Ideal',
                personalityTextMaxLength,
              ),
              _personalityField(
                const Key('editor-bonds'),
                _bondsController,
                'Vínculo',
                personalityTextMaxLength,
              ),
              _personalityField(
                const Key('editor-flaws'),
                _flawsController,
                'Defecto',
                personalityTextMaxLength,
              ),
              _personalityField(
                const Key('editor-background-detail'),
                _backgroundDetailController,
                'Detalle del trasfondo',
                backgroundDetailMaxLength,
              ),
              const SizedBox(height: 24),
              SizedBox(
                width: double.infinity,
                child: OfflineAware(
                  builder: (context, canWrite) => FilledButton(
                    key: const Key('editor-save-bottom'),
                    onPressed: _saving || !canWrite ? null : _save,
                    child: const Text('Guardar'),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _personalityField(Key key, TextEditingController controller, String label, int max) =>
      Padding(
        padding: const EdgeInsets.only(bottom: 12),
        child: TextFormField(
          key: key,
          controller: controller,
          minLines: 1,
          maxLines: 5,
          textCapitalization: TextCapitalization.sentences,
          decoration: InputDecoration(labelText: label, alignLabelWithHint: true),
          validator: (v) => (v ?? '').trim().length > max ? 'Como máximo $max caracteres' : null,
        ),
      );

  Widget _buildRaceSection() {
    final races = ref.watch(racesProvider);
    final raceDetail = _raceIndex == null ? null : ref.watch(raceDetailProvider(_raceIndex!));
    return Column(
      children: [
        _CatalogDropdown(
          fieldKey: const Key('editor-race'),
          label: 'Raza',
          value: _raceIndex,
          options: races.value?.map((r) => (index: r.index, name: r.name)).toList() ?? const [],
          loading: races.isLoading,
          onChanged: (value) => setState(() {
            if (value != _raceIndex) _subraceIndex = null;
            _raceIndex = value;
          }),
        ),
        const SizedBox(height: 8),
        _CatalogDropdown(
          fieldKey: const Key('editor-subrace'),
          label: 'Subraza',
          value: _subraceIndex,
          options:
              raceDetail?.value?.subraces.map((s) => (index: s.index, name: s.name)).toList() ??
              const [],
          loading: raceDetail?.isLoading ?? false,
          onChanged: (value) => setState(() => _subraceIndex = value),
        ),
        SwitchListTile(
          key: const Key('editor-racial-bonuses'),
          contentPadding: EdgeInsets.zero,
          title: const Text('Aplicar bonos raciales'),
          value: _applyRacial,
          onChanged: (value) => setState(() => _applyRacial = value),
        ),
      ],
    );
  }

  Widget _buildBackgroundAndAlignment() {
    final backgrounds = ref.watch(backgroundsProvider);
    return Column(
      children: [
        _CatalogDropdown(
          fieldKey: const Key('editor-background'),
          label: 'Trasfondo',
          value: _backgroundIndex,
          options:
              backgrounds.value?.map((b) => (index: b.index, name: b.name)).toList() ?? const [],
          loading: backgrounds.isLoading,
          onChanged: (value) => setState(() => _backgroundIndex = value),
        ),
        const SizedBox(height: 8),
        _CatalogDropdown(
          fieldKey: const Key('editor-alignment'),
          label: 'Alineamiento',
          value: _alignment,
          options: [for (final e in alignments.entries) (index: e.key, name: e.value)],
          onChanged: (value) => setState(() => _alignment = value),
        ),
      ],
    );
  }

  Widget _buildAbilitiesSection() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionTitle('Puntuaciones base'),
        Wrap(
          spacing: 12,
          runSpacing: 8,
          children: [
            for (final k in abilityKeys)
              SizedBox(
                width: 96,
                child: TextFormField(
                  key: Key('base-$k'),
                  controller: _baseControllers[k],
                  keyboardType: TextInputType.number,
                  inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                  decoration: InputDecoration(labelText: abilityLabel(k)),
                  validator: (v) {
                    final n = int.tryParse((v ?? '').trim());
                    return (n == null || n < 1 || n > 30) ? '1 a 30' : null;
                  },
                ),
              ),
          ],
        ),
        const SizedBox(height: 8),
        OutlinedButton.icon(
          key: const Key('editor-point-buy'),
          onPressed: _pointBuy,
          icon: const Icon(Icons.calculate_outlined),
          label: const Text('Compra por puntos'),
        ),
      ],
    );
  }

  Widget _buildClassesSection(List<ClassSummary> catalog) {
    final total = _totalLevel;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionTitle('Clases'),
        for (var i = 0; i < _classes.length; i++)
          _ClassRowTile(
            key: ObjectKey(_classes[i]),
            index: i,
            row: _classes[i],
            catalog: catalog,
            usedByOthers: {
              for (var j = 0; j < _classes.length; j++)
                if (j != i) _classes[j].classIndex,
            },
            onClassChanged: (value) => _onClassChosen(i, value),
            onLevelChanged: (value) => setState(() => _classes[i].level = value),
            onSubclassChanged: (value) => setState(() => _classes[i].subclassIndex = value),
            onRemove: () {
              setState(() => _classes.removeAt(i));
              if (i == 0 && _classes.isNotEmpty && _classes.first.classIndex != null) {
                _applyMainClassSaves(_classes.first.classIndex!);
              }
            },
          ),
        Row(
          children: [
            OutlinedButton.icon(
              key: const Key('editor-add-class'),
              onPressed: _classes.length >= catalog.length && catalog.isNotEmpty
                  ? null
                  : () => _addClass(catalog),
              icon: const Icon(Icons.add),
              label: const Text('Añadir clase'),
            ),
            const SizedBox(width: 12),
            Text(
              'Nivel total: $total',
              key: const Key('editor-total-level'),
              style: TextStyle(
                color: total > 20 ? Theme.of(context).colorScheme.error : null,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ),
      ],
    );
  }

  Widget _buildProficienciesSection() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionTitle('Competencias'),
        Text('Salvaciones', style: Theme.of(context).textTheme.titleSmall),
        const SizedBox(height: 4),
        Wrap(
          spacing: 8,
          runSpacing: 4,
          children: [
            for (final k in abilityKeys)
              FilterChip(
                key: Key('save-prof-$k'),
                label: Text(abilityLabel(k)),
                selected: _saveProf.contains(k),
                onSelected: (on) => setState(() => on ? _saveProf.add(k) : _saveProf.remove(k)),
              ),
          ],
        ),
        const SizedBox(height: 12),
        Text('Habilidades', style: Theme.of(context).textTheme.titleSmall),
        for (final s in _skills)
          Row(
            key: Key('skill-row-${s.index}'),
            children: [
              Checkbox(
                key: Key('skill-prof-${s.index}'),
                value: _skillProf.contains(s.index),
                onChanged: (on) => setState(() {
                  if (on ?? false) {
                    _skillProf.add(s.index);
                  } else {
                    _skillProf.remove(s.index);
                    _skillExpertise.remove(s.index);
                  }
                }),
              ),
              Expanded(
                child: Text(
                  s.ability.isEmpty ? s.label : '${s.label} (${abilityAbbreviation(s.ability)})',
                ),
              ),
              const Text('Pericia'),
              Checkbox(
                key: Key('skill-exp-${s.index}'),
                value: _skillExpertise.contains(s.index),
                onChanged: _skillProf.contains(s.index)
                    ? (on) => setState(
                        () => (on ?? false)
                            ? _skillExpertise.add(s.index)
                            : _skillExpertise.remove(s.index),
                      )
                    : null,
              ),
            ],
          ),
      ],
    );
  }

  Widget _buildSpellsSection(List<ClassSummary> catalog) {
    final info = ref.watch(spellInfoProvider(spellInfoKey(_spells.map((s) => s.spellIndex)))).value;
    // Only the DM (or the owner of a draft) decides what is prepared; a player
    // prepares through "Prepara tus conjuros".
    final myRole = ref.watch(campaignDetailControllerProvider(_initial.campaignId)).value?.myRole;
    final canPrepare = _initial.status != CharacterStatus.active || (myRole?.isAtLeastDm ?? false);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionTitle('Hechizos'),
        if (_spells.isEmpty) const Text('Sin hechizos.'),
        for (var i = 0; i < _spells.length; i++)
          Builder(
            builder: (context) {
              final s = _spells[i];
              final spell = info?[s.spellIndex];
              final level = s.level ?? spell?.level;
              return ListTile(
                key: Key('spell-row-${s.spellIndex}'),
                contentPadding: EdgeInsets.zero,
                leading: SpellCategoryIcon(s.category ?? spell?.category),
                title: Text(s.name ?? spell?.name ?? titleFromSpellIndex(s.spellIndex)),
                subtitle: Text(
                  [
                    if (level != null) spellLevelLabel(level),
                    titleFromIndex(s.classIndex),
                  ].join(' · '),
                ),
                trailing: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    if (s.alwaysPrepared)
                      const Text('Siempre')
                    else if (!canPrepare)
                      Text(s.isPrepared ? 'Preparado' : 'No preparado')
                    else ...[
                      const Text('Preparado'),
                      Checkbox(
                        key: Key('spell-prepared-${s.spellIndex}'),
                        value: s.isPrepared,
                        onChanged: (on) => setState(
                          () => _spells[i] = CharacterSpell(
                            spellIndex: s.spellIndex,
                            classIndex: s.classIndex,
                            isPrepared: on ?? false,
                            alwaysPrepared: s.alwaysPrepared,
                            name: s.name,
                            level: s.level,
                            category: s.category,
                          ),
                        ),
                      ),
                    ],
                    IconButton(
                      key: Key('spell-remove-${s.spellIndex}'),
                      tooltip: 'Quitar hechizo',
                      onPressed: () => setState(() => _spells.removeAt(i)),
                      icon: const Icon(Icons.close),
                    ),
                  ],
                ),
              );
            },
          ),
        const SizedBox(height: 8),
        OutlinedButton.icon(
          key: const Key('editor-add-spell'),
          onPressed: () => _addSpells(catalog),
          icon: const Icon(Icons.search),
          label: const Text('Buscar hechizos'),
        ),
      ],
    );
  }

  Widget _buildOverridesSection() {
    final used = {for (final o in _overrides) o.field};
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionTitle('Valores modificados'),
        const Text('Sustituyen al valor calculado de la hoja. La nota explica el motivo.'),
        const SizedBox(height: 8),
        for (var i = 0; i < _overrides.length; i++)
          Card(
            key: ObjectKey(_overrides[i]),
            margin: const EdgeInsets.symmetric(vertical: 6),
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: DropdownButtonFormField<String>(
                          key: Key('override-field-$i'),
                          initialValue: _overrides[i].field,
                          isExpanded: true,
                          decoration: const InputDecoration(labelText: 'Campo'),
                          items: [
                            for (final f in overrideFields)
                              if (f == _overrides[i].field || !used.contains(f))
                                DropdownMenuItem(
                                  value: f,
                                  child: Text(
                                    overrideFieldLabel(f),
                                    overflow: TextOverflow.ellipsis,
                                  ),
                                ),
                          ],
                          onChanged: (value) => setState(() => _overrides[i].field = value),
                        ),
                      ),
                      IconButton(
                        key: Key('override-remove-$i'),
                        tooltip: 'Quitar valor modificado',
                        onPressed: () {
                          final row = _overrides.removeAt(i);
                          setState(() {});
                          // Disposed after the frame so the field is no longer in the tree.
                          WidgetsBinding.instance.addPostFrameCallback((_) => row.dispose());
                        },
                        icon: const Icon(Icons.delete_outline),
                      ),
                    ],
                  ),
                  TextFormField(
                    key: Key('override-value-$i'),
                    controller: _overrides[i].valueController,
                    keyboardType: const TextInputType.numberWithOptions(signed: true),
                    inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'-?[0-9]*'))],
                    decoration: const InputDecoration(labelText: 'Valor'),
                    validator: (v) =>
                        int.tryParse((v ?? '').trim()) == null ? 'Número entero' : null,
                  ),
                  TextFormField(
                    key: Key('override-note-$i'),
                    controller: _overrides[i].noteController,
                    maxLength: 500,
                    decoration: const InputDecoration(labelText: 'Nota (opcional)'),
                  ),
                ],
              ),
            ),
          ),
        OutlinedButton.icon(
          key: const Key('editor-add-override'),
          onPressed: used.length >= overrideFields.length ? null : _addOverride,
          icon: const Icon(Icons.add),
          label: const Text('Añadir valor modificado'),
        ),
      ],
    );
  }
}

typedef _Option = ({String index, String name});

/// Dropdown over catalog entries. The current value is always offered, even if
/// the catalog has not loaded or no longer contains it.
class _CatalogDropdown extends StatelessWidget {
  const _CatalogDropdown({
    required this.fieldKey,
    required this.label,
    required this.value,
    required this.options,
    required this.onChanged,
    this.loading = false,
  });

  final Key fieldKey;
  final String label;
  final String? value;
  final List<_Option> options;
  final bool loading;
  final ValueChanged<String?> onChanged;

  @override
  Widget build(BuildContext context) {
    final items = [...options];
    if (value != null && !items.any((o) => o.index == value)) {
      items.add((index: value!, name: alignmentLabel(titleFromIndex(value!))));
    }
    return DropdownButtonFormField<String?>(
      // The key changes with the value so the field follows programmatic resets.
      key: ValueKey('$fieldKey|$value'),
      initialValue: value,
      isExpanded: true,
      decoration: InputDecoration(labelText: label, helperText: loading ? 'Cargando…' : null),
      items: [
        const DropdownMenuItem<String?>(value: null, child: Text('Sin elegir')),
        for (final o in items)
          DropdownMenuItem<String?>(
            value: o.index,
            child: Text(o.name, overflow: TextOverflow.ellipsis),
          ),
      ],
      onChanged: onChanged,
    );
  }
}

class _ClassRowTile extends ConsumerWidget {
  const _ClassRowTile({
    super.key,
    required this.index,
    required this.row,
    required this.catalog,
    required this.usedByOthers,
    required this.onClassChanged,
    required this.onLevelChanged,
    required this.onSubclassChanged,
    required this.onRemove,
  });

  final int index;
  final _ClassRow row;
  final List<ClassSummary> catalog;
  final Set<String?> usedByOthers;
  final ValueChanged<String> onClassChanged;
  final ValueChanged<int> onLevelChanged;
  final ValueChanged<String?> onSubclassChanged;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final classIndex = row.classIndex;
    final showSubclass = classIndex != null && row.level >= subclassUnlockLevel(classIndex);
    final detail = showSubclass ? ref.watch(classDetailProvider(classIndex)).value : null;

    final options = [
      for (final c in catalog)
        if (c.index == classIndex || !usedByOthers.contains(c.index))
          (index: c.index, name: c.name),
    ];
    if (classIndex != null && !options.any((o) => o.index == classIndex)) {
      options.add((index: classIndex, name: titleFromIndex(classIndex)));
    }
    final subclasses = [
      for (final s in detail?.subclasses ?? const []) (index: s.index, name: s.name),
    ];
    final subclassIndex = row.subclassIndex;
    if (subclassIndex != null && !subclasses.any((s) => s.index == subclassIndex)) {
      subclasses.add((index: subclassIndex, name: titleFromIndex(subclassIndex)));
    }

    return Card(
      margin: const EdgeInsets.symmetric(vertical: 6),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          children: [
            Row(
              children: [
                Expanded(
                  flex: 3,
                  child: DropdownButtonFormField<String>(
                    key: Key('class-select-$index'),
                    initialValue: classIndex,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'Clase'),
                    items: [
                      for (final o in options)
                        DropdownMenuItem(
                          value: o.index,
                          child: Text(o.name, overflow: TextOverflow.ellipsis),
                        ),
                    ],
                    onChanged: (value) {
                      if (value != null) onClassChanged(value);
                    },
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  flex: 2,
                  child: DropdownButtonFormField<int>(
                    key: Key('class-level-$index'),
                    initialValue: row.level,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'Nivel'),
                    items: [
                      for (var l = 1; l <= 20; l++) DropdownMenuItem(value: l, child: Text('$l')),
                    ],
                    onChanged: (value) {
                      if (value != null) onLevelChanged(value);
                    },
                  ),
                ),
                IconButton(
                  key: Key('class-remove-$index'),
                  tooltip: 'Quitar clase',
                  onPressed: onRemove,
                  icon: const Icon(Icons.delete_outline),
                ),
              ],
            ),
            if (showSubclass)
              Padding(
                padding: const EdgeInsets.only(top: 8),
                child: DropdownButtonFormField<String?>(
                  key: Key('class-subclass-$index'),
                  initialValue: row.subclassIndex,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Subclase'),
                  items: [
                    const DropdownMenuItem<String?>(value: null, child: Text('Sin subclase')),
                    for (final s in subclasses)
                      DropdownMenuItem<String?>(
                        value: s.index,
                        child: Text(s.name, overflow: TextOverflow.ellipsis),
                      ),
                  ],
                  onChanged: onSubclassChanged,
                ),
              ),
          ],
        ),
      ),
    );
  }
}
