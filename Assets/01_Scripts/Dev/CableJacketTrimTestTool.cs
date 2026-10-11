using UnityEngine;

/// <summary>
/// TEST HARNESS SAJA — bukan implementasi crimping final.
///
/// Tujuan : membuktikan di runtime bahwa memakai Crimping Tool pada ujung A
/// benar-benar menghilangkan jacket sepanjang Trim Length A, bukan sekadar
/// lolos compile.
///
/// Sengaja tidak menambah sistem apa pun : trim tetap milik
/// <see cref="CableJacketTrim"/>, dispatch klik tetap
/// ToolInput -> ToolManager -> ITool, resolusi target tetap HitResolver.
/// Tidak ada physics, trigger, joint, RJ45, maupun tester. Endpoint B ditolak.
///
/// Hapus file ini saat crimping final dikerjakan.
/// </summary>
public class CableJacketTrimTestTool : MonoBehaviour, ITool
{
    [Header("Test Value")]
    [Tooltip("Panjang jacket yang dicoba dipotong di ujung A (meter).")]
    [SerializeField] private float testTrimLengthA = 0.05f;

    [Header("References (opsional)")]
    [Tooltip("Kosong = dicari dari collider yang kena raycast lewat HitResolver. " +
             "Isi hanya kalau mau memakai ContextMenu sebelum ada klik.")]
    [SerializeField] private CableJacketTrim jacketTrim;
    [Tooltip("Kosong = diambil dari raycast. Dipakai untuk log posisi endpoint A.")]
    [SerializeField] private CablePoint pointA;

    public ToolState Id => ToolState.CrimpingTool;

    // Diagnostik : tiga penyebab paling sering "klik tidak ngapa-ngapain" —
    // InputMode bukan UseTool, tool aktif bukan CrimpingTool, atau komponen ini
    // tidak terdaftar karena bukan child dari toolsRoot ToolManager.
    private void Start()
    {
        ToolManager manager = GetComponentInParent<ToolManager>();

        Debug.Log(
            $"[{nameof(CableJacketTrimTestTool)}] READY  " +
            $"inputMode={InputModeService.Current} (butuh UseTool)  " +
            $"toolManager={(manager != null ? manager.name : "TIDAK KETEMU di parent")}  " +
            $"currentTool={(manager != null ? manager.CurrentToolState.ToString() : "-")} " +
            $"(butuh CrimpingTool)  " +
            $"registered={(manager != null && ReferenceEquals(manager.CurrentTool, this))}",
            this);
    }

    // Target terakhir yang benar-benar di-trim, supaya Reset tetap jalan walau
    // Inspector tidak diisi.
    private CableJacketTrim lastTarget;
    private CablePoint lastPointA;

    // Kondisi awal jacket, direkam sebelum trim pertama.
    private float baselineTrim;
    private bool baselineCaptured;

    public bool Execute(RaycastHit hit)
    {
        CablePoint point = HitResolver.Resolve<CablePoint>(hit);

        // Log tanpa syarat : membedakan "klik tidak pernah sampai" (tidak ada
        // baris ini sama sekali) dari "sampai tapi ditolak".
        Debug.Log(
            $"[{nameof(CableJacketTrimTestTool)}] HIT '{hit.collider.name}'  " +
            $"cablePoint={(point != null ? point.name : "null")}  " +
            $"endpoint={(point != null ? point.Endpoint.ToString() : "-")}",
            this);

        // Endpoint B memang harus ditolak.
        if (point == null || point.Endpoint != CableEndpoint.A)
            return false;

        CableJacketTrim trim = jacketTrim != null
            ? jacketTrim
            : HitResolver.Resolve<CableJacketTrim>(hit);

        if (trim == null)
        {
            Debug.LogWarning(
                $"[{nameof(CableJacketTrimTestTool)}] CableJacketTrim tidak ditemukan dari " +
                $"'{hit.collider.name}'. Assign manual di Inspector.", this);
            return false;
        }

        return TrimA(trim, point, testTrimLengthA);
    }

    /// <summary>
    /// Menjalankan trim A lalu melaporkan bukti bahwa mesh jacket memang
    /// terpotong. Boleh dipanggil dari UI Button / UnityEvent juga.
    /// </summary>
    public bool TrimA(CableJacketTrim trim, CablePoint endpointA, float requested)
    {
        if (trim == null)
            return false;

        if (!baselineCaptured || lastTarget != trim)
        {
            baselineTrim = trim.TrimLengthA;
            baselineCaptured = true;
        }

        lastTarget = trim;

        if (endpointA != null)
            lastPointA = endpointA;

        Mesh mesh = MeshOf(trim);

        if (mesh == null)
        {
            Debug.LogWarning(
                $"[{nameof(CableJacketTrimTestTool)}] MeshFilter/sharedMesh kosong di " +
                $"'{trim.name}'. Cek ACC_Cable.showMeshes.", trim);
            return false;
        }

        int indicesBefore = (int)mesh.GetIndexCount(0);
        Vector3 boundsBefore = mesh.bounds.size;

        // Setter hanya Apply() saat nilainya berubah; Apply() dipanggil lagi
        // supaya pemasangan trim terjamin walau nilainya sama (klik kedua).
        trim.TrimLengthA = requested;
        trim.Apply();

        int indicesAfter = (int)mesh.GetIndexCount(0);
        Vector3 boundsAfter = mesh.bounds.size;

        // Bukti absolut, bukan hanya "beda dari call sebelumnya" : jumlah index
        // yang tergambar harus lebih kecil dari mesh penuh. Index per band
        // diturunkan dari band yang tersisa, jadi tidak perlu tahu
        // crossSegments milik ACC.
        int keptBands = trim.RingCount - 1 - trim.BoundaryRing;
        int fullIndices = keptBands > 0
            ? (trim.RingCount - 1) * (indicesAfter / keptBands)
            : indicesAfter;

        bool jacketShortened = indicesAfter < fullIndices;
        bool changedThisCall = indicesAfter != indicesBefore || boundsAfter != boundsBefore;

        Vector3 posA = endpointA != null
            ? endpointA.Position
            : (pointA != null ? pointA.Position : Vector3.zero);

        Debug.Log(
            $"[{nameof(CableJacketTrimTestTool)}] TRIM A  " +
            $"requested={requested:F4}  achieved={trim.AchievedTrim:F4}  " +
            $"endpointA={posA:F4}  " +
            $"rings={trim.RingCount}  boundaryRing={trim.BoundaryRing}  " +
            $"totalArc={trim.TotalArc:F4}  " +
            $"indices={indicesBefore}->{indicesAfter}/{fullIndices}  " +
            $"bounds={boundsBefore:F4}->{boundsAfter:F4}  " +
            $"jacketShortened={jacketShortened}  changedThisCall={changedThisCall}",
            trim);

        if (!jacketShortened)
            Debug.LogWarning(
                $"[{nameof(CableJacketTrimTestTool)}] Jacket TIDAK terpotong — " +
                $"nilai trim mungkin 0, atau mesh ACC belum siap.", trim);

        return jacketShortened;
    }

    /// <summary>
    /// Mengembalikan jacket ke kondisi sebelum test, tanpa reload scene.
    /// Aman dipanggil berkali-kali.
    /// </summary>
    public void ResetTrim()
    {
        CableJacketTrim trim = jacketTrim != null ? jacketTrim : lastTarget;

        if (trim == null)
        {
            Debug.LogWarning(
                $"[{nameof(CableJacketTrimTestTool)}] Belum ada target. Klik ujung A " +
                $"dulu, atau assign CableJacketTrim di Inspector.", this);
            return;
        }

        trim.TrimLengthA = baselineCaptured ? baselineTrim : 0f;
        trim.Apply();

        Mesh mesh = MeshOf(trim);

        Debug.Log(
            $"[{nameof(CableJacketTrimTestTool)}] RESET  " +
            $"trimLengthA={trim.TrimLengthA:F4}  achieved={trim.AchievedTrim:F4}  " +
            $"indices={(mesh != null ? (int)mesh.GetIndexCount(0) : -1)}",
            trim);
    }

    // Mesh ACC ada di GameObject yang sama dengan CableJacketTrim.
    private static Mesh MeshOf(CableJacketTrim trim)
    {
        MeshFilter filter = trim.GetComponent<MeshFilter>();

        return filter != null ? filter.sharedMesh : null;
    }

#if UNITY_EDITOR
    [ContextMenu("Test/Trim A Now")]
    private void TestTrimANow()
    {
        CableJacketTrim trim = jacketTrim != null ? jacketTrim : lastTarget;

        if (trim == null)
        {
            Debug.LogWarning(
                $"[{nameof(CableJacketTrimTestTool)}] Assign CableJacketTrim untuk " +
                $"memakai ContextMenu tanpa klik.", this);
            return;
        }

        TrimA(trim, pointA != null ? pointA : lastPointA, testTrimLengthA);
    }

    [ContextMenu("Test/Reset Trim A")]
    private void TestResetTrim() => ResetTrim();
#endif
}
