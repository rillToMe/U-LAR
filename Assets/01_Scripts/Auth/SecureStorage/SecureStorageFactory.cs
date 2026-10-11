using UnityEngine;

public static class SecureStorageFactory
{
    public static ISecureStorage Create()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return new AndroidSecureStorage();

#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        return new WindowsSecureStorage();

#else
        Debug.LogError(
            "Platform secure storage belum didukung."
        );

        return null;
#endif
    }
}