using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Button "KELUARKAN CRIMPING TOOL". Satu-satunya isinya : meminta
/// <see cref="CrimpController.Reveal"/> supaya crimper masuk ke meja, lalu
/// menyembunyikan diri sampai ada konektor berikutnya yang perlu di-crimp.
///
/// Tidak ada aturan gameplay di sini. Yang memutuskan boleh/tidak tetap
/// controller — button hanya bertanya lewat <c>HasTarget</c> /
/// <c>IsRevealed</c> untuk urusan tampilan, dan kliknya pun hanya memanggil
/// API yang sudah ada.
///
/// Muncul HANYA saat ada konektor yang sudah Inserted dan crimper belum
/// keluar. Karena syaratnya berubah dari luar (hasil insert RJ45, dan posisi
/// crimper yang digeser pemain), tampilan disegarkan tiap frame — ceknya
/// sekadar jarak/state, tanpa raycast dan tanpa alokasi.
///
/// PENTING : <c>Root</c> wajib diisi dengan tombol VISUAL (mis. di Canvas),
/// bukan GameObject komponen ini. Komponen logika harus tetap aktif supaya
/// bisa memunculkan tombolnya lagi untuk endpoint berikutnya.
/// </summary>
public class ShowCrimpToolButton : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Pemilik crimper dan aturan crimping. Wajib.")]
    [SerializeField] private CrimpController crimpController;
    [Tooltip("Opsional. Saat crimper dikeluarkan, tool aktif digeser ke " +
             "CrimpingTool supaya langsung bisa di-grab. Kosong = tidak digeser.")]
    [SerializeField] private ToolManager toolManager;
    [Tooltip("Kosong = Button pada GameObject ini sendiri.")]
    [SerializeField] private Button button;

    [Header("Behaviour")]
    [Tooltip("Tombol visual yang disembunyikan. Kosong = GameObject komponen " +
             "ini (hindari — lihat catatan kelas).")]
    [SerializeField] private GameObject root;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (root == null)
            root = gameObject;
    }

    private void OnEnable()
    {
        if (crimpController == null)
        {
            Debug.LogError(
                $"[{nameof(ShowCrimpToolButton)}] Crimp Controller belum diassign — " +
                $"tombol ini tidak akan pernah muncul.", this);
        }
        else if (button != null)
        {
            button.onClick.AddListener(OnClicked);
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClicked);
    }

    // Syarat tampil berubah di luar komponen ini (state konektor hasil insert,
    // posisi crimper hasil drag), dan tidak ada event yang bisa dilanggan dari
    // sini tanpa membuka API baru. Ceknya murah : dua pembacaan + satu
    // perbandingan, tanpa alokasi.
    private void Update() => Refresh();

    /// <summary>
    /// Dipakai Button.onClick, dan boleh juga dipasang manual di Inspector
    /// (public supaya UnityEvent bisa menemukannya).
    /// </summary>
    public void OnClicked()
    {
        if (crimpController == null)
            return;

        crimpController.Reveal();

        // Tool aktif digeser lewat API ToolManager yang sudah ada — bukan
        // sistem pilihan tool baru. Kegagalan tidak membatalkan reveal.
        if (toolManager != null)
            toolManager.SetCurrentTool(ToolState.CrimpingTool);

        Debug.Log($"[{nameof(ShowCrimpToolButton)}] Crimping tool dikeluarkan.", this);

        Refresh();
    }

    /// <summary>
    /// Menyelaraskan tampilan dengan state. Sumber kebenarannya
    /// <see cref="CrimpController"/> — tidak ada bool kedua di sini yang bisa
    /// desync.
    ///
    /// Menyembunyikan DIRI SENDIRI tidak pernah dilakukan : kalau itu terjadi,
    /// komponen ini ikut mati, polling berhenti, listener klik dilepas, dan
    /// tombol tidak akan pernah muncul lagi. Bila <c>Root</c> salah diisi
    /// (kosong / objek ini sendiri), yang jalan hanya bagian interactable-nya —
    /// dan salah konfigurasi itu dilaporkan sekali.
    /// </summary>
    private void Refresh()
    {
        bool show =
            crimpController != null &&
            crimpController.HasTarget &&
            !crimpController.IsRevealed;

        if (button != null && button.interactable != show)
            button.interactable = show;

        if (root == null || root == gameObject)
        {
            WarnSelfHideOnce();
            return;
        }

        if (root.activeSelf != show)
            root.SetActive(show);
    }

    private void WarnSelfHideOnce()
    {
        if (warnedSelfHide)
            return;

        warnedSelfHide = true;

        Debug.LogWarning(
            $"[{nameof(ShowCrimpToolButton)}] Root kosong atau menunjuk GameObject " +
            $"komponen ini — tombol visual tidak bisa disembunyikan. Isi Root dengan " +
            $"tombol di Canvas, bukan objek logika ini.", this);
    }

    private bool warnedSelfHide;

#if UNITY_EDITOR
    [ContextMenu("Crimp Button/Log Status")]
    private void LogStatus()
    {
        Debug.Log(
            $"[{nameof(ShowCrimpToolButton)}] " +
            $"crimpController={(crimpController != null ? crimpController.name : "belum diassign")}  " +
            $"hasTarget={(crimpController != null ? crimpController.HasTarget.ToString() : "?")}  " +
            $"revealed={(crimpController != null ? crimpController.IsRevealed.ToString() : "?")}\n" +
            $"  button={(button != null ? button.name : "belum diassign")}  " +
            $"root={(root != null ? root.name : "belum diassign")}  " +
            $"rootActive={(root != null ? root.activeSelf.ToString() : "?")}",
            this);
    }
#endif
}
