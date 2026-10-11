#if UNITY_ANDROID && !UNITY_EDITOR

using System;
using System.IO;
using System.Text;
using UnityEngine;

public class AndroidSecureStorage : ISecureStorage
{
    private const string KeyAlias = "ular_auth_refresh_token";

    private const string FileName =
        "auth_refresh_token.dat";

    private const string KeyStoreName =
        "AndroidKeyStore";

    private const string Transformation =
        "AES/GCM/NoPadding";

    private const int GcmTagBits = 128;

    // javax.crypto.Cipher.ENCRYPT_MODE / DECRYPT_MODE
    private const int EncryptMode = 1;
    private const int DecryptMode = 2;

    // KeyProperties.PURPOSE_ENCRYPT | PURPOSE_DECRYPT
    private const int KeyPurposes = 3;

    private string FilePath =>
        Path.Combine(
            Application.persistentDataPath,
            FileName
        );

    public bool HasRefreshToken()
    {
        return File.Exists(FilePath);
    }

    public bool SaveRefreshToken(string refreshToken)
    {
        try
        {
            if (string.IsNullOrEmpty(refreshToken))
                return false;

            File.WriteAllBytes(
                FilePath,
                Encrypt(refreshToken)
            );

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"Gagal menyimpan refresh token Android: {ex}"
            );

            return false;
        }
    }

    public string GetRefreshToken()
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;

            return Decrypt(
                File.ReadAllBytes(FilePath)
            );
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"Gagal membaca refresh token Android: {ex}"
            );

            return null;
        }
    }

    public bool DeleteRefreshToken()
    {
        try
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"Gagal menghapus refresh token Android: {ex}"
            );

            return false;
        }
    }

    // Format berkas: [4 byte panjang IV][IV][ciphertext].
    private byte[] Encrypt(string plainText)
    {
        using AndroidJavaObject cipher =
            CreateCipher();

        using AndroidJavaObject secretKey =
            GetOrCreateKey();

        cipher.Call(
            "init",
            EncryptMode,
            secretKey
        );

        byte[] encrypted =
            cipher.Call<byte[]>(
                "doFinal",
                Encoding.UTF8.GetBytes(plainText)
            );

        byte[] iv =
            cipher.Call<byte[]>("getIV");

        byte[] result =
            new byte[
                4 +
                iv.Length +
                encrypted.Length
            ];

        Buffer.BlockCopy(
            BitConverter.GetBytes(iv.Length),
            0,
            result,
            0,
            4
        );

        Buffer.BlockCopy(
            iv,
            0,
            result,
            4,
            iv.Length
        );

        Buffer.BlockCopy(
            encrypted,
            0,
            result,
            4 + iv.Length,
            encrypted.Length
        );

        return result;
    }

    private string Decrypt(byte[] data)
    {
        int ivLength =
            BitConverter.ToInt32(
                data,
                0
            );

        byte[] iv =
            new byte[ivLength];

        Buffer.BlockCopy(
            data,
            4,
            iv,
            0,
            ivLength
        );

        byte[] encrypted =
            new byte[
                data.Length -
                4 -
                ivLength
            ];

        Buffer.BlockCopy(
            data,
            4 + ivLength,
            encrypted,
            0,
            encrypted.Length
        );

        using AndroidJavaObject spec =
            new AndroidJavaObject(
                "javax.crypto.spec.GCMParameterSpec",
                GcmTagBits,
                iv
            );

        using AndroidJavaObject cipher =
            CreateCipher();

        using AndroidJavaObject secretKey =
            GetOrCreateKey();

        cipher.Call(
            "init",
            DecryptMode,
            secretKey,
            spec
        );

        return Encoding.UTF8.GetString(
            cipher.Call<byte[]>(
                "doFinal",
                encrypted
            )
        );
    }

    private static AndroidJavaObject CreateCipher()
    {
        using AndroidJavaClass cipherClass =
            new AndroidJavaClass(
                "javax.crypto.Cipher"
            );

        return cipherClass.CallStatic<AndroidJavaObject>(
            "getInstance",
            Transformation
        );
    }

    private static AndroidJavaObject GetOrCreateKey()
    {
        using (AndroidJavaObject probe = OpenKeyStore())
        {
            if (probe.Call<bool>(
                    "containsAlias",
                    KeyAlias))
            {
                return probe.Call<AndroidJavaObject>(
                    "getKey",
                    KeyAlias,
                    null
                );
            }
        }

        CreateKey();

        // Buka ulang KeyStore supaya kunci baru pasti terlihat.
        using AndroidJavaObject keyStore =
            OpenKeyStore();

        return keyStore.Call<AndroidJavaObject>(
            "getKey",
            KeyAlias,
            null
        );
    }

    private static AndroidJavaObject OpenKeyStore()
    {
        using AndroidJavaClass keyStoreClass =
            new AndroidJavaClass(
                "java.security.KeyStore"
            );

        AndroidJavaObject keyStore =
            keyStoreClass.CallStatic<AndroidJavaObject>(
                "getInstance",
                KeyStoreName
            );

        keyStore.Call(
            "load",
            null,
            null
        );

        return keyStore;
    }

    private static void CreateKey()
    {
        using AndroidJavaClass keyGeneratorClass =
            new AndroidJavaClass(
                "javax.crypto.KeyGenerator"
            );

        using AndroidJavaObject generator =
            keyGeneratorClass.CallStatic<AndroidJavaObject>(
                "getInstance",
                "AES",
                KeyStoreName
            );

        using AndroidJavaObject specBuilder =
            new AndroidJavaObject(
                "android.security.keystore.KeyGenParameterSpec$Builder",
                KeyAlias,
                KeyPurposes
            );

        // setBlockModes / setEncryptionPaddings String... (varargs)
        specBuilder.Call<AndroidJavaObject>(
            "setBlockModes",
            new object[] { new[] { "GCM" } }
        );

        specBuilder.Call<AndroidJavaObject>(
            "setEncryptionPaddings",
            new object[] { new[] { "NoPadding" } }
        );

        specBuilder.Call<AndroidJavaObject>(
            "setKeySize",
            256
        );

        using AndroidJavaObject spec =
            specBuilder.Call<AndroidJavaObject>(
                "build"
            );

        generator.Call(
            "init",
            spec
        );

        generator.Call<AndroidJavaObject>(
            "generateKey"
        );
    }
}

#endif
