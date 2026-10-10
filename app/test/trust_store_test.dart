import 'dart:io';

import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opentrpg_core/core/network/trust_store.dart';

// Throwaway self-signed CA generated only for this test.
const _validPem = '''
-----BEGIN CERTIFICATE-----
MIIDBTCCAe2gAwIBAgIUFZK7uQ6Rv7Xt8ZWO52CGibOYFcEwDQYJKoZIhvcNAQEL
BQAwEjEQMA4GA1UEAwwHVGVzdCBDQTAeFw0yNjEwMDUyMTA4MDhaFw0zNjEwMDIy
MTA4MDhaMBIxEDAOBgNVBAMMB1Rlc3QgQ0EwggEiMA0GCSqGSIb3DQEBAQUAA4IB
DwAwggEKAoIBAQDkfq0oS/demqCVl6uT0nv5pGfO144/xgYdR5zuRatNC/7LJBdA
KgkwZDl7N1Kj25KebyVBHVcj+PO0vv2cy25ojJ+RxK+4kx57MOhJfSE1t6ng5O7H
uhG3eoJjVzATx7Her23W+7Wl4GlyvyY1R2HrpOUiAsX0mlVel1MItRmYzNGupcM7
1PyaiDvhjvr3JxT8iyI3306aadTHyowrQxX4aCuq4kHNT1xS7FePlzQhJHy9pWd1
fXtZXMqSsY9WEnfbwvIrwE6AckqvrlX4ZGfVb9Z21AYx7oo3UfrJsSIBXaK6XJW+
RJ4vFDkTkV/cQEgXgQLhQiMXuynwyKFKwZWtAgMBAAGjUzBRMB0GA1UdDgQWBBRI
zf0Ht1kdv/x87zDCYv6V/0oogjAfBgNVHSMEGDAWgBRIzf0Ht1kdv/x87zDCYv6V
/0oogjAPBgNVHRMBAf8EBTADAQH/MA0GCSqGSIb3DQEBCwUAA4IBAQDI+zXkhHye
qgRWCaIN3nj0X3YtG9OHzIorlavtg31Q5pp0SBNyPTLPQINS00vcBD61ggrFPCDT
ftB8qpGf+5Lb8RkhG6dQhwrx6qrT1OwIpcrM/AjSNRsCAD3vAny1XMo2dH6g6LjA
H/cljiO80TyZxHHLP4MWtPkO3Ed+g6359AA43vg9MKbJOxBfi7r4GwSwDSh3trZT
XZAtZvRED7N00IxlhecaFggQW1Lw9ILXglvoBbd0GrLj75Yvzt2gSWDp0Pdi6HMf
84vfCHP9n2+2G62Mo1WDSb0M2SIV/zMGAQ4hEzKki7+Er4O8vTGJUF7VrdITDm6Q
Tk1T8JvDMV+u
-----END CERTIFICATE-----
''';

const _corruptPem = '''
-----BEGIN CERTIFICATE-----
bm90IGEgY2VydGlmaWNhdGU=
-----END CERTIFICATE-----
''';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  final messenger = TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger;
  late List<MethodCall> calls;

  void mockChannel(Future<Object?> Function(MethodCall call) handler) {
    messenger.setMockMethodCallHandler(TrustStore.channel, (call) {
      calls.add(call);
      return handler(call);
    });
  }

  setUp(() => calls = []);

  tearDown(() {
    messenger.setMockMethodCallHandler(TrustStore.channel, null);
    HttpOverrides.global = null;
  });

  group('TrustStore.fromPem', () {
    test('adds every valid certificate', () {
      final store = TrustStore.fromPem([_validPem]);
      expect(store.certificateCount, 1);
    });

    test('works with an empty list', () {
      final store = TrustStore.fromPem(const []);
      expect(store.certificateCount, 0);
      expect(store.context, isA<SecurityContext>());
    });

    test('skips a corrupt certificate without throwing', () {
      final store = TrustStore.fromPem([_corruptPem, _validPem, 'garbage']);
      expect(store.certificateCount, 1);
    });
  });

  group('TrustStore.load', () {
    test('reads the PEM list from the platform channel on Android', () async {
      mockChannel((_) async => [_validPem, _corruptPem]);
      final store = await TrustStore.load(isAndroid: true);
      expect(calls.single.method, 'userCertificates');
      expect(store.certificateCount, 1);
    });

    test('returns an empty store when the channel fails', () async {
      mockChannel((_) async => throw PlatformException(code: 'boom'));
      final store = await TrustStore.load(isAndroid: true);
      expect(store.certificateCount, 0);
    });

    test('returns an empty store when the channel is missing', () async {
      final store = await TrustStore.load(isAndroid: true);
      expect(store.certificateCount, 0);
    });

    test('does not touch the channel off Android', () async {
      mockChannel((_) async => [_validPem]);
      final store = await TrustStore.load(isAndroid: false);
      expect(calls, isEmpty);
      expect(store.certificateCount, 0);
    });
  });

  group('installGlobalHttpOverrides', () {
    test('installs overrides that still create working clients', () async {
      mockChannel((_) async => [_validPem]);
      final store = await installGlobalHttpOverrides(isAndroid: true);
      expect(store.certificateCount, 1);
      expect(HttpOverrides.current, isNotNull);

      final client = HttpClient();
      client.close(force: true);
      final explicit = HttpClient(context: SecurityContext());
      explicit.close(force: true);
    });
  });
}
