using UnityEngine;

public static class AuthSession
{
    private static ISecureStorage secureStorage;

    public static bool IsLoggedIn { get; private set; }

    public static int UserId { get; private set; }

    public static string NIM { get; private set; }
    public static string Name { get; private set; }
    public static string Email { get; private set; }
    public static string Role { get; private set; }

    public static string AccessToken { get; private set; }

    private static ISecureStorage Storage
    {
        get
        {
            if (secureStorage == null)
                secureStorage =
                    SecureStorageFactory.Create();

            return secureStorage;
        }
    }

    public static void SetStudentSession(
        StudentLoginResponse response)
    {
        if (response == null)
        {
            Debug.LogError(
                "Student login response null."
            );

            return;
        }

        UserId = response.userId;
        NIM = response.nim;
        Name = response.name;
        Email = response.email;
        Role = response.role;
        AccessToken = response.accessToken;

        if (!Storage.SaveRefreshToken(
                response.refreshToken))
        {
            Debug.LogError(
                "Refresh token gagal disimpan."
            );

            ClearMemory();
            return;
        }

        IsLoggedIn = true;

        Debug.Log(
            $"Session aktif: {Name} ({NIM})"
        );
    }

    public static bool HasRefreshToken()
    {
        return Storage != null &&
               Storage.HasRefreshToken();
    }

    public static string GetRefreshToken()
    {
        return Storage?.GetRefreshToken();
    }

    public static void UpdateTokens(
        RefreshTokenResponse response)
    {
        if (response == null)
            return;

        AccessToken =
            response.accessToken;

        if (!Storage.SaveRefreshToken(
                response.refreshToken))
        {
            Debug.LogError(
                "Refresh token baru gagal disimpan."
            );

            Clear();
            return;
        }

        IsLoggedIn = true;
    }

    public static void Clear()
    {
        Storage?.DeleteRefreshToken();

        ClearMemory();

        Debug.Log(
            "Auth session berhasil dihapus."
        );
    }

    private static void ClearMemory()
    {
        IsLoggedIn = false;

        UserId = 0;

        NIM = null;
        Name = null;
        Email = null;
        Role = null;
        AccessToken = null;
    }
}