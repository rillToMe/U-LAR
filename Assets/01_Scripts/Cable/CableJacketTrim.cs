using System;
using UnityEngine;
using UnityEngine.Rendering;
using NOT_Lonely;

/// <summary>
/// Memendekkan tampilan outer jacket ACC di kedua ujung kabel, tanpa mengubah
/// endpoint fisik, panjang kabel, maupun kurva.
///
/// Murni presentasi. Tidak tahu RJ45, inner wire, crimping, stripping, maupun
/// input — <see cref="TrimLengthA"/> dan <see cref="TrimLengthB"/> adalah
/// parameter visual.
///
/// Cara kerjanya : ACC tetap membangun mesh penuh dari endpoint asli, lalu
/// komponen ini hanya (a) menulis ulang index sehingga band di dekat ujung tidak
/// digambar, dan (b) menggeser ring batas ke titik potong eksak. Vertex ACC
/// tidak pernah dihapus, jadi ini bukan mesh cutting : setiap ReDraw() menulis
/// ulang mesh dari nol dan trim tinggal dipasang lagi.
///
/// Ring 0 selalu ada di ujung A dan ring terakhir di ujung B, karena
/// CableController menulis cableStart dari PointA dan cableEnd dari PointB. Jadi
/// trim A memakan blok index dari depan dan trim B dari belakang.
/// </summary>
public class CableJacketTrim : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ACC_Cable cable;

    [Header("Trim")]
    [Tooltip("Panjang jacket yang disembunyikan di ujung A, diukur sepanjang " +
             "kurva kabel (meter). 0 = ujung A tampil normal.")]
    [SerializeField] private float trimLengthA = 0f;
    [Tooltip("Panjang jacket yang disembunyikan di ujung B, diukur sepanjang " +
             "kurva kabel (meter). 0 = ujung B tampil normal. Bersama trim A " +
             "dibatasi agar minimal satu band jacket selalu tersisa.")]
    [SerializeField] private float trimLengthB = 0f;

    /// <summary>
    /// Panjang trim di ujung A. Menulisnya langsung memicu Apply(), jadi
    /// pemanggil tidak perlu tahu soal mesh.
    /// </summary>
    public float TrimLengthA
    {
        get => trimLengthA;
        set
        {
            float clamped = Mathf.Max(0f, value);

            if (clamped == trimLengthA)
                return;

            trimLengthA = clamped;

            Apply();
        }
    }

    /// <summary>Panjang trim di ujung B. Simetris dengan <see cref="TrimLengthA"/>.</summary>
    public float TrimLengthB
    {
        get => trimLengthB;
        set
        {
            float clamped = Mathf.Max(0f, value);

            if (clamped == trimLengthB)
                return;

            trimLengthB = clamped;

            Apply();
        }
    }


    private MeshFilter filter;

    // Dipakai ulang antar frame supaya rebuild saat drag tidak mengalokasi.
    private Vector3[] centers = Array.Empty<Vector3>();
    private float[] arc = Array.Empty<float>();
    private Vector3[] verts = Array.Empty<Vector3>();
    private int[] indices = Array.Empty<int>();

    // Hasil perhitungan terakhir, hanya untuk verifikasi manual. Properti,
    // bukan field privat, supaya tidak menjadi "assigned but never used" di
    // build player (di sana blok diagnostik UNITY_EDITOR ikut hilang).
    public int RingCount { get; private set; }

    /// <summary>
    /// Band jacket yang benar-benar punya index di mesh ACC.
    /// </summary>
    public int BandCount { get; private set; }

    /// <summary>Band pertama yang digambar. 0 = ujung A utuh.</summary>
    public int BoundaryRing { get; private set; }

    /// <summary>
    /// Band terakhir yang digambar (eksklusif). Sama dengan
    /// <see cref="BandCount"/> saat ujung B utuh.
    /// </summary>
    public int BoundaryRingB { get; private set; }

    public float AchievedTrim { get; private set; }

    /// <summary>Trim yang benar-benar tercapai di ujung B.</summary>
    public float AchievedTrimB { get; private set; }

    public float TotalArc { get; private set; }

    // ACC membangun mesh di OnEnable-nya sendiri dan urutan OnEnable antar
    // komponen tidak dijamin, jadi pemasangan pertama dilakukan di Start.
    private void Start() => Apply();

    /// <summary>
    /// Memasang ulang trim pada mesh ACC yang sedang ada. Idempotent : boleh
    /// dipanggil berkali-kali tanpa ReDraw di antaranya, karena vertex dan index
    /// selalu ditulis ulang dari array ACC yang masih utuh.
    ///
    /// Index ditulis eksplisit lewat <see cref="Mesh.SetIndices"/>, BUKAN dengan
    /// menggeser indexStart pada SubMeshDescriptor. Alasannya terukur : dengan
    /// SetSubMesh(indexStart: boundary * indicesPerBand, …) Unity melaporkan
    /// indexStart yang benar tapi tetap menggambar mulai dari index ke-0 —
    /// indexCount jadi bekerja sebagai pemotongan dari BELAKANG. Efeknya band di
    /// ujung B yang hilang dan jacket di ujung A tetap utuh, kebalikan dari yang
    /// dimaksud.
    /// </summary>
    public void Apply()
    {
        if (cable == null || !cable.showMeshes)
            return;

        if (filter == null)
            filter = cable.GetComponent<MeshFilter>();

        Mesh mesh = filter != null ? filter.sharedMesh : null;

        if (mesh == null)
            return;

        // Sumber kebenaran vertex : array milik ACC, belum tergeser trim.
        Vector3[] source = cable.meshVertices;

        if (source == null || source.Length == 0)
            return;

        int crossSegments = cable.radialSegments == 2 ? 2 : cable.radialSegments + 1;
        int ringCount = source.Length / crossSegments;

        // Butuh minimal dua band : satu dibuang, satu tersisa.
        if (ringCount < 3)
            return;

        // Rebuild() melewati c terakhir (c >= crossPoints.Length - 1) dan ring
        // p = 0, jadi satu band = (crossSegments - 1) quad = 6 index.
        int indicesPerBand = 6 * (crossSegments - 1);

        // Jumlah band dibaca dari index buffer ACC, bukan diturunkan dari
        // ringCount : Rebuild() mengalokasi tris berlebih lalu memadatkannya,
        // jadi panjang buffer adalah satu-satunya sumber yang pasti benar.
        int[] indexBuffer = cable.tris;

        if (indexBuffer == null || indexBuffer.Length < indicesPerBand)
            return;

        int bandCount = indexBuffer.Length / indicesPerBand;

        if (bandCount < 2)
            return;

        mesh.subMeshCount = 1;

        if (trimLengthA <= 0f && trimLengthB <= 0f)
        {
            mesh.SetVertices(source);
            mesh.SetIndices(indexBuffer, MeshTopology.Triangles, 0, true);

            RingCount = ringCount;
            BandCount = bandCount;
            BoundaryRing = 0;
            BoundaryRingB = bandCount;
            AchievedTrim = 0f;
            AchievedTrimB = 0f;
            TotalArc = 0f;

            return;
        }

        MeasureCenterline(source, crossSegments, ringCount);

        float totalArc = arc[bandCount];

        // Kedua trim dibatasi bersama supaya minimal satu band selalu tersisa.
        // Pembagian proporsional dipakai hanya saat gabungannya kelewatan, jadi
        // selama masih masuk, nilai yang diminta dipenuhi apa adanya.
        float maxCombined = arc[bandCount - 1];
        float requestedA = Mathf.Max(0f, trimLengthA);
        float requestedB = Mathf.Max(0f, trimLengthB);
        float combined = requestedA + requestedB;

        if (combined > maxCombined && combined > 1e-6f)
        {
            float scale = maxCombined / combined;
            requestedA *= scale;
            requestedB *= scale;
        }

        EnsureSize(ref verts, source.Length);
        Array.Copy(source, verts, source.Length);

        // --- Ujung A : diukur dari ring 0 maju. ---
        int boundaryA = 0;
        float achievedA = 0f;

        if (requestedA > 0f)
        {
            achievedA = SolveBoundary(requestedA, 0, bandCount, out boundaryA, out Vector3 cutA);

            Vector3 shiftA = cutA - centers[boundaryA];
            int ringBaseA = boundaryA * crossSegments;

            for (int c = 0; c < crossSegments; c++)
                verts[ringBaseA + c] += shiftA;
        }

        // --- Ujung B : diukur dari ring terakhir mundur. Arc diukur dari A,
        //     jadi jarak dari B = totalArc - arc[ring]. ---
        int boundaryB = bandCount;
        float achievedB = 0f;

        if (requestedB > 0f)
        {
            achievedB = SolveBoundaryFromEnd(
                requestedB, boundaryA, bandCount, totalArc, out boundaryB, out Vector3 cutB);

            Vector3 shiftB = cutB - centers[boundaryB];
            int ringBaseB = boundaryB * crossSegments;

            for (int c = 0; c < crossSegments; c++)
                verts[ringBaseB + c] += shiftB;
        }

        // Blok index yang disalin : boundaryA..boundaryB. Band k memakai ring k
        // dan k + 1, jadi membuang blok sebelum boundaryA menyembunyikan jacket
        // di ujung A, dan membuang blok setelah boundaryB menyembunyikan ujung B.
        // Index-nya benar-benar tidak ikut ditulis, jadi tidak ada
        // ketergantungan pada perlakuan indexStart.
        int keptBands = Mathf.Max(1, boundaryB - boundaryA);
        int keptIndices = keptBands * indicesPerBand;
        int copyFrom = cable.invert ? 0 : boundaryA * indicesPerBand;

        EnsureSize(ref indices, keptIndices);
        Array.Copy(indexBuffer, copyFrom, indices, 0, keptIndices);

        mesh.SetVertices(verts);
        mesh.SetIndices(indices, MeshTopology.Triangles, 0, true);

        RingCount = ringCount;
        BandCount = bandCount;
        BoundaryRing = boundaryA;
        BoundaryRingB = boundaryB;
        AchievedTrim = achievedA;
        AchievedTrimB = achievedB;
        TotalArc = totalArc;
    }

    /// <summary>
    /// Ring batas untuk trim yang diukur dari ring <paramref name="lower"/> maju.
    /// Ring batas digeser ke titik potong, jadi presisinya tidak dibulatkan ke
    /// spacing ring.
    /// </summary>
    private float SolveBoundary(
        float requested, int lower, int upper, out int boundary, out Vector3 cut)
    {
        float trim = Mathf.Min(requested, arc[upper - 1] - arc[lower]);
        float target = arc[lower] + trim;

        int segment = lower;
        while (segment < upper - 1 && arc[segment + 1] < target)
            segment++;

        float span = arc[segment + 1] - arc[segment];
        float t = span > 1e-6f ? (target - arc[segment]) / span : 0f;

        cut = Vector3.Lerp(centers[segment], centers[segment + 1], t);

        boundary = Mathf.Clamp(
            (target - arc[segment]) <= (arc[segment + 1] - target) ? segment : segment + 1,
            lower + 1,
            upper - 1);

        return trim;
    }

    /// <summary>
    /// Ring batas untuk trim yang diukur dari ring terakhir mundur. Simetris
    /// dengan <see cref="SolveBoundary"/>, hanya arah ukurnya dibalik.
    /// </summary>
    private float SolveBoundaryFromEnd(
        float requested, int lower, int upper, float totalArc, out int boundary, out Vector3 cut)
    {
        // Sisakan minimal satu band setelah boundary A.
        float available = totalArc - arc[lower + 1];
        float trim = Mathf.Min(requested, Mathf.Max(0f, available));
        float target = totalArc - trim;

        int segment = upper - 1;
        while (segment > lower && arc[segment] > target)
            segment--;

        float span = arc[segment + 1] - arc[segment];
        float t = span > 1e-6f ? (target - arc[segment]) / span : 0f;

        cut = Vector3.Lerp(centers[segment], centers[segment + 1], t);

        boundary = Mathf.Clamp(
            (target - arc[segment]) <= (arc[segment + 1] - target) ? segment : segment + 1,
            lower + 1,
            upper);

        return trim;
    }

    /// <summary>
    /// Mengisi <see cref="centers"/> dan <see cref="arc"/> dari vertex ACC.
    ///
    /// Pusat ring dihitung eksak, bukan diperkirakan : offset radial ACC adalah
    /// titik-titik pada lingkaran satuan berjarak sudut sama, dan jumlah seluruh
    /// akar satuan itu nol. Vertex terakhir tiap ring (duplikat vertex pertama,
    /// hanya beda UV) dikecualikan agar jumlahnya benar-benar nol.
    /// </summary>
    private void MeasureCenterline(Vector3[] source, int crossSegments, int ringCount)
    {
        int centerCount = cable.radialSegments == 2 ? crossSegments : crossSegments - 1;

        EnsureSize(ref centers, ringCount);
        EnsureSize(ref arc, ringCount);

        for (int p = 0; p < ringCount; p++)
        {
            Vector3 sum = Vector3.zero;
            int ringBase = p * crossSegments;

            for (int c = 0; c < centerCount; c++)
                sum += source[ringBase + c];

            centers[p] = sum / centerCount;

            arc[p] = p == 0
                ? 0f
                : arc[p - 1] + Vector3.Distance(centers[p - 1], centers[p]);
        }
    }

    private static void EnsureSize<T>(ref T[] array, int size)
    {
        if (array == null || array.Length != size)
            array = new T[size];
    }

    private void OnValidate()
    {
        trimLengthA = Mathf.Max(0f, trimLengthA);
        trimLengthB = Mathf.Max(0f, trimLengthB);

        // Supaya trim bisa disetel & dilihat dari Inspector tanpa masuk Play
        // Mode. ACC ber-ExecuteInEditMode, jadi mesh-nya sudah ada.
        Apply();
    }

#if UNITY_EDITOR
    [ContextMenu("Log Trim Status")]
    private void LogTrimStatus()
    {
        Apply();

        if (RingCount == 0)
        {
            Debug.LogWarning(
                $"[{nameof(CableJacketTrim)}] Mesh ACC belum siap atau reference kosong.",
                this);
            return;
        }

        float spacing = RingCount > 1 ? TotalArc / (RingCount - 1) : 0f;

        Debug.Log(
            $"[{nameof(CableJacketTrim)}] rings={RingCount}  bands={BandCount}  " +
            $"ringSpacing={spacing:F4}  totalArc={TotalArc:F4}  " +
            $"A: requested={trimLengthA:F4} achieved={AchievedTrim:F4} boundary={BoundaryRing}  " +
            $"B: requested={trimLengthB:F4} achieved={AchievedTrimB:F4} boundary={BoundaryRingB}",
            this);
    }

    /// <summary>
    /// Memeriksa kedua ujung sekaligus : trim yang dicapai harus sama dengan yang
    /// diminta (bukan dibulatkan ke ring), band yang tergambar harus persis
    /// boundaryA..boundaryB, dan index pertama/terakhir yang benar-benar digambar
    /// harus cocok dengan blok yang dimaksud.
    ///
    /// Index diperiksa lewat GetIndices(0), bukan lewat SubMeshDescriptor :
    /// descriptor pernah melaporkan indexStart yang benar sementara Unity tetap
    /// menggambar dari index ke-0, dan justru itu bug yang lolos dari pemeriksaan
    /// versi sebelumnya.
    /// </summary>
    [ContextMenu("Self Check")]
    private void SelfCheck()
    {
        if (cable == null)
        {
            Debug.LogWarning($"[{nameof(CableJacketTrim)}] ACC_Cable belum diassign.", this);
            return;
        }

        float restoreA = trimLengthA;
        float restoreB = trimLengthB;
        bool pass = true;

        // Baseline : mesh penuh.
        trimLengthA = 0f;
        trimLengthB = 0f;
        Apply();

        int crossSegments = cable.radialSegments == 2 ? 2 : cable.radialSegments + 1;
        int indicesPerBand = 6 * (crossSegments - 1);

        if (cable.tris == null || cable.tris.Length < indicesPerBand)
        {
            Debug.LogWarning($"[{nameof(CableJacketTrim)}] Index buffer ACC belum siap.", this);
            return;
        }

        int bandCount = cable.tris.Length / indicesPerBand;
        int fullIndices = bandCount * indicesPerBand;

        Mesh mesh = filter != null ? filter.sharedMesh : null;

        if (mesh == null)
        {
            Debug.LogWarning($"[{nameof(CableJacketTrim)}] Mesh belum ada.", this);
            return;
        }

        bool baselineOk = mesh.GetIndexCount(0) == fullIndices;
        pass &= baselineOk;
        Debug.Log(
            $"trim A=0 B=0  bands={bandCount}  " +
            $"indices={mesh.GetIndexCount(0)}/{fullIndices}  ok={baselineOk}");

        // (trim A, trim B) : hanya A, hanya B, lalu keduanya bersamaan.
        (float a, float b)[] cases =
        {
            (0.05f, 0f),
            (0.25f, 0f),
            (0f, 0.05f),
            (0f, 0.25f),
            (0.05f, 0.05f),
            (0.2f, 0.3f),
        };

        foreach ((float a, float b) in cases)
        {
            trimLengthA = a;
            trimLengthB = b;
            Apply();

            // Trim yang diminta harus dipenuhi apa adanya selama gabungannya
            // masih masuk — tidak ada pembulatan ke spacing ring.
            bool exactA = a <= 0f || Mathf.Abs(AchievedTrim - a) <= 1e-4f;
            bool exactB = b <= 0f || Mathf.Abs(AchievedTrimB - b) <= 1e-4f;

            // Selalu ada sisa jacket yang tergambar.
            bool inRange = BoundaryRing < BoundaryRingB &&
                           BoundaryRing >= 0 && BoundaryRingB <= bandCount;

            int expectedBands = BoundaryRingB - BoundaryRing;
            bool indicesOk = mesh.GetIndexCount(0) == expectedBands * indicesPerBand;

            int[] drawn = mesh.GetIndices(0);

            // Index pertama = blok boundaryA, index terakhir = blok boundaryB-1.
            // Dua-duanya diperiksa supaya ujung A dan B masing-masing terbukti.
            int expectFirst = cable.tris[BoundaryRing * indicesPerBand];
            int expectLast = cable.tris[BoundaryRingB * indicesPerBand - 1];

            bool startOk = cable.invert || (drawn.Length > 0 && drawn[0] == expectFirst);
            bool endOk = cable.invert ||
                         (drawn.Length > 0 && drawn[drawn.Length - 1] == expectLast);

            bool ok = exactA && exactB && inRange && indicesOk && startOk && endOk;
            pass &= ok;

            Debug.Log(
                $"A={a:F3}->{AchievedTrim:F4} B={b:F3}->{AchievedTrimB:F4}  " +
                $"bands={BoundaryRing}..{BoundaryRingB}/{bandCount}  " +
                $"exactA={exactA} exactB={exactB} inRange={inRange} indices={indicesOk}  " +
                $"first={drawn[0]}/{expectFirst}{(startOk ? "" : " SALAH")}  " +
                $"last={drawn[drawn.Length - 1]}/{expectLast}{(endOk ? "" : " SALAH")}  ok={ok}");
        }

        // Kembali ke 0 harus memulihkan mesh penuh.
        trimLengthA = 0f;
        trimLengthB = 0f;
        Apply();

        bool restoredOk = mesh.GetIndexCount(0) == fullIndices;
        pass &= restoredOk;
        Debug.Log($"restore A=0 B=0  indices={mesh.GetIndexCount(0)}/{fullIndices}  ok={restoredOk}");

        trimLengthA = restoreA;
        trimLengthB = restoreB;
        Apply();

        Debug.Log($"[{nameof(CableJacketTrim)}] Self check : {(pass ? "PASS" : "FAIL")}", this);
    }
#endif
}
