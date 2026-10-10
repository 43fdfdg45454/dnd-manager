package com.opentrpg.app

import android.util.Base64
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import java.security.KeyStore
import java.security.cert.X509Certificate

class MainActivity : FlutterActivity() {
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "com.dndcompanion/trust")
            .setMethodCallHandler { call, result ->
                if (call.method == "userCertificates") {
                    result.success(userCertificates())
                } else {
                    result.notImplemented()
                }
            }
    }

    /** PEM-encoded CA certificates the user installed on the device, or an empty list. */
    private fun userCertificates(): List<String> {
        return try {
            val store = KeyStore.getInstance("AndroidCAStore")
            store.load(null)
            val pems = mutableListOf<String>()
            for (alias in store.aliases()) {
                if (!alias.startsWith("user:")) continue
                val certificate = store.getCertificate(alias) as? X509Certificate ?: continue
                val body = Base64.encodeToString(certificate.encoded, Base64.NO_WRAP)
                    .chunked(64)
                    .joinToString("\n")
                pems.add("-----BEGIN CERTIFICATE-----\n$body\n-----END CERTIFICATE-----")
            }
            pems
        } catch (e: Exception) {
            emptyList()
        }
    }
}
