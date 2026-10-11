using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel pengaturan MainScene: dibuka dari tombol SETTINGS di HUD, ditutup dari
/// tombol Back di dalam panel. Isinya tiga opsi — Music, Sound Effect, Language —
/// sama seperti panel pengaturan MainMenu.
///
/// Tidak ada aturan gameplay di sini. Penyambungan klik dan opsi dilakukan dari
/// kode (OnEnable/OnDisable), bukan UnityEvent di Inspector: referensi yang
/// salah langsung kelihatan lewat Debug.LogError, dan tidak ada listener
/// "hantu" yang ikut tersalin ke scene lain.
///
/// PENTING : komponen ini harus menempel di GameObject yang SELALU aktif
/// (mis. Canvas), bukan di panel-nya. Kalau menempel di panel, menutup panel
/// ikut mematikan komponen ini, OnDisable melepas listener tombol SETTINGS, dan
/// panel tidak akan pernah bisa dibuka lagi.
///
/// Opsi Music/Sound Effect/Language belum disimpan ke mana pun — method di
/// bawah hanya mencatat perubahannya. Kalau nanti mau dihubungkan ke AudioMixer
/// atau penyimpanan, titik sambungnya sudah satu tempat.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Root panel yang dimunculkan/disembunyikan. Wajib diisi, dan JANGAN " +
             "GameObject komponen ini sendiri.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("Tombol SETTINGS di HUD (panel tertutup).")]
    [SerializeField] private Button openButton;

    [Tooltip("Tombol Back di dalam panel (panel terbuka).")]
    [SerializeField] private Button closeButton;

    [Tooltip("Tombol Logout di dalam panel. Opsional — kosongkan kalau panel " +
             "tidak perlu keluar dari sesi.")]
    [SerializeField] private Button logoutButton;

    [SerializeField] private Toggle musicToggle;
    [SerializeField] private Toggle soundEffectToggle;
    [SerializeField] private TMP_Dropdown languageDropdown;

    private void Awake()
    {
        if (panelRoot == null)
        {
            Debug.LogError(
                $"[{nameof(SettingsPanel)}] Panel Root belum diassign — tombol SETTINGS " +
                $"tidak akan membuka apa pun. Assign GameObject 'SettingsPanel' di Canvas.",
                this);
            return;
        }

        if (panelRoot == gameObject)
        {
            Debug.LogError(
                $"[{nameof(SettingsPanel)}] Panel Root menunjuk GameObject komponen ini " +
                $"sendiri — menutup panel ikut mematikan komponen ini beserta " +
                $"listener-nya. Pindahkan komponen ini ke Canvas.", this);
        }

        // Panel sengaja dibiarkan aktif di scene supaya layout-nya kelihatan saat
        // mengedit; ditutup dari sini saat runtime.
        panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (openButton != null)
            openButton.onClick.AddListener(Open);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (logoutButton != null)
            logoutButton.onClick.AddListener(Logout);

        if (musicToggle != null)
            musicToggle.onValueChanged.AddListener(SetMusic);

        if (soundEffectToggle != null)
            soundEffectToggle.onValueChanged.AddListener(SetSoundEffect);

        if (languageDropdown != null)
            languageDropdown.onValueChanged.AddListener(SetLanguage);
    }

    private void OnDisable()
    {
        if (openButton != null)
            openButton.onClick.RemoveListener(Open);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);

        if (logoutButton != null)
            logoutButton.onClick.RemoveListener(Logout);

        if (musicToggle != null)
            musicToggle.onValueChanged.RemoveListener(SetMusic);

        if (soundEffectToggle != null)
            soundEffectToggle.onValueChanged.RemoveListener(SetSoundEffect);

        if (languageDropdown != null)
            languageDropdown.onValueChanged.RemoveListener(SetLanguage);
    }

    /// <summary>
    /// Dipakai Button.onClick tombol SETTINGS, dan boleh juga dipasang manual di
    /// Inspector (public supaya UnityEvent bisa menemukannya).
    /// </summary>
    public void Open()
    {
        if (panelRoot == null)
            return;

        panelRoot.SetActive(true);
    }

    /// <summary>Dipakai Button.onClick tombol Back di dalam panel.</summary>
    public void Close()
    {
        if (panelRoot == null)
            return;

        panelRoot.SetActive(false);
    }

    /// <summary>
    /// Dipakai Button.onClick tombol Logout di dalam panel — sama seperti di
    /// MainMenu, keluar dari sesi lewat AuthManager (yang memutuskan pindah scene).
    /// </summary>
    public void Logout()
    {
        AuthManager authManager = AuthManager.Instance;

        if (authManager == null)
        {
            Debug.LogError(
                $"[{nameof(SettingsPanel)}] AuthManager.Instance tidak ditemukan — " +
                $"Logout diabaikan.", this);
            return;
        }

        authManager.Logout();
    }

    // Opsi. Belum ada penyimpanan — log dulu, titik sambungnya di sini.
    public void SetMusic(bool enabled) =>
        Debug.Log($"[{nameof(SettingsPanel)}] Music: {enabled}", this);

    public void SetSoundEffect(bool enabled) =>
        Debug.Log($"[{nameof(SettingsPanel)}] Sound Effect: {enabled}", this);

    public void SetLanguage(int index) =>
        Debug.Log($"[{nameof(SettingsPanel)}] Language: {index}", this);

#if UNITY_EDITOR
    [ContextMenu("Settings/Log Status")]
    private void LogStatus()
    {
        Debug.Log(
            $"[{nameof(SettingsPanel)}] " +
            $"panelRoot={(panelRoot != null ? panelRoot.name : "belum diassign")}  " +
            $"panelActive={(panelRoot != null ? panelRoot.activeSelf.ToString() : "?")}  " +
            $"open={(openButton != null ? openButton.name : "belum diassign")}  " +
            $"close={(closeButton != null ? closeButton.name : "belum diassign")}  " +
            $"logout={(logoutButton != null ? logoutButton.name : "belum diassign")}  " +
            $"music={(musicToggle != null ? musicToggle.name : "belum diassign")}  " +
            $"sound={(soundEffectToggle != null ? soundEffectToggle.name : "belum diassign")}  " +
            $"language={(languageDropdown != null ? languageDropdown.name : "belum diassign")}",
            this);
    }
#endif
}
