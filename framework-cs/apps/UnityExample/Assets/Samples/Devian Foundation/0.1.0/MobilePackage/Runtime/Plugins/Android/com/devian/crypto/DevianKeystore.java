package com.devian.crypto;

import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;
import android.content.SharedPreferences;

import java.security.KeyStore;
import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

/**
 * Android Keystore-based device-bound key store.
 * KEK: AES-256 non-exportable key in Android Keystore.
 * DEK: 48-byte secret (key 32B + iv 16B) wrapped by KEK via AES-GCM.
 *
 * IMPORTANT: This is a minimal implementation stub.
 * Production use requires proper error handling and testing.
 */
public class DevianKeystore {

    private static final String KEYSTORE_ALIAS = "DevianLocalDEK";
    private static final String PREFS_NAME = "DevianCrypto";
    private static final String PREFS_WRAPPED = "wrappedDEK";
    private static final String PREFS_GCM_IV = "wrappedIV";
    private static final int GCM_TAG_LENGTH = 128;

    /**
     * Returns 48-byte secret (key 32B + iv 16B).
     * Creates and persists if not yet exists.
     */
    public static byte[] getOrCreateSecret48() throws Exception {
        KeyStore ks = KeyStore.getInstance("AndroidKeyStore");
        ks.load(null);

        // Ensure KEK exists
        if (!ks.containsAlias(KEYSTORE_ALIAS)) {
            createKEK();
        }

        SharedPreferences prefs = getPrefs();
        String wrappedB64 = prefs.getString(PREFS_WRAPPED, null);
        String gcmIvB64 = prefs.getString(PREFS_GCM_IV, null);

        if (wrappedB64 != null && gcmIvB64 != null) {
            // Unwrap existing DEK
            return unwrapDEK(wrappedB64, gcmIvB64);
        } else {
            // Generate new 48-byte secret and wrap it
            byte[] secret48 = new byte[48];
            new java.security.SecureRandom().nextBytes(secret48);
            wrapAndStoreDEK(secret48);
            return secret48;
        }
    }

    private static void createKEK() throws Exception {
        KeyGenerator kg = KeyGenerator.getInstance(
                KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore");
        KeyGenParameterSpec spec = new KeyGenParameterSpec.Builder(
                KEYSTORE_ALIAS,
                KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256)
                // non-exportable by default in Android Keystore
                .build();
        kg.init(spec);
        kg.generateKey();
    }

    private static void wrapAndStoreDEK(byte[] secret48) throws Exception {
        KeyStore ks = KeyStore.getInstance("AndroidKeyStore");
        ks.load(null);
        SecretKey kek = (SecretKey) ks.getKey(KEYSTORE_ALIAS, null);

        Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
        cipher.init(Cipher.ENCRYPT_MODE, kek);

        byte[] wrapped = cipher.doFinal(secret48);
        byte[] gcmIv = cipher.getIV();

        SharedPreferences.Editor editor = getPrefs().edit();
        editor.putString(PREFS_WRAPPED, Base64.encodeToString(wrapped, Base64.NO_WRAP));
        editor.putString(PREFS_GCM_IV, Base64.encodeToString(gcmIv, Base64.NO_WRAP));
        editor.apply();
    }

    private static byte[] unwrapDEK(String wrappedB64, String gcmIvB64) throws Exception {
        KeyStore ks = KeyStore.getInstance("AndroidKeyStore");
        ks.load(null);
        SecretKey kek = (SecretKey) ks.getKey(KEYSTORE_ALIAS, null);

        byte[] wrapped = Base64.decode(wrappedB64, Base64.NO_WRAP);
        byte[] gcmIv = Base64.decode(gcmIvB64, Base64.NO_WRAP);

        Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
        GCMParameterSpec gcmSpec = new GCMParameterSpec(GCM_TAG_LENGTH, gcmIv);
        cipher.init(Cipher.DECRYPT_MODE, kek, gcmSpec);

        return cipher.doFinal(wrapped);
    }

    private static SharedPreferences getPrefs() {
        return com.unity3d.player.UnityPlayer.currentActivity
                .getSharedPreferences(PREFS_NAME, android.content.Context.MODE_PRIVATE);
    }
}
