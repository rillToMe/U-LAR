using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BootManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private AuthManager authManager;

    [Header("Scenes")]
    [SerializeField]
    private string loginScene = "Login";

    [SerializeField]
    private string mainMenuScene = "MainMenu";

    [Header("Loading UI")]
    [SerializeField]
    private Slider progressBar;

    [SerializeField]
    private TMP_Text loadingText;

    private bool isLoadingScene;

    private void Start()
    {
        SetProgress(
            0f,
            "Memulai aplikasi..."
        );

        CheckAuthentication();
    }

    private void CheckAuthentication()
    {
        SetProgress(
            0.2f,
            "Memeriksa session..."
        );

        if (authManager == null)
        {
            Debug.LogError(
                "BootManager: AuthManager belum di-assign."
            );

            LoadTargetScene(loginScene);
            return;
        }

        authManager.TryRestoreSession(
            OnSessionRestored,
            OnSessionNotFound
        );
    }

    private void OnSessionRestored()
    {
        SetProgress(
            0.35f,
            "Session berhasil dipulihkan..."
        );

        LoadTargetScene(mainMenuScene);
    }

    private void OnSessionNotFound()
    {
        SetProgress(
            0.35f,
            "Menyiapkan login..."
        );

        LoadTargetScene(loginScene);
    }

    private void LoadTargetScene(string sceneName)
    {
        if (isLoadingScene)
            return;

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError(
                "Nama scene kosong."
            );

            return;
        }

        StartCoroutine(
            LoadSceneAsync(sceneName)
        );
    }

    private IEnumerator LoadSceneAsync(
        string sceneName)
    {
        isLoadingScene = true;

        SetProgress(
            0.4f,
            "Memuat scene..."
        );

        AsyncOperation operation =
            SceneManager.LoadSceneAsync(sceneName);

        if (operation == null)
        {
            Debug.LogError(
                $"Gagal memuat scene: {sceneName}"
            );

            isLoadingScene = false;
            yield break;
        }

        // Jangan langsung pindah scene.
        operation.allowSceneActivation = false;

        while (!operation.isDone)
        {
            // Unity AsyncOperation:
            // progress normalnya 0 sampai 0.9
            float sceneProgress =
                Mathf.Clamp01(
                    operation.progress / 0.9f
                );

            // Loading scene dimulai dari 40%.
            float totalProgress =
                Mathf.Lerp(
                    0.4f,
                    0.95f,
                    sceneProgress
                );

            SetProgress(
                totalProgress,
                "Memuat scene..."
            );

            if (operation.progress >= 0.9f)
            {
                SetProgress(
                    0.95f,
                    "Menyiapkan aplikasi..."
                );

                // Scene sudah selesai dimuat.
                // Aktifkan pada frame berikutnya.
                yield return null;

                SetProgress(
                    1f,
                    "Selesai!"
                );

                operation.allowSceneActivation = true;
            }

            yield return null;
        }

        isLoadingScene = false;
    }

    private void SetProgress(
        float progress,
        string message)
    {
        progress = Mathf.Clamp01(progress);

        if (progressBar != null)
            progressBar.value = progress;

        if (loadingText != null)
            loadingText.text = message;
    }
}