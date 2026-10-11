using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menu jeda: tombol PAUSE di HUD membekukan permainan, tombol Resume
/// mengembalikannya. Isi panelnya sudah disusun di scene (Resume / Settings /
/// Quit) — di sini hanya perilakunya.
///
/// Dua hal yang harus berhenti saat pause, dan keduanya harus:
/// 1. waktu  — <c>Time.timeScale = 0</c>, sehingga semua yang memakai
///             <c>Time.deltaTime</c> berhenti.
/// 2. input  — mode digeser ke <see cref="InputMode.UI"/> lewat
///             <see cref="InputModeSwitcher"/>. Tanpa ini, CableInput (butuh
///             DragCable) dan ToolInput (butuh UseTool) tetap memproses klik,
///             jadi kabel masih bisa di-drag dan tool 3D masih menyala walau
///             waktu sudah beku.
///
/// Mode input sebelumnya DIINGAT, bukan diasumsikan DragCable — pemain bisa
/// saja menekan PAUSE saat sedang memegang tool. Penulisan mode tetap lewat
/// <see cref="InputModeSwitcher"/> karena dialah pemilik tunggal
/// <see cref="InputModeService"/>.
///
/// <c>Time.timeScale</c> itu global: kalau scene ditinggalkan dalam keadaan
/// pause, scene berikutnya ikut beku. Karena itu status pause selalu dilepas
/// saat komponen ini mati (OnDisable) dan sebelum pindah scene (Quit).
///
/// PENTING : komponen ini harus menempel di GameObject yang SELALU aktif
/// (mis. Canvas), bukan di panel-nya — kalau menempel di panel, menutup panel
/// melepas listener tombol PAUSE dan panel tidak bisa dibuka lagi.
/// </summary>
public class PausePanel : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Root panel pause yang dimunculkan/disembunyikan. Wajib, dan JANGAN " +
             "GameObject komponen ini sendiri.")]
    [SerializeField] private GameObject pausePanel;

    [Tooltip("Tombol PAUSE di HUD.")]
    [SerializeField] private Button pauseButton;

    [Tooltip("Tombol Resume di dalam panel.")]
    [SerializeField] private Button resumeButton;

    [Tooltip("Tombol Settings di dalam panel. Panel Settings dibuka DI ATAS panel " +
             "pause, sehingga Back dari Settings kembali ke sini.")]
    [SerializeField] private Button settingsButton;

    [Tooltip("Panel Settings yang dibuka tombol Settings. Kosong = tombol Settings " +
             "diabaikan.")]
    [SerializeField] private SettingsPanel settingsPanel;

    [Tooltip("Tombol Quit di dalam panel.")]
    [SerializeField] private Button quitButton;

    [Tooltip("Pemilik mode input. Dipakai untuk memblokir CableInput/ToolInput " +
             "selama pause. Kosong = waktu beku tapi input tetap jalan.")]
    [SerializeField] private InputModeSwitcher inputModeSwitcher;

    [Header("Behaviour")]
    [Tooltip("Scene yang dibuka tombol Quit. Harus terdaftar di Build Profiles.")]
    [SerializeField] private string quitSceneName = "MainMenu";

    private bool isPaused;
    private float timeScaleBeforePause = 1f;
    private InputMode modeBeforePause;
    private bool modeWasConfigured;
    private bool warnedMissingSwitcher;

    private void Awake()
    {
        if (pausePanel == null)
        {
            Debug.LogError(
                $"[{nameof(PausePanel)}] Pause Panel belum diassign — tombol PAUSE " +
                $"tidak akan membuka apa pun. Assign GameObject 'Pause' di Canvas.", this);
            return;
        }

        if (pausePanel == gameObject)
        {
            Debug.LogError(
                $"[{nameof(PausePanel)}] Pause Panel menunjuk GameObject komponen ini " +
                $"sendiri — menutup panel ikut mematikan komponen ini beserta " +
                $"listener-nya. Pindahkan komponen ini ke Canvas.", this);
        }

        // Panel sengaja dibiarkan aktif di scene supaya layout-nya kelihatan saat
        // mengedit; ditutup dari sini saat runtime.
        pausePanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (pauseButton != null)
            pauseButton.onClick.AddListener(TogglePause);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(Resume);

        if (settingsPanel != null && settingsButton != null)
            settingsButton.onClick.AddListener(OpenSettings);

        if (quitButton != null)
            quitButton.onClick.AddListener(Quit);
    }

    private void OnDisable()
    {
        if (pauseButton != null)
            pauseButton.onClick.RemoveListener(TogglePause);

        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(Resume);

        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OpenSettings);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(Quit);

        // Jangan tinggalkan scene dalam keadaan beku (mis. scene di-unload dari
        // keadaan pause) — Time.timeScale global dan akan ikut ke scene berikutnya.
        if (isPaused)
            Resume();
    }

    /// <summary>Dipakai tombol PAUSE di HUD: sekali tekan pause, tekan lagi lanjut.</summary>
    public void TogglePause()
    {
        if (isPaused)
            Resume();
        else
            Pause();
    }

    /// <summary>Dipakai Button.onClick, dan boleh dipasang manual di Inspector.</summary>
    public void Pause()
    {
        if (isPaused)
            return;

        isPaused = true;

        // Diingat apa adanya, bukan diasumsikan 1 / DragCable.
        timeScaleBeforePause = Time.timeScale;
        modeBeforePause = InputModeService.Current;
        modeWasConfigured = InputModeService.IsConfigured;

        Time.timeScale = 0f;

        if (inputModeSwitcher != null)
            inputModeSwitcher.SetUI();
        else
            WarnMissingSwitcher();

        if (pausePanel != null)
            pausePanel.SetActive(true);

        Debug.Log($"[{nameof(PausePanel)}] Pause — permainan dibekukan.", this);
    }

    /// <summary>Dipakai Button.onClick Resume. Aman dipanggil walau tidak sedang pause.</summary>
    public void Resume()
    {
        if (isPaused)
        {
            isPaused = false;

            // timeScaleBeforePause bisa 0 kalau ada yang membekukan permainan
            // lebih dulu — jangan diteruskan, nanti tidak bisa lanjut sama sekali.
            Time.timeScale = timeScaleBeforePause > 0f ? timeScaleBeforePause : 1f;

            if (inputModeSwitcher != null && modeWasConfigured)
                inputModeSwitcher.SetMode(modeBeforePause);
        }

        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    /// <summary>
    /// Panel Settings dibuka tanpa menutup panel pause, jadi Back di Settings
    /// kembali ke menu pause — bukan ke permainan.
    /// </summary>
    public void OpenSettings()
    {
        if (settingsPanel == null)
            return;

        settingsPanel.Open();
    }

    /// <summary>
    /// Keluar dari sesi latihan. Status pause dilepas lebih dulu: Time.timeScale
    /// global, scene tujuan akan ikut beku kalau dibiarkan.
    /// </summary>
    public void Quit()
    {
        Resume();

        if (string.IsNullOrEmpty(quitSceneName))
        {
            Application.Quit();
            return;
        }

        Debug.Log($"[{nameof(PausePanel)}] Quit — kembali ke scene '{quitSceneName}'.", this);
        SceneManager.LoadScene(quitSceneName);
    }

    private void WarnMissingSwitcher()
    {
        if (warnedMissingSwitcher)
            return;

        warnedMissingSwitcher = true;

        Debug.LogWarning(
            $"[{nameof(PausePanel)}] Input Mode Switcher belum diassign — waktu " +
            $"berhenti tapi CableInput/ToolInput masih memproses klik. Assign " +
            $"InputModeSwitcher ke field itu.", this);
    }

#if UNITY_EDITOR
    [ContextMenu("Pause/Log Status")]
    private void LogStatus()
    {
        Debug.Log(
            $"[{nameof(PausePanel)}] " +
            $"pausePanel={(pausePanel != null ? pausePanel.name : "belum diassign")}  " +
            $"panelActive={(pausePanel != null ? pausePanel.activeSelf.ToString() : "?")}  " +
            $"isPaused={isPaused}  timeScale={Time.timeScale}  " +
            $"pause={(pauseButton != null ? pauseButton.name : "belum diassign")}  " +
            $"resume={(resumeButton != null ? resumeButton.name : "belum diassign")}  " +
            $"settings={(settingsButton != null ? settingsButton.name : "belum diassign")}  " +
            $"quit={(quitButton != null ? quitButton.name : "belum diassign")}",
            this);
    }
#endif
}
