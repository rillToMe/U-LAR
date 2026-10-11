using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject settingsPanel;

    private void Start()
    {
        menuPanel.SetActive(true);
        settingsPanel.SetActive(false);
    }

    // MAIN MENU
    public void NewGame()
    {
        Debug.Log("New Game clicked!");

        SceneManager.LoadScene("MainScene");
    }

    public void OpenAssesment()
    {
        Debug.Log("Assesment clicked!");

        SceneManager.LoadScene("AssesmentWebView");
    }

    public void OpenMateri()
    {
        Debug.Log("Materi clicked!");

        SceneManager.LoadScene("MateriWebView");
    }

    public void Settings()
    {
        Debug.Log("Settings clicked!");

        menuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void Logout()
    {
        Debug.Log("Logout clicked!");

        AuthManager authManager = AuthManager.Instance;

        if (authManager == null)
        {
            Debug.LogError(
                "AuthManager.Instance tidak ditemukan."
            );

            return;
        }

        authManager.Logout();
    }

    public void Quit()
    {
        Debug.Log("Quit clicked!");

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // SETTINGS

    public void Back()
    {
        Debug.Log("Back clicked!");

        settingsPanel.SetActive(false);
        menuPanel.SetActive(true);
    }

    public void SetSoundEffect(bool enabled)
    {
        Debug.Log("Sound Effect: " + enabled);
    }

    public void SetMusic(bool enabled)
    {
        Debug.Log("Music: " + enabled);
    }

    public void SetVibration(bool enabled)
    {
        Debug.Log("Vibration: " + enabled);
    }
}