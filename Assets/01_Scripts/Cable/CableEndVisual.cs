using UnityEngine;

/// <summary>
/// Menempelkan visual endpoint (CableBeforeTrim / AfterCut) ke ujung kabel,
/// memakai attachment frame yang dipublikasikan CablePoint.
///
/// Murni presentasi : tidak tahu ACC, tidak menyentuh state kabel, tidak
/// mengandung logic RJ45/crimping/tester. Dependency tetap satu arah —
/// komponen ini membaca CablePoint, dan CablePoint tidak tahu ia ada.
///
/// Alasan komponen ini perlu ada : pivot model yang sudah benar hanya
/// menyelesaikan *posisi*. Yang membuat sambungan tetap menyatu saat kabel
/// diputar adalah orientasi — sumbu panjang model harus disejajarkan dengan
/// arah keluar kabel di ujung tersebut.
/// </summary>
public class CableEndVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CablePoint cablePoint;
    [Tooltip("Root visual yang ditempel (mis. CableBeforeTrimfbx). " +
             "Kosong = Transform komponen ini sendiri.")]
    [SerializeField] private Transform visual;

    [Header("Model Axis")]
    [Tooltip("Sumbu panjang model dalam ruang lokalnya, arah menjauh dari " +
             "sambungan. Model FBX ini panjang pada +Z, jadi default +Z.")]
    [SerializeField] private Vector3 modelForwardAxis = Vector3.forward;
    [Tooltip("Sumbu 'atas' model, dipakai agar rotasi di sekitar sumbu panjang " +
             "tidak berputar acak.")]
    [SerializeField] private Vector3 modelUpAxis = Vector3.up;

    [Header("Junction")]
    [Tooltip("Koreksi sambungan dalam ATTACHMENT FRAME endpoint (meter), bukan " +
             "world space : x/y = geser menyamping untuk mencocokkan sumbu " +
             "geometry model dengan sumbu kabel, z = geser sepanjang arah keluar " +
             "(negatif = masuk ke badan kabel). Karena offset ini ikut diputar " +
             "bersama frame endpoint, hasilnya tetap benar saat kabel diputar.")]
    [SerializeField] private Vector3 localJunctionOffset = Vector3.zero;

    private Quaternion axisCorrection = Quaternion.identity;

    private void Awake()
    {
        if (visual == null)
            visual = transform;

        // Rotasi yang memutar sumbu panjang model ke +Z (forward) dan sumbu
        // atas model ke +Y. Inversnya dipakai saat menyusun rotasi akhir.
        axisCorrection = Quaternion.Inverse(
            Quaternion.LookRotation(
                modelForwardAxis.sqrMagnitude > 1e-10f ? modelForwardAxis.normalized : Vector3.forward,
                modelUpAxis.sqrMagnitude > 1e-10f ? modelUpAxis.normalized : Vector3.up));
    }

    private void OnEnable()
    {
        if (cablePoint == null)
        {
            Debug.LogWarning($"[{nameof(CableEndVisual)}] CablePoint belum diassign.", this);
            return;
        }

        cablePoint.PositionChanged += OnEndpointChanged;
        cablePoint.AttachmentChanged += OnEndpointChanged;
    }

    private void OnDisable()
    {
        if (cablePoint == null)
            return;

        cablePoint.PositionChanged -= OnEndpointChanged;
        cablePoint.AttachmentChanged -= OnEndpointChanged;
    }

    // Event tidak fire untuk kondisi awal. LateUpdate tidak dipakai supaya
    // tidak ada kerja per-frame; cukup satu kali di Start setelah
    // CableController melakukan sinkronisasi awal.
    private void Start() => Apply();

    private void OnEndpointChanged(CablePoint _) => Apply();

    private void Apply()
    {
        if (cablePoint == null || visual == null)
            return;

        // Frame endpoint : rotasi mengikuti arah keluar kabel, lalu dikoreksi
        // ke sumbu model. Offset dinyatakan di dalam frame ini (bukan world),
        // jadi ikut berputar bersama kabel.
        Quaternion frame = cablePoint.AttachmentRotation;

        visual.SetPositionAndRotation(
            cablePoint.Position + frame * localJunctionOffset,
            frame * axisCorrection);
    }

#if UNITY_EDITOR
    // Dipakai untuk menyetel junctionOffset dari Inspector tanpa masuk Play Mode.
    private void OnValidate()
    {
        if (visual == null)
            visual = transform;

        axisCorrection = Quaternion.Inverse(
            Quaternion.LookRotation(
                modelForwardAxis.sqrMagnitude > 1e-10f ? modelForwardAxis.normalized : Vector3.forward,
                modelUpAxis.sqrMagnitude > 1e-10f ? modelUpAxis.normalized : Vector3.up));
    }

    [ContextMenu("Snap Now")]
    private void SnapNow()
    {
        OnValidate();
        Apply();

        Debug.Log(
            $"[{nameof(CableEndVisual)}] endpoint={cablePoint?.Position.ToString("F4")}  " +
            $"direction={cablePoint?.AttachmentDirection.ToString("F4")}  " +
            $"visualPos={visual?.position.ToString("F4")}",
            this);
    }
#endif
}
