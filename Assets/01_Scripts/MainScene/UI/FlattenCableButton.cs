using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Button "FLATTEN CABLE". Satu-satunya isinya : meneruskan klik ke
/// <see cref="InnerWireStageController.BeginFlatten"/>, lalu menyembunyikan diri
/// begitu tidak berguna lagi.
///
/// Tidak ada aturan gameplay di sini. Yang memutuskan boleh/tidak tetap stage
/// controller — button hanya bertanya lewat <c>CanBeginFlatten</c> untuk urusan
/// tampilan. Kalau pertanyaannya dilewati, <c>BeginFlatten</c> tetap menolak
/// sendiri, jadi tidak ada jalan pintas dari sisi UI.
///
/// Dependency satu arah : UI -> Puzzle. Komponen ini tidak tahu InnerWireFlatten,
/// InnerWireOrdering, wire, maupun warna. Satu-satunya hal dari Cable Module yang
/// disentuhnya adalah <see cref="CablePoint.OnStateChanged"/> — dipakai hanya
/// sebagai pemicu Refresh, bukan untuk membaca aturan (aturan tetap di
/// <c>CanBeginFlatten</c>).
///
/// Interactable dimatikan DAN GameObject disembunyikan setelah flatten. Project
/// ini belum punya konsep "klik dimakan UI" — ToolInput meraycast pada tiap
/// press tanpa memeriksa EventSystem — jadi button yang cuma non-interactable
/// tetap menutupi wire di belakangnya secara visual sambil klik-nya menembus.
/// Menyembunyikannya menutup celah itu tanpa menyentuh ToolInput.
/// </summary>
public class FlattenCableButton : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Pemilik tahap. Wajib.")]
    [SerializeField] private InnerWireStageController stageController;
    [Tooltip("Kosong = Button pada GameObject ini sendiri.")]
    [SerializeField] private Button button;

    [Header("Behaviour")]
    [Tooltip("Sembunyikan GameObject button setelah flatten dimulai. Matikan " +
             "bila kamu ingin button tetap terlihat (tapi tidak bisa diklik).")]
    [SerializeField] private bool hideWhenDone = true;
    [Tooltip("Root yang disembunyikan. Kosong = GameObject komponen ini. Isi " +
             "dengan parent bila button punya label/background terpisah.")]
    [SerializeField] private GameObject root;

    // Runtime saja : CablePoint di-resolve dari stageController (GameObject
    // endpoint yang sama), bukan field serialized. Disimpan supaya langganan di
    // OnDisable dilepas dari instance yang persis sama dengan yang dipasang.
    private CablePoint cablePoint;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (root == null)
            root = gameObject;
    }

    private void OnEnable()
    {
        if (button == null)
        {
            Debug.LogError(
                $"[{nameof(FlattenCableButton)}] Button belum diassign dan tidak ada " +
                $"komponen Button di GameObject ini — klik tidak akan tersambung.", this);
        }
        else
        {
            button.onClick.AddListener(OnClicked);
        }

        if (stageController == null)
        {
            // Tidak crash : button sekadar tidak berguna, dan alasannya disebut
            // lengkap dengan cara memperbaikinya.
            Debug.LogError(
                $"[{nameof(FlattenCableButton)}] Stage Controller belum diassign. " +
                $"Assign {nameof(InnerWireStageController)} (ada di GameObject endpoint, " +
                $"mis. 'A') ke field Stage Controller — tanpa itu button tidak bisa " +
                $"memulai flatten.", this);
        }
        else
        {
            stageController.OnStageChanged += HandleStageChanged;

            // CableState hidup di CablePoint pada GameObject endpoint yang sama
            // dengan stage controller. Langganan ini WAJIB : strip mengubah
            // CableState, bukan InnerWireStage, jadi tanpa event ini tidak ada
            // apa pun yang membangunkan Refresh setelah endpoint di-strip —
            // button tetap non-interactable (dan tidak bisa diklik, sehingga
            // tidak pernah refresh) dan alur berhenti.
            cablePoint = stageController.GetComponent<CablePoint>();

            if (cablePoint == null)
            {
                Debug.LogError(
                    $"[{nameof(FlattenCableButton)}] CablePoint tidak ada di GameObject " +
                    $"'{stageController.name}' (endpoint yang sama dengan Stage Controller). " +
                    $"Button tidak akan menyala setelah strip.", this);
            }
            else
            {
                cablePoint.OnStateChanged += HandlePointStateChanged;
            }
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClicked);

        if (stageController != null)
            stageController.OnStageChanged -= HandleStageChanged;

        if (cablePoint != null)
            cablePoint.OnStateChanged -= HandlePointStateChanged;

        cablePoint = null;
    }

    /// <summary>
    /// Dipakai Button.onClick, dan boleh juga dipasang manual di Inspector
    /// (public supaya UnityEvent bisa menemukannya).
    /// </summary>
    public void OnClicked()
    {
        if (stageController == null)
        {
            Debug.LogError(
                $"[{nameof(FlattenCableButton)}] Klik diabaikan — Stage Controller " +
                $"belum diassign.", this);
            return;
        }

        // Nilai kembaliannya dibaca : klik yang ditolak tidak boleh terlihat
        // seperti berhasil, dan alasannya sudah di-log stage controller.
        if (!stageController.BeginFlatten())
        {
            Refresh();
            return;
        }

        Debug.Log($"[{nameof(FlattenCableButton)}] FLATTEN CABLE ditekan.", this);

        Refresh();
    }

    private void HandleStageChanged(InnerWireStage stage) => Refresh();

    /// <summary>
    /// Strip tidak mengubah <see cref="InnerWireStage"/> — hanya CableState — jadi
    /// OnStageChanged tidak ikut menyala. Event ini yang membangunkan tombol saat
    /// endpoint di-strip (dan saat state lain berubah, mis. ke RJ45Mounted).
    /// </summary>
    private void HandlePointStateChanged(CableState state) => Refresh();

    /// <summary>
    /// Menyelaraskan tampilan dengan tahap. Sumber kebenarannya
    /// <see cref="InnerWireStageController.CanBeginFlatten"/> — tidak ada bool
    /// kedua di sini yang bisa desync.
    /// </summary>
    private void Refresh()
    {
        bool usable = stageController != null && stageController.CanBeginFlatten;

        if (button != null)
            button.interactable = usable;

        if (!hideWhenDone || root == null)
            return;

        // Disembunyikan hanya setelah flatten benar-benar BERHASIL.
        //
        // Selagi Flattening button tetap ada (walau tidak bisa diklik) karena
        // flatten bisa gagal dan tahap dikembalikan ke Messy — dan kalau root
        // yang disembunyikan adalah GameObject komponen ini sendiri, OnDisable
        // ikut melepas langganan, sehingga button tidak akan pernah tahu tahap
        // sudah balik ke Messy dan hilang selamanya.
        bool done =
            stageController != null &&
            (stageController.Stage == InnerWireStage.Flattened ||
             stageController.Stage == InnerWireStage.Sorting ||
             stageController.Stage == InnerWireStage.Completed);

        if (root.activeSelf != done)
            return;

        root.SetActive(!done);
    }

#if UNITY_EDITOR
    [ContextMenu("Flatten Button/Log Status")]
    private void LogStatus()
    {
        Debug.Log(
            $"[{nameof(FlattenCableButton)}] " +
            $"stageController={(stageController != null ? stageController.name : "belum diassign")}  " +
            $"stage={(stageController != null ? stageController.Stage.ToString() : "?")}  " +
            $"canBeginFlatten={(stageController != null ? stageController.CanBeginFlatten.ToString() : "?")}\n" +
            $"  button={(button != null ? button.name : "belum diassign")}  " +
            $"interactable={(button != null ? button.interactable.ToString() : "?")}  " +
            $"root={(root != null ? root.name : "belum diassign")}",
            this);
    }
#endif
}
