using UnityEngine;

/// <summary>
/// Visual tangan selama proses meratakan. OPSIONAL sepenuhnya : komponen ini
/// tidak boleh menjadi syarat apa pun.
///
/// Ia hanya MENDENGAR <see cref="InnerWireStageController.OnStageChanged"/> dan
/// menggerakkan satu Transform yang kamu assign. Ia tidak memajukan tahap, tidak
/// menyentuh wire, tidak menyentuh zone, tidak tahu warna, dan tidak pernah
/// dipanggil oleh flatten — jadi mencabut komponen ini (atau membiarkan Hand
/// kosong) tidak mengubah hasil flatten sedikit pun.
///
/// Gerakannya prosedural : turun ke Press Anchor, menekan sebentar, lalu naik
/// kembali. Tidak ada AnimatorController maupun clip .anim yang dibuat — project
/// ini belum punya aset animasi, dan menuntut satu aset baru hanya untuk visual
/// opsional akan membuat fitur utamanya tidak bisa dites.
///
/// Kalau nanti ada model tangan ber-Animator, ganti isi
/// <see cref="Press"/>/<see cref="Lift"/> dengan Play() — antarmuka ke tahap
/// (OnStageChanged) tidak perlu berubah.
/// </summary>
public class FlattenHandView : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Pemilik tahap. Kosong = dicari di GameObject ini dan parent-nya.")]
    [SerializeField] private InnerWireStageController stageController;
    [Tooltip("OPSIONAL. Transform tangan yang digerakkan. Kosong = tanpa visual, " +
             "flatten tetap berjalan normal.")]
    [SerializeField] private Transform hand;
    [Tooltip("OPSIONAL. Titik yang ditekan tangan — arahkan ke FlattenZone_A. " +
             "Kosong = posisi awal tangan dipakai sebagai titik tekan.")]
    [SerializeField] private Transform pressAnchor;

    [Header("Motion")]
    [Tooltip("Tinggi tangan di atas titik tekan saat mulai & selesai (meter).")]
    [SerializeField] private float hoverHeight = 0.08f;
    [Tooltip("Arah 'naik' tangan. Kosong/zero = Vector3.up.")]
    [SerializeField] private Vector3 liftDirection = Vector3.up;
    [Tooltip("Sembunyikan GameObject tangan di luar proses flatten.")]
    [SerializeField] private bool hideWhenIdle = true;

    // Pose awal tangan di scene, dipulihkan setelah selesai. Runtime saja.
    private Vector3 restPosition;
    private bool restCaptured;
    private Coroutine motion;

    private void Awake()
    {
        if (stageController == null)
            stageController = GetComponentInParent<InnerWireStageController>();

        if (hand != null)
        {
            restPosition = hand.position;
            restCaptured = true;
        }
    }

    private void OnEnable()
    {
        if (stageController == null)
        {
            // Warning, bukan error : tangan memang opsional, jadi ketiadaannya
            // tidak boleh terlihat seperti kegagalan fitur.
            Debug.LogWarning(
                $"[{nameof(FlattenHandView)}] Stage Controller belum diassign dan tidak " +
                $"ditemukan di parent — visual tangan tidak akan bereaksi. Flatten tetap " +
                $"berjalan normal.", this);
        }
        else
        {
            stageController.OnStageChanged += HandleStageChanged;
        }

        Idle();
    }

    private void OnDisable()
    {
        if (stageController != null)
            stageController.OnStageChanged -= HandleStageChanged;

        motion = null;

        if (hand != null && restCaptured)
            hand.position = restPosition;
    }

    private void HandleStageChanged(InnerWireStage stage)
    {
        if (hand == null)
            return;

        if (stage == InnerWireStage.Flattening)
        {
            Press();
            return;
        }

        // Tahap apa pun selain Flattening : tangan tidak punya urusan lagi.
        Idle();
    }

    /// <summary>Turun ke titik tekan, menekan, lalu naik — selama Flattening.</summary>
    private void Press()
    {
        if (hand == null)
            return;

        if (!restCaptured)
        {
            restPosition = hand.position;
            restCaptured = true;
        }

        hand.gameObject.SetActive(true);

        if (motion != null)
            StopCoroutine(motion);

        motion = StartCoroutine(PressRoutine());
    }

    /// <summary>Kembali ke pose istirahat, dan disembunyikan bila diminta.</summary>
    private void Idle()
    {
        if (hand == null)
            return;

        if (motion != null)
        {
            StopCoroutine(motion);
            motion = null;
        }

        if (restCaptured)
            hand.position = restPosition;

        if (hideWhenIdle)
            hand.gameObject.SetActive(false);
    }

    /// <summary>
    /// Durasi diambil dari <see cref="InnerWireStageController.FlattenDuration"/>,
    /// jadi tangan selesai bersamaan dengan wire — tanpa angka durasi kedua yang
    /// bisa berbeda.
    /// </summary>
    private System.Collections.IEnumerator PressRoutine()
    {
        float duration = stageController != null ? stageController.FlattenDuration : 1.2f;

        Vector3 lift = liftDirection.sqrMagnitude > 1e-10f
            ? liftDirection.normalized
            : Vector3.up;

        Vector3 down = pressAnchor != null ? pressAnchor.position : restPosition;
        Vector3 up = down + lift * hoverHeight;

        hand.position = up;

        if (duration <= 0f)
        {
            hand.position = down;
            motion = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            // Turun pada paruh pertama, menekan (tertahan di bawah) lalu naik di
            // paruh kedua. Sin memberi gerakan halus tanpa kurva tambahan.
            float depth = Mathf.Sin(t * Mathf.PI);

            hand.position = Vector3.Lerp(up, down, depth);

            yield return null;
        }

        hand.position = up;
        motion = null;
    }

    private void OnValidate()
    {
        hoverHeight = Mathf.Max(0f, hoverHeight);
    }

#if UNITY_EDITOR
    [ContextMenu("Hand/Log Status")]
    private void LogStatus()
    {
        Debug.Log(
            $"[{nameof(FlattenHandView)}] " +
            $"stageController={(stageController != null ? stageController.name : "belum diassign")}  " +
            $"stage={(stageController != null ? stageController.Stage.ToString() : "?")}\n" +
            $"  hand={(hand != null ? hand.name : "KOSONG — tanpa visual, flatten tetap jalan")}  " +
            $"pressAnchor={(pressAnchor != null ? pressAnchor.name : "kosong — pakai pose awal tangan")}\n" +
            $"  restPosition={(restCaptured ? restPosition.ToString("F4") : "belum tercatat")}  " +
            $"hoverHeight={hoverHeight:F4} m",
            this);
    }
#endif
}
