import '../../../core/auth/user_dto.dart';

class PagedUsers {
  const PagedUsers({
    required this.items,
    required this.total,
    required this.page,
    required this.pageSize,
  });

  factory PagedUsers.fromJson(Map<String, dynamic> json) => PagedUsers(
    items: (json['items'] as List<dynamic>)
        .map((e) => UserDto.fromJson(e as Map<String, dynamic>))
        .toList(),
    total: json['total'] as int,
    page: json['page'] as int,
    pageSize: json['pageSize'] as int,
  );

  final List<UserDto> items;
  final int total;
  final int page;
  final int pageSize;

  bool get hasMore => items.length < total;

  PagedUsers copyWith({List<UserDto>? items, int? total, int? page}) => PagedUsers(
    items: items ?? this.items,
    total: total ?? this.total,
    page: page ?? this.page,
    pageSize: pageSize,
  );
}
