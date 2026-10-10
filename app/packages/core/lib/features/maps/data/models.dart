import 'package:flutter/material.dart';

import '../../../core/content/content_visibility.dart';
import '../../../core/files/stored_file.dart';

String? _strOrNull(Object? value) {
  final text = value?.toString() ?? '';
  return text.isEmpty ? null : text;
}

/// Icon of a map pin; the server stores only the name.
enum PinIcon {
  place('place', 'Lugar', Icons.place),
  city('city', 'Ciudad', Icons.location_city),
  dungeon('dungeon', 'Mazmorra', Icons.castle),
  quest('quest', 'Misión', Icons.flag),
  npc('npc', 'PNJ', Icons.person_pin_circle),
  danger('danger', 'Peligro', Icons.warning_amber),
  custom('custom', 'Otro', Icons.push_pin);

  const PinIcon(this.apiValue, this.label, this.icon);

  final String apiValue;
  final String label;
  final IconData icon;

  static PinIcon fromApi(Object? value) =>
      PinIcon.values.firstWhere((i) => i.apiValue == value, orElse: () => PinIcon.custom);
}

/// Colours a pin can have, as `#RRGGBB` (the format the server validates).
const pinColors = <String>[
  '#D32F2F',
  '#F57C00',
  '#FBC02D',
  '#388E3C',
  '#1976D2',
  '#7B1FA2',
  '#5D4037',
  '#212121',
];

/// Parses `#RRGGBB`; null when [hex] is null or malformed.
Color? parsePinColor(String? hex) {
  if (hex == null) return null;
  final match = RegExp(r'^#([0-9a-fA-F]{6})$').firstMatch(hex);
  if (match == null) return null;
  return Color(0xFF000000 | int.parse(match.group(1)!, radix: 16));
}

class MapSummary {
  const MapSummary({
    required this.id,
    required this.campaignId,
    required this.name,
    required this.fileId,
    required this.url,
    required this.widthPx,
    required this.heightPx,
    required this.visibility,
    this.sortOrder = 0,
  });

  factory MapSummary.fromJson(Map<String, dynamic> json) {
    final fileId = json['fileId'] as String? ?? '';
    return MapSummary(
      id: json['id'] as String,
      campaignId: json['campaignId'] as String? ?? '',
      name: json['name'] as String? ?? '',
      fileId: fileId,
      url: _strOrNull(json['url']) ?? filePath(fileId),
      widthPx: (json['widthPx'] as num?)?.toInt() ?? 0,
      heightPx: (json['heightPx'] as num?)?.toInt() ?? 0,
      visibility: ContentVisibility.fromApi(json['visibility']),
      sortOrder: (json['sortOrder'] as num?)?.toInt() ?? 0,
    );
  }

  final String id;
  final String campaignId;
  final String name;
  final String fileId;

  /// Relative URL of the image.
  final String url;
  final int widthPx;
  final int heightPx;
  final ContentVisibility visibility;
  final int sortOrder;

  /// Width over height of the image (1 when the size is unknown).
  double get aspectRatio => widthPx > 0 && heightPx > 0 ? widthPx / heightPx : 1;
}

class MapPin {
  const MapPin({
    required this.id,
    required this.mapId,
    required this.x,
    required this.y,
    required this.title,
    required this.icon,
    required this.visibility,
    this.note,
    this.color,
    this.loreEntryId,
  });

  factory MapPin.fromJson(Map<String, dynamic> json) => MapPin(
    id: json['id'] as String,
    mapId: json['mapId'] as String? ?? '',
    x: (json['x'] as num?)?.toDouble() ?? 0,
    y: (json['y'] as num?)?.toDouble() ?? 0,
    title: json['title'] as String? ?? '',
    note: _strOrNull(json['note']),
    icon: PinIcon.fromApi(json['icon']),
    color: _strOrNull(json['color']),
    loreEntryId: _strOrNull(json['loreEntryId']),
    visibility: ContentVisibility.fromApi(json['visibility']),
  );

  final String id;
  final String mapId;

  /// Relative position, 0 (left/top) to 1 (right/bottom).
  final double x;
  final double y;
  final String title;
  final String? note;
  final PinIcon icon;
  final String? color;
  final String? loreEntryId;
  final ContentVisibility visibility;

  MapPin copyWith({double? x, double? y}) => MapPin(
    id: id,
    mapId: mapId,
    x: x ?? this.x,
    y: y ?? this.y,
    title: title,
    note: note,
    icon: icon,
    color: color,
    loreEntryId: loreEntryId,
    visibility: visibility,
  );
}

/// A map with its pins; players only receive the visible ones.
class MapDetail extends MapSummary {
  const MapDetail({
    required super.id,
    required super.campaignId,
    required super.name,
    required super.fileId,
    required super.url,
    required super.widthPx,
    required super.heightPx,
    required super.visibility,
    super.sortOrder,
    this.pins = const [],
  });

  factory MapDetail.fromJson(Map<String, dynamic> json) {
    final summary = MapSummary.fromJson(json);
    final pins = json['pins'];
    return MapDetail(
      id: summary.id,
      campaignId: summary.campaignId,
      name: summary.name,
      fileId: summary.fileId,
      url: summary.url,
      widthPx: summary.widthPx,
      heightPx: summary.heightPx,
      visibility: summary.visibility,
      sortOrder: summary.sortOrder,
      pins: [
        if (pins is List)
          for (final p in pins)
            if (p is Map) MapPin.fromJson(Map<String, dynamic>.from(p)),
      ],
    );
  }

  final List<MapPin> pins;

  MapDetail withPins(List<MapPin> pins) => MapDetail(
    id: id,
    campaignId: campaignId,
    name: name,
    fileId: fileId,
    url: url,
    widthPx: widthPx,
    heightPx: heightPx,
    visibility: visibility,
    sortOrder: sortOrder,
    pins: pins,
  );
}

/// The editable fields of a pin (body of the create request and, in full, of
/// an edit: a null [color] or [loreEntryId] clears it).
class PinDraft {
  const PinDraft({
    required this.title,
    required this.icon,
    required this.visibility,
    this.note = '',
    this.color,
    this.loreEntryId,
  });

  factory PinDraft.of(MapPin pin) => PinDraft(
    title: pin.title,
    note: pin.note ?? '',
    icon: pin.icon,
    color: pin.color,
    loreEntryId: pin.loreEntryId,
    visibility: pin.visibility,
  );

  final String title;
  final String note;
  final PinIcon icon;
  final String? color;
  final String? loreEntryId;
  final ContentVisibility visibility;

  Map<String, dynamic> toJson() => {
    'title': title,
    'note': note,
    'icon': icon.apiValue,
    'color': color,
    'loreEntryId': loreEntryId,
    'visibility': visibility.apiValue,
  };
}
