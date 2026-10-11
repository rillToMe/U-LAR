using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Button "FINISH SORTING". Satu-satunya isinya : meneruskan klik ke
/// <see cref="InnerWireStageController.FinishSorting"/> — pernyataan manual
/// pemain bahwa pengurutan wire sudah selesai, benar maupun salah. Tidak ada
/// validasi warna di sini : yang memutuskan boleh/tidak tetap stage
/// controller, dan kebenaran susunan menunggu LAN Tester.
///
/// Dependency satu arah : UI -> Puzzle. Komponen ini tidak tahu wire, warna,
/// InnerWireOrdering, maupun ToolManager.
///
/// Seperti FlattenCableButton, root yang disembunyikan default-nya GameObject
/// komponen ini sendiri — JANGAN mengisinya dengan ToolsManager atau parent
/// alat lain : menyembunyikan root ikut mematikan komponen di dalamnya, dan
/// di sini OnDisable melepas langganan stage. Setelah Completed stage tidak
/// kembali (kecuali OnDisable stage controller), jadi sekali tersembunyi
/// button memang tidak dibutuhkan lagi.
///
/// Interactable dimatikan DAN GameObject disembunyikan setelah sorting
/// selesai — button non-interactable tetap terlihat menutupi area kerja
/// secara visual sementara kliknya menembus (ToolInput tidak mengenal konsep
/// "klik dimakan UI").
/// </summary>
public class FinishSortingButton : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Pemilik tahap. Wajib.")]
    [SerializeField] private InnerWireStageController stageController;
    [Tooltip("Kosong = Button pada GameObject ini sendiri.")]
    [SerializeField] private Button button;

    [Header("Behaviour")]
    [Tooltip("Sembunyikan root setelah sorting selesai. Matikan bila button " +
             "harus tetap terlihat (tapi tidak bisa diklik).")]
    [SerializeField] private bool hideWhenDone = true;
    [Tooltip("Root yang disembunyikan. Kosong = GameObject komponen ini. " +
             "JANGAN diisi ToolsManager.")]
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
        if (button == null)
        {
            Debug.LogError(
                $"[{nameof(FinishSortingButton)}] Button belum diassign dan tidak ada " +
                $"komponen Button di GameObject ini — klik tidak akan tersambung.", this);
        }
        else
        {
            button.onClick.AddListener(OnClicked);
        }

        if (stageController == null)
        {
            Debug.LogError(
                $"[{nameof(FinishSortingButton)}] Stage Controller belum diassign. " +
                $"Assign {nameof(InnerWireStageController)} (GameObject endpoint, " +
                $"mis. 'A') — tanpa itu button tidak bisa menyatakan selesai sorting.",
                this);
        }
        else
        {
            stageController.OnStageChanged += HandleStageChanged;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClicked);

        if (stageController != null)
            stageController.OnStageChanged -= HandleStageChanged;
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
                $"[{nameof(FinishSortingButton)}] Klik diabaikan — Stage Controller " +
                $"belum diassign.", this);
            return;
        }

        // Nilai kembaliannya dibaca : klik yang ditolak tidak boleh terlihat
        // seperti berhasil, dan alasannya sudah di-log stage controller.
        if (!stageController.FinishSorting())
        {
            Refresh();
            return;
        }

        Debug.Log($"[{nameof(FinishSortingButton)}] FINISH SORTING ditekan.", this);

        Refresh();
    }

    private void HandleStageChanged(InnerWireStage stage) => Refresh();

    /// <summary>
    /// Menyelaraskan tampilan dengan tahap. Interactable mengikuti
    /// <see cref="InnerWireStageController.CanFinishSorting"/> — tidak ada
    /// bool kedua di sini yang bisa desync.
    /// </summary>
    private void Refresh()
    {
        bool usable = stageController != null && stageController.CanFinishSorting;

        if (button != null)
            button.interactable = usable;

        if (!hideWhenDone || root == null)
            return;

        // Disembunyikan hanya setelah sorting benar-benar DINYATAKAN selesai
        // (Completed). Selama Messy/Flattening/Flattened/Sorting button tetap
        // tampil — di Messy/Flattening ia non-interactable, bukan hilang,
        // supaya langganan stage tidak pernah putus sebelum waktunya.
        bool done = stageController != null &&
                    stageController.Stage == InnerWireStage.Completed;

        if (root.activeSelf != done)
            return;

        root.SetActive(!done);
    }
}
