using UnityEngine;

/// <summary>
/// Aturan pemasangan SATU konektor RJ45 ke SATU endpoint kabel. Ditempatkan
/// di ROOT konektor (objek yang dipegang pemain), bukan di endpoint —
/// <see cref="TryInsert"/> memasang transform komponen ini ke frame
/// attachment endpoint target.
///
/// Area pemasangan dan titik snap keduanya diturunkan dari API attachment
/// <see cref="CablePoint"/> yang sudah ada (Position + AttachmentRotation) —
/// tidak ada collider/handle/mesh baru di endpoint, tidak ada sumbu world
/// yang di-hardcode, jadi tetap benar saat kabel diputar.
///
/// Tidak menyentuh inner wire sama sekali : urutan wire hasil sorting pemain
/// (benar maupun salah) dibiarkan apa adanya — penilaian T568A/B milik LAN
/// Tester nanti, bukan pemasangan ini.
/// </summary>
public class RJ45Controller : MonoBehaviour
{
    [Header("Endpoint Target")]
    [SerializeField] private CablePoint cablePoint;
    [SerializeField] private RJ45Point rj45Point;
    [SerializeField] private PracticeManager practiceManager;
    [Tooltip("Pemilik tahap endpoint ini. Kosong = tanpa gate tahap (endpoint yang " +
             "belum punya tahap, mis. B). Wajib diisi untuk endpoint yang punya " +
             "tahap sorting.")]
    [SerializeField] private InnerWireStageController stageController;
    [Tooltip("Pemilik tool aktif. Setelah insert berhasil, tool aktif digeser ke " +
             "CrimpingTool supaya langkah berikutnya langsung tersedia. Kosong = " +
             "pergeseran tool dilewati (dengan warning).")]
    [SerializeField] private ToolManager toolManager;

    [Header("Insertion")]
    [Tooltip("Radius area pemasangan (meter) di sekitar titik duduk konektor. " +
             "Konektor harus masuk radius ini sebelum bisa menempel.")]
    [SerializeField] private float insertionRadius = 0.05f;
    [Tooltip("Offset titik duduk konektor relatif frame endpoint, di ruang " +
             "attachment (+Z = keluar dari ujung kabel). Inilah posisi konektor " +
             "setelah menempel.")]
    [SerializeField] private Vector3 mountOffset = new Vector3(0f, 0f, 0.03f);
    [Tooltip("Rotasi tambahan konektor (Euler, di ruang attachment) bila mesh " +
             "konektor perlu koreksi orientasi terhadap frame endpoint.")]
    [SerializeField] private Vector3 mountRotation = Vector3.zero;

    /// <summary>Akses read-only state konektor — dipakai RJ45Tool saat grab.</summary>
    public RJ45Point Point => rj45Point;

    /// <summary>
    /// Akses read-only endpoint pasangan konektor ini. Dipakai pemilik aturan
    /// berikutnya (crimping) supaya pasangan konektor-endpoint tetap satu
    /// sumber kebenaran dan tidak perlu di-wire ulang di Inspector.
    /// </summary>
    public CablePoint Cable => cablePoint;

    /// <summary>
    /// Titik duduk konektor di frame endpoint — satu-satunya acuan untuk area
    /// pemasangan DAN untuk snap : keduanya memakai titik yang sama supaya
    /// "masuk zona" selalu berarti "menempel di posisi yang benar".
    /// </summary>
    public Vector3 AttachPosition =>
        cablePoint.Position + cablePoint.AttachmentRotation * mountOffset;

    /// <summary>
    /// Apakah <paramref name="rj45Position"/> berada di dalam area pemasangan
    /// endpoint target. Murni jarak ke <see cref="AttachPosition"/> — tidak
    /// ada raycast, tidak ada collider tambahan.
    /// </summary>
    public bool IsInInsertionArea(Vector3 rj45Position)
    {
        if (cablePoint == null)
            return false;

        return Vector3.Distance(rj45Position, AttachPosition) <= insertionRadius;
    }

    private void OnEnable()
    {
        if (cablePoint == null)
        {
            Debug.LogWarning(
                $"{name} : CablePoint belum diassign — konektor terpasang tidak " +
                "akan mengikuti pergerakan ujung kabel.",
                this);
            return;
        }

        // Dua event, bukan satu : PositionChanged fire saat ujung digeser,
        // AttachmentChanged saat CableController menulis ulang frame attachment
        // (arah keluar kabel berubah karena ujung lain ikut bergerak/tertekuk).
        // Keduanya berarti "frame endpoint pindah", dan urutan fire keduanya
        // tidak dijamin — menempelkan ulang di keduanya membuat hasil akhir
        // selalu memakai frame terbaru, bukan frame sisa frame sebelumnya.
        cablePoint.PositionChanged += OnEndpointMoved;
        cablePoint.AttachmentChanged += OnEndpointMoved;
    }

    private void OnDisable()
    {
        if (cablePoint == null)
            return;

        cablePoint.PositionChanged -= OnEndpointMoved;
        cablePoint.AttachmentChanged -= OnEndpointMoved;
    }

    // Event tidak pernah fire untuk kondisi awal, jadi konektor yang sudah
    // terserialisasi sebagai terpasang disamakan sekali di sini (pola yang sama
    // dipakai CableEndVisual).
    private void Start() => FollowEndpoint();

    private void OnEndpointMoved(CablePoint _) => FollowEndpoint();

    /// <summary>
    /// Menempelkan ulang konektor yang SUDAH terpasang setiap frame endpoint
    /// berpindah. Tanpa ini konektor hanya benar pada saat insert : begitu ujung
    /// kabel digeser (mis. saat menyiapkan pengujian LAN Tester) kabel bergerak
    /// sementara konektornya tertinggal di posisi lama.
    ///
    /// Konektor ber-state None sengaja dilewati — ia masih milik pemain, belum
    /// terpasang ke endpoint mana pun.
    /// </summary>
    private void FollowEndpoint()
    {
        if (cablePoint == null || rj45Point == null)
            return;

        if (rj45Point.State == RJ45State.None)
            return;

        ApplyMountTransform();
    }

    /// <summary>
    /// Satu-satunya tempat konektor ditempelkan ke frame endpoint — dipakai
    /// saat insert DAN saat mengikuti pergerakan ujung kabel, supaya posisi
    /// duduknya tidak pernah dihitung dengan cara berbeda.
    /// </summary>
    private void ApplyMountTransform()
    {
        transform.SetPositionAndRotation(
            AttachPosition,
            cablePoint.AttachmentRotation * Quaternion.Euler(mountRotation));
    }

    /// <summary>
    /// Memasang konektor ke endpoint. Semua gate diperiksa dulu — state
    /// endpoint harus Stripped, konektor harus None, tahap sorting endpoint
    /// (bila ada controllernya) harus sudah dinyatakan selesai pemain.
    /// Tidak ada pemeriksaan urutan warna : susunan salah tetap boleh
    /// dipasang, penilaiannya menunggu LAN Tester.
    ///
    /// SUKSES = state maju + konektor di-snap ke frame endpoint. GAGAL =
    /// tidak ada yang berubah sama sekali.
    /// </summary>
    public bool TryInsert()
    {
        if (cablePoint == null || rj45Point == null)
            return false;

        if (cablePoint.State != CableState.Stripped)
            return false;

        if (rj45Point.State != RJ45State.None)
            return false;

        if (practiceManager != null &&
            !practiceManager.CanInsertRJ45(cablePoint))
            return false;

        // GATE TAHAP : RJ45 hanya boleh setelah pemain MENYATAKAN selesai
        // sorting (stage Completed) — bukan setelah urutan dianggap benar.
        if (stageController != null && !stageController.CanInsertConnector)
        {
            Debug.Log(
                $"{name} : insert ditolak — tahap {stageController.Stage}, " +
                $"sorting belum dinyatakan selesai oleh pemain.",
                this);
            return false;
        }

        rj45Point.State = RJ45State.Inserted;

        // Snap ke frame endpoint : posisi & rotasi dari API attachment
        // CablePoint. Controller ada di root konektor, jadi transform ini
        // adalah konektor itu sendiri. Inner wire tidak tersentuh.
        ApplyMountTransform();

        // Endpoint ikut maju : RJ45Mounted tercatat CableInstallationTracker.
        // CableController memetakan state ini ke trim yang sama seperti
        // Stripped (lihat TrimForState), jadi jacket tidak pulih.
        cablePoint.State = CableState.RJ45Mounted;

        practiceManager?.CompleteCurrentStep();

        // Alur lanjut : langkah berikutnya adalah crimping. Tool aktif digeser
        // lewat API ToolManager yang sudah ada. Kegagalan pergeseran (mis.
        // CrimpTool belum ada di scene) TIDAK membatalkan insert yang sudah
        // berhasil — hanya diperingatkan.
        if (toolManager != null)
        {
            toolManager.SetCurrentTool(ToolState.CrimpingTool);
        }
        else
        {
            Debug.LogWarning(
                $"{name} : ToolManager belum diassign — tool aktif tidak digeser " +
                $"ke {ToolState.CrimpingTool}.",
                this);
        }

        Debug.Log($"{name} : RJ45 Inserted di endpoint {cablePoint.Endpoint}");

        return true;
    }
#if UNITY_EDITOR
    [ContextMenu("Test/Try Insert")]
    private void TestTryInsert()
    {
        bool result = TryInsert();

        Debug.Log($"Result : {result}");
        Debug.Log($"Cable : {cablePoint.State}");
        Debug.Log($"RJ45 : {rj45Point.State}");
    }
#endif
}
