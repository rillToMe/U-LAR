using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Button "CRIMP". Muncul hanya saat crimper benar-benar duduk di area
/// crimping konektor yang menunggu — jadi pemain melihat posisinya pas dulu,
/// baru tombolnya keluar. Klik = meneruskan ke
/// <see cref="CrimpController.TryCrimp"/>, lalu tombolnya hilang lagi.
///
/// Tidak ada aturan gameplay di sini. Sumber kebenaran satu-satunya adalah
/// <see cref="CrimpController.CanCrimpNow"/>; <c>TryCrimp</c> tetap memeriksa
/// sendiri state konektor, jadi tidak ada jalan pintas dari sisi UI.
///
/// Area HANYA GERBANG TAMPILAN — tidak ada validasi urutan warna di sini.
/// Susunan wire salah tetap boleh di-crimp; penilaian T568A/B milik LAN Tester.
///
/// PENTING : <c>Root</c> wajib diisi dengan tombol VISUAL (mis. di Canvas),
/// bukan GameObject komponen ini. Komponen logika harus tetap aktif supaya
/// bisa memunculkan tombolnya lagi untuk endpoint berikutnya.
/// </summary>
public class CrimpButton : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Pemilik crimper dan aturan crimping. Wajib.")]
    [SerializeField] private CrimpController crimpController;
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
                $"[{nameof(CrimpButton)}] Crimp Controller belum diassign — " +
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

    // Posisi crimper hanya berubah lewat drag (CrimpTool), dan tool itu tidak
    // mempublikasikan event pergerakan. Jadi syarat tampil dibaca tiap frame :
    // murni jarak, tanpa raycast, tanpa alokasi.
    private void Update() => Refresh();

    /// <summary>
    /// Dipakai Button.onClick, dan boleh juga dipasang manual di Inspector
    /// (public supaya UnityEvent bisa menemukannya).
    /// </summary>
    public void OnClicked()
    {
        if (crimpController == null)
            return;

        // Nilai kembaliannya dibaca : klik yang ditolak tidak boleh terlihat
        // seperti berhasil. Alasannya sudah di-log controller.
        if (crimpController.TryCrimp())
        {
            Debug.Log($"[{nameof(CrimpButton)}] CRIMP ditekan — konektor ter-crimp.", this);
        }

        Refresh();
    }

    /// <summary>
    /// Menyelaraskan tampilan dengan state. Sumber kebenarannya
    /// <see cref="CrimpController.CanCrimpNow"/>.
    ///
    /// Menyembunyikan DIRI SENDIRI tidak pernah dilakukan : kalau itu terjadi,
    /// komponen ini ikut mati, polling berhenti, listener klik dilepas, dan
    /// tombol tidak akan pernah muncul lagi. Bila <c>Root</c> salah diisi
    /// (kosong / objek ini sendiri), yang jalan hanya bagian interactable-nya —
    /// dan salah konfigurasi itu dilaporkan sekali.
    /// </summary>
    private void Refresh()
    {
        bool show = crimpController != null && crimpController.CanCrimpNow;

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
            $"[{nameof(CrimpButton)}] Root kosong atau menunjuk GameObject komponen " +
            $"ini — tombol visual tidak bisa disembunyikan. Isi Root dengan tombol di " +
            $"Canvas, bukan objek logika ini.", this);
    }

    private bool warnedSelfHide;

#if UNITY_EDITOR
    [ContextMenu("Crimp Button/Log Status")]
    private void LogStatus()
    {
        Debug.Log(
            $"[{nameof(CrimpButton)}] " +
            $"crimpController={(crimpController != null ? crimpController.name : "belum diassign")}  " +
            $"canCrimpNow={(crimpController != null ? crimpController.CanCrimpNow.ToString() : "?")}  " +
            $"target={(crimpController != null ? crimpController.CrimpTargetPosition.ToString("F4") : "?")}\n" +
            $"  button={(button != null ? button.name : "belum diassign")}  " +
            $"root={(root != null ? root.name : "belum diassign")}  " +
            $"rootActive={(root != null ? root.activeSelf.ToString() : "?")}",
            this);
    }
#endif
}
