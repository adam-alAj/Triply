import 'dart:io' show Platform, Socket, SocketException;

import 'package:device_info_plus/device_info_plus.dart';
import 'package:flutter/foundation.dart';

/// Resolves the backend's base URL for every run target this app supports,
/// so nothing here is a value someone has to remember to edit by hand
/// before running on a different target.
///
/// Resolution order:
/// 1. `--dart-define=BASE_URL=...` — always wins when given (CI, a
///    physical device over Wi-Fi with no USB bridge, a deployed backend).
/// 2. Platform-specific auto-detection (see [_detect]).
/// 3. A last-resort default if nothing above produced or confirmed a URL.
class ApiConfig {
  ApiConfig._();

  /// Local-dev backend port. One named constant instead of typing `8080`
  /// into every candidate URL below.
  static const int devPort = 8080;

  /// Set via `flutter run --dart-define=BASE_URL=http://192.168.1.23:8080`.
  /// Empty when not passed.
  static const String _override = String.fromEnvironment('BASE_URL');

  static String? _cached;

  /// Resolves and caches the base URL for this app run. Safe to call more
  /// than once — later calls return the cached value instead of re-probing.
  static Future<String> resolve() async {
    final cached = _cached;
    if (cached != null) return cached;

    if (_override.isNotEmpty) {
      _log('override', _override, '--dart-define=BASE_URL was set');
      return _cached = _override;
    }

    final resolved = await _detect();
    _cached = resolved;
    return resolved;
  }

  static Future<String> _detect() async {
    if (kIsWeb) {
      return _decide('web', 'http://localhost:$devPort', 'Flutter Web always reaches the host via localhost');
    }

    if (!Platform.isAndroid && !Platform.isIOS) {
      // Desktop (Windows/macOS/Linux): the app runs directly on the host
      // machine that's also running the backend.
      return _decide('desktop', 'http://localhost:$devPort', 'Desktop app and backend share the same machine');
    }

    final deviceInfo = DeviceInfoPlugin();

    if (Platform.isAndroid) {
      final info = await deviceInfo.androidInfo;
      if (!info.isPhysicalDevice) {
        return _decide(
          'Android emulator',
          'http://10.0.2.2:$devPort',
          'Emulator reaches the host machine via the 10.0.2.2 loopback alias',
        );
      }

      // Real device. Two ways it can reach the host:
      // - USB with `adb reverse tcp:$devPort tcp:$devPort` already run —
      //   the device's own localhost then forwards to the host's.
      // - Same Wi-Fi network — needs the host's real LAN IP, which a
      //   device can't discover on its own; that's what BASE_URL is for.
      if (await _canReach('localhost', devPort)) {
        return _decide(
          'physical Android device (USB)',
          'http://localhost:$devPort',
          'adb reverse is forwarding the device\'s localhost to the host',
        );
      }

      _warn(
        'physical Android device, but localhost:$devPort is not '
        'reachable (no adb reverse?) and no --dart-define=BASE_URL was '
        'given. Falling back to 10.0.2.2, which will NOT work on real '
        'hardware — run `adb reverse tcp:$devPort tcp:$devPort` for USB, '
        'or pass --dart-define=BASE_URL=http://<host-lan-ip>:$devPort '
        'for Wi-Fi.',
      );
      return _decide(
        'physical Android device (unreachable — see warning above)',
        'http://10.0.2.2:$devPort',
        'last-resort default; almost certainly wrong for this device',
      );
    }

    // iOS.
    final info = await deviceInfo.iosInfo;
    if (!info.isPhysicalDevice) {
      return _decide(
        'iOS simulator',
        'http://127.0.0.1:$devPort',
        'Simulator shares the host machine\'s network namespace',
      );
    }

    if (await _canReach('localhost', devPort)) {
      return _decide(
        'physical iOS device (USB)',
        'http://localhost:$devPort',
        'iproxy/adb-reverse-equivalent is forwarding to the host',
      );
    }

    _warn(
      'physical iOS device, but localhost:$devPort is not '
      'reachable and no --dart-define=BASE_URL was given. A real iPhone/'
      'iPad has no 10.0.2.2-style alias at all — pass '
      '--dart-define=BASE_URL=http://<host-lan-ip>:$devPort for Wi-Fi, or '
      'set up USB port forwarding (e.g. via `iproxy`) for USB.',
    );
    return _decide(
      'physical iOS device (unreachable — see warning above)',
      'http://127.0.0.1:$devPort',
      'last-resort default; almost certainly wrong for this device',
    );
  }

  static String _decide(String environment, String url, String reason) {
    _log(environment, url, reason);
    return url;
  }

  static void _log(String environment, String url, String reason) {
    if (!kDebugMode) return;
    debugPrint(
      '[ApiConfig] Detected environment: $environment | Base URL: $url | $reason',
    );
  }

  static void _warn(String message) {
    if (!kDebugMode) return;
    debugPrint('[ApiConfig] WARNING: $message');
  }

  static Future<bool> _canReach(String host, int port) async {
    try {
      final socket = await Socket.connect(
        host,
        port,
        timeout: const Duration(milliseconds: 800),
      );
      socket.destroy();
      return true;
    } on SocketException {
      return false;
    }
  }

  /// Exposed for tests / callers that need to force a specific URL without
  /// going through [resolve]'s probing (e.g. pointing at a fake server).
  @visibleForTesting
  static void debugOverrideCache(String? url) => _cached = url;
}
