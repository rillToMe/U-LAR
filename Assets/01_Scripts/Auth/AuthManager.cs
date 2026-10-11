using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }

    [Header("References")]
    [SerializeField]
    private AuthService authService;

    [Header("Scene")]
    [SerializeField]
    private string loginScene = "Login";

    [SerializeField]
    private string mainMenuScene = "MainMenu";

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    public void LoginStudent(
        string nim,
        string password,
        Action onSuccess,
        Action<string> onError)
    {
        StartCoroutine(
            LoginStudentCoroutine(
                nim,
                password,
                onSuccess,
                onError
            )
        );
    }

    private IEnumerator LoginStudentCoroutine(
        string nim,
        string password,
        Action onSuccess,
        Action<string> onError)
    {
        yield return StartCoroutine(
            authService.LoginStudent(
                nim,
                password,
                response =>
                {
                    AuthSession.SetStudentSession(
                        response
                    );

                    if (AuthSession.IsLoggedIn)
                    {
                        onSuccess?.Invoke();
                    }
                    else
                    {
                        onError?.Invoke(
                            "Session gagal disimpan."
                        );
                    }
                },
                onError
            )
        );
    }

    public void TryRestoreSession(
        Action onSuccess,
        Action onFailure)
    {
        StartCoroutine(
            TryRestoreSessionCoroutine(
                onSuccess,
                onFailure
            )
        );
    }

    private IEnumerator TryRestoreSessionCoroutine(
        Action onSuccess,
        Action onFailure)
    {
        string refreshToken =
            AuthSession.GetRefreshToken();

        if (string.IsNullOrEmpty(refreshToken))
        {
            onFailure?.Invoke();
            yield break;
        }

        yield return StartCoroutine(
            authService.RefreshSession(
                refreshToken,
                response =>
                {
                    AuthSession.UpdateTokens(
                        response
                    );

                    onSuccess?.Invoke();
                },
                error =>
                {
                    Debug.LogWarning(
                        $"Restore session gagal: {error}"
                    );

                    AuthSession.Clear();

                    onFailure?.Invoke();
                }
            )
        );
    }

    public void GoToMainMenu()
    {
        SceneManager.LoadScene(
            mainMenuScene
        );
    }

    public void GoToLogin()
    {
        SceneManager.LoadScene(
            loginScene
        );
    }

    public void Logout()
    {
        StartCoroutine(LogoutCoroutine());
    }

    private IEnumerator LogoutCoroutine()
    {
        string refreshToken =
            AuthSession.GetRefreshToken();

        if (!string.IsNullOrEmpty(refreshToken))
        {
            yield return StartCoroutine(
                authService.Logout(
                    refreshToken,
                    error =>
                        Debug.LogWarning(
                            $"Logout server gagal: {error}"
                        )
                )
            );
        }

        // Access token mahasiswa stateless (umur 1 tahun),
        // jadi hapus lokal wajib walau request gagal.
        AuthSession.Clear();

        SceneManager.LoadScene(
            loginScene
        );
    }
}