class ServerInfo {
  const ServerInfo({required this.name, required this.version});

  factory ServerInfo.fromJson(Map<String, dynamic> json) =>
      ServerInfo(name: json['name'] as String, version: json['version'] as String);

  final String name;
  final String version;
}
