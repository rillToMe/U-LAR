using UnityEngine;
using NOT_Lonely;

/// <summary>
/// Adapter tunggal antara CablePoint dan Advanced Cable Creator, sekaligus
/// pemegang constraint fixed length.
///
/// Constraint diletakkan di sini, bukan di CablePoint maupun CableInput, karena
/// panjang kabel adalah properti *kabel* — butuh tahu kedua endpoint sekaligus.
/// CablePoint hanya tahu dirinya sendiri, dan CableInput tidak boleh memegang
/// aturan gameplay. Dependency tetap satu arah :
/// CableInput -> CablePoint -> CableController -> ACC.
/// </summary>
public class CableController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CablePoint pointA;
    [SerializeField] private CablePoint pointB;
    [SerializeField] private ACC_Cable cable;
    [Tooltip("Opsional. Dipasang ulang setelah tiap ReDraw karena ACC menulis " +
             "mesh dari nol. Kosong = jacket tampil penuh.")]
    [SerializeField] private CableJacketTrim jacketTrim;

    [Header("Fixed Length")]
    [Tooltip("Panjang fisik kabel (meter). Ditetapkan sekali di sini dan " +
             "menjadi batas maksimum jarak endpoint. Tidak pernah dihitung " +
             "dari jarak PointA-PointB.")]
    [SerializeField] private float cableLength = 5f;

    [Header("Stripping")]
    [Tooltip("Panjang jacket yang dilepas di ujung A saat CablePoint A masuk " +
             "state Stripped (meter). Murni visual : diteruskan ke " +
             "CableJacketTrim.TrimLengthA, tidak mengubah posisi endpoint, " +
             "cableStart/cableEnd, panjang kabel, maupun kurva.")]
    [SerializeField] private float stripLengthA = 0.05f;
    [Tooltip("Sama untuk ujung B, diteruskan ke CableJacketTrim.TrimLengthB.")]
    [SerializeField] private float stripLengthB = 0.05f;

    [Header("Diagnostics")]
    [Tooltip("Log tiap rebuild : apakah trim benar-benar terpasang lagi setelah " +
             "ReDraw. Ramai saat drag — nyalakan hanya saat melacak bug.")]
    [SerializeField] private bool logRebuilds = false;

    public float CableLength => cableLength;

    /// <summary>Sisa panjang yang belum terpakai. 0 = kabel sudah lurus.</summary>
    public float Slack => Mathf.Max(0f, cableLength - EndpointDistance);

    public float EndpointDistance =>
        HasReferences() ? Vector3.Distance(pointA.Position, pointB.Position) : 0f;

    public int RebuildCount { get; private set; }

    // Menulis CablePoint.Position memicu PositionChanged lagi. Flag ini
    // memutus rekursi saat posisi ditulis balik oleh constraint.
    private bool applyingConstraint;

    private bool refsWarned;

    private void OnEnable()
    {
        if (!HasReferences())
        {
            WarnMissingReferences();
            return;
        }

        pointA.PositionChanged += OnPositionChanged;
        pointB.PositionChanged += OnPositionChanged;

        // Trim jacket adalah konsekuensi visual dari state kabel. CablePoint
        // hanya menyiarkan state, dan CableJacketTrim hanya tahu mesh —
        // penerjemahnya di sini, satu-satunya tempat yang sudah memegang
        // keduanya. Kedua ujung memakai jalur yang sama.
        pointA.OnStateChanged += OnPointAStateChanged;
        pointB.OnStateChanged += OnPointBStateChanged;
    }

    private void OnDisable()
    {
        if (pointA != null)
        {
            pointA.PositionChanged -= OnPositionChanged;
            pointA.OnStateChanged -= OnPointAStateChanged;
        }

        if (pointB != null)
        {
            pointB.PositionChanged -= OnPositionChanged;
            pointB.OnStateChanged -= OnPointBStateChanged;
        }
    }

    /// <summary>
    /// Panjang jacket yang harus tersembunyi untuk state tertentu.
    /// Intact = jacket utuh; Stripped dan seterusnya = jacket sudah dilepas.
    /// Dipetakan begitu (bukan <c>== Stripped</c>) supaya RJ45Mounted/Connected
    /// nanti tidak diam-diam memulihkan jacket.
    /// </summary>
    private static float TrimForState(CableState state, float stripLength) =>
        state == CableState.Intact ? 0f : stripLength;

    private void OnPointAStateChanged(CableState state)
    {
        if (jacketTrim == null)
            return;

        // Setter CableJacketTrim sudah memanggil Apply() sendiri saat nilainya
        // berubah, jadi mesh langsung ikut tanpa menunggu rebuild berikutnya.
        jacketTrim.TrimLengthA = TrimForState(state, stripLengthA);

        Debug.Log(
            $"[{nameof(CableController)}] PointA state={state}  " +
            $"trimLengthA={jacketTrim.TrimLengthA:F4}  " +
            $"achieved={jacketTrim.AchievedTrim:F4}",
            this);
    }

    private void OnPointBStateChanged(CableState state)
    {
        if (jacketTrim == null)
            return;

        jacketTrim.TrimLengthB = TrimForState(state, stripLengthB);

        Debug.Log(
            $"[{nameof(CableController)}] PointB state={state}  " +
            $"trimLengthB={jacketTrim.TrimLengthB:F4}  " +
            $"achieved={jacketTrim.AchievedTrimB:F4}",
            this);
    }

    // Start, bukan OnEnable: ACC_Cable membuat mesh di OnEnable-nya sendiri dan
    // urutan OnEnable antar komponen tidak dijamin.
    private void Start()
    {
        if (!HasReferences())
        {
            WarnMissingReferences();
            return;
        }

        // Kalau jarak awal di scene sudah melebihi panjang kabel, PointB yang
        // ditarik masuk. PointA diperlakukan sebagai anchor default.
        Constrain(pointB, pointA);

        // Event tidak pernah fire untuk kondisi awal, jadi state yang sudah
        // terserialisasi di scene disamakan sekali di sini.
        if (jacketTrim != null)
        {
            jacketTrim.TrimLengthA = TrimForState(pointA.State, stripLengthA);
            jacketTrim.TrimLengthB = TrimForState(pointB.State, stripLengthB);
        }

        PushToCable();
    }

    private void OnPositionChanged(CablePoint moved)
    {
        if (applyingConstraint)
            return;

        if (!HasReferences())
        {
            WarnMissingReferences();
            return;
        }

        // Endpoint yang bergerak di-clamp terhadap endpoint lain sebagai anchor.
        // Simetris : A maupun B memakai jalur yang sama.
        CablePoint anchor =
            moved == pointA ? pointB :
            moved == pointB ? pointA : null;

        if (anchor == null)
            return;

        Constrain(moved, anchor);

        PushToCable();
    }

    /// <summary>
    /// Menarik <paramref name="moved"/> kembali ke permukaan bola berjari-jari
    /// cableLength di sekitar anchor bila sudah kelewat jauh. Arah target
    /// dipertahankan, jadi endpoint berhenti tepat di batas alih-alih melompat.
    /// </summary>
    private void Constrain(CablePoint moved, CablePoint anchor)
    {
        Vector3 clamped = ClampToLength(anchor.Position, moved.Position, cableLength);

        if (clamped == moved.Position)
            return;

        applyingConstraint = true;
        moved.Position = clamped;
        applyingConstraint = false;
    }

    /// <summary>
    /// Pure : titik terdekat ke <paramref name="target"/> yang jaraknya ke
    /// <paramref name="anchor"/> tidak melebihi <paramref name="length"/>.
    /// </summary>
    public static Vector3 ClampToLength(Vector3 anchor, Vector3 target, float length)
    {
        Vector3 offset = target - anchor;
        float distance = offset.magnitude;

        if (distance <= length || distance <= Mathf.Epsilon)
            return target;

        return anchor + (offset / distance) * length;
    }

    private void PushToCable()
    {
        cable.cableStart = pointA.Position;
        cable.cableEnd = pointB.Position;

        ApplySag(EndpointDistance);

        cable.ReDraw();
        RebuildCount++;

        // Setelah ReDraw, bukan sebelum : ReDraw menulis ulang seluruh vertex
        // dan index, jadi trim visual harus dipasang lagi di atas mesh baru.
        jacketTrim?.Apply();

        LogRebuild();

        PublishAttachmentFrames();
    }

    /// <summary>
    /// Diagnostik : apakah trim kedua ujung benar-benar terpasang lagi setelah
    /// tiap ReDraw.
    ///
    /// Yang dibandingkan index PERTAMA dan TERAKHIR yang benar-benar digambar,
    /// bukan indexStart dari SubMeshDescriptor : descriptor pernah melaporkan
    /// nilai benar sementara Unity tetap menggambar dari awal buffer.
    /// </summary>
    private void LogRebuild()
    {
        if (!logRebuilds || jacketTrim == null)
            return;

        MeshFilter filter = cable.GetComponent<MeshFilter>();
        Mesh mesh = filter != null ? filter.sharedMesh : null;

        int indices = mesh != null ? (int)mesh.GetIndexCount(0) : -1;

        int bands = jacketTrim.BandCount;
        int perBand = bands > 0 && cable.tris != null ? cable.tris.Length / bands : 0;
        int full = bands * perBand;

        int firstIndex = -1, expectFirst = -1;
        int lastIndex = -1, expectLast = -1;

        if (mesh != null && cable.tris != null && perBand > 0)
        {
            int[] drawn = mesh.GetIndices(0);

            if (drawn.Length > 0)
            {
                firstIndex = drawn[0];
                lastIndex = drawn[drawn.Length - 1];
            }

            int offsetFirst = jacketTrim.BoundaryRing * perBand;
            int offsetLast = jacketTrim.BoundaryRingB * perBand - 1;

            if (offsetFirst >= 0 && offsetFirst < cable.tris.Length)
                expectFirst = cable.tris[offsetFirst];

            if (offsetLast >= 0 && offsetLast < cable.tris.Length)
                expectLast = cable.tris[offsetLast];
        }

        Debug.Log(
            $"[CableController] rebuild #{RebuildCount}  " +
            $"A={jacketTrim.TrimLengthA:F4}->{jacketTrim.AchievedTrim:F4}  " +
            $"B={jacketTrim.TrimLengthB:F4}->{jacketTrim.AchievedTrimB:F4}  " +
            $"bands={jacketTrim.BoundaryRing}..{jacketTrim.BoundaryRingB}/{bands}  " +
            $"indices={indices}/{full}  " +
            $"first={firstIndex}/{expectFirst}  last={lastIndex}/{expectLast}  " +
            $"trimmedAtA={firstIndex == expectFirst && jacketTrim.BoundaryRing > 0}  " +
            $"trimmedAtB={lastIndex == expectLast && jacketTrim.BoundaryRingB < bands}",
            this);
    }

    /// <summary>
    /// Memberi tahu tiap endpoint arah keluar kabel di ujungnya, memakai
    /// tangen kurva ACC yang baru dibangun — bukan sekadar arah A→B, supaya
    /// tetap benar saat kabel melengkung.
    ///
    /// Tangen diambil dari turunan Bézier kuadratik yang dipakai ACC di
    /// parameter ujung (t=0 dan t=1) : titik kontrol tengah = midpoint yang
    /// digeser turun sebesar heightScale.
    /// </summary>
    private void PublishAttachmentFrames()
    {
        Vector3 a = pointA.Position;
        Vector3 b = pointB.Position;
        Vector3 mid = (a + b) * 0.5f + Vector3.down * cable.heightScale;

        // d/dt Bézier(t) : t=0 -> 2(mid - a), t=1 -> 2(b - mid).
        // Arah *keluar* = menjauh dari badan kabel, jadi tangen dibalik di A.
        Vector3 outA = a - mid;
        Vector3 outB = b - mid;

        // Degenerate (kedua endpoint & mid menyatu) : jatuh ke arah A-B.
        if (outA.sqrMagnitude <= 1e-10f) outA = a - b;
        if (outB.sqrMagnitude <= 1e-10f) outB = b - a;

        pointA.SetAttachmentDirection(outA);
        pointB.SetAttachmentDirection(outB);
    }

    /// <summary>
    /// Slack diterjemahkan menjadi kelengkungan, memakai parameter shape ACC —
    /// bukan mesh baru dan bukan scale.
    ///
    /// lengthDependentHeight dimatikan karena parameter itu mengalikan sagging
    /// dengan kuadrat jarak endpoint : makin jauh ditarik makin menjuntai,
    /// kebalikan dari kabel fisik. heightScale lalu dihitung dari slack.
    /// </summary>
    private void ApplySag(float distance)
    {
        // useCurvature menambah dua kurva ekstra di kedua ujung yang panjangnya
        // tidak ikut terhitung, membuat path 1–5% lebih panjang dari cableLength
        // (paling parah saat slack besar). Dimatikan supaya panjang lengkung
        // benar-benar sama dengan cableLength. Kurva & mesh tetap dibuat ACC.
        cable.useCurvature = false;

        cable.lengthDependentHeight = 0f;
        cable.heightScale = SolveHeightScale(distance);
    }

    /// <summary>
    /// heightScale yang membuat panjang lengkung kabel = cableLength.
    ///
    /// ACC menaruh control point Bézier kuadratik di titik tengah endpoint,
    /// digeser ke bawah sebesar heightScale. Bézier kuadratik simetris seperti
    /// itu adalah parabola, dan panjang busurnya punya bentuk tertutup :
    ///
    ///     u = 2h/d
    ///     arc = (d/2) * ( sqrt(1+u²) + asinh(u)/u )
    ///
    /// arc/d naik monoton terhadap u, jadi inversnya dicari dengan bisection —
    /// bukan aproksimasi kasar. 24 iterasi, hanya jalan saat rebuild.
    ///
    /// Kurva ACC tidak diduplikasi di sini: yang dihitung hanya *panjang* dari
    /// control point yang memang sudah dipakai ACC. Pembentuk kurva dan mesh
    /// tetap sepenuhnya ACC.
    /// </summary>
    private float SolveHeightScale(float distance)
    {
        if (cableLength - distance <= 0f)
            return 0f;

        // Endpoint menyatu : kabel menggantung terlipat dua, dalamnya L/2.
        if (distance <= Mathf.Epsilon)
            return cableLength;

        float target = 2f * cableLength / distance;

        float lo = 0f;
        float hi = target;

        for (int i = 0; i < 24; i++)
        {
            float mid = (lo + hi) * 0.5f;

            if (ArcRatio(mid) < target)
                lo = mid;
            else
                hi = mid;
        }

        return (lo + hi) * 0.5f * distance * 0.5f;
    }

    /// <summary>arc / (d/2) untuk u = 2h/d. Monoton naik dari 2 (kabel lurus).</summary>
    private static float ArcRatio(float u)
    {
        if (u <= 1e-6f)
            return 2f;

        return Mathf.Sqrt(1f + u * u) + Asinh(u) / u;
    }

    private static float Asinh(float x) => Mathf.Log(x + Mathf.Sqrt(x * x + 1f));

    /// <summary>
    /// Panjang lengkung kabel yang benar-benar terbentuk, bukan jarak endpoint.
    /// Dipakai untuk verifikasi : nilainya harus selalu ≈ cableLength.
    /// </summary>
    public float CablePathLength
    {
        get
        {
            float distance = EndpointDistance;

            if (distance <= Mathf.Epsilon)
                return cableLength;

            float h = SolveHeightScale(distance);

            return ArcRatio(2f * h / distance) * distance * 0.5f;
        }
    }

    private bool HasReferences() => pointA != null && pointB != null && cable != null;

    private void WarnMissingReferences()
    {
        if (refsWarned)
            return;

        refsWarned = true;

        Debug.LogWarning(
            "[CableController] Reference belum lengkap. Assign PointA, PointB, dan ACC_Cable di Inspector.",
            this);
    }

    private void OnValidate()
    {
        cableLength = Mathf.Max(0.01f, cableLength);
        stripLengthA = Mathf.Max(0f, stripLengthA);
    }

#if UNITY_EDITOR
    [ContextMenu("Log Status")]
    private void LogStatus()
    {
        if (!HasReferences())
        {
            Debug.LogWarning("[CableController] Reference belum lengkap.", this);
            return;
        }

        Debug.Log(
            $"[CableController] length={cableLength:F2}  distance={EndpointDistance:F2}  " +
            $"slack={Slack:F2}  pathLength={CablePathLength:F2}  " +
            $"heightScale={cable.heightScale:F2}  rebuilds={RebuildCount}",
            this);
    }

    [ContextMenu("Self Check")]
    private void SelfCheck()
    {
        float[] distances = { 0f, 0.5f, 1f, 2f, 3f, 4f, 4.9f, cableLength };
        bool pass = true;

        foreach (float d in distances)
        {
            float h = SolveHeightScale(d);
            float path = d <= Mathf.Epsilon
                ? cableLength
                : ArcRatio(2f * h / d) * d * 0.5f;

            bool ok = Mathf.Abs(path - cableLength) <= cableLength * 0.01f;
            pass &= ok;

            Debug.Log($"d={d:F2}  heightScale={h:F3}  pathLength={path:F3}  ok={ok}");
        }

        // Clamp : target di luar jangkauan harus jatuh tepat di batas.
        Vector3 anchor = Vector3.zero;
        foreach (float d in new[] { cableLength * 0.5f, cableLength, cableLength * 2f })
        {
            Vector3 clamped = ClampToLength(anchor, new Vector3(d, 0f, 0f), cableLength);
            bool ok = clamped.magnitude <= cableLength + 1e-4f;
            pass &= ok;

            Debug.Log($"clamp target={d:F2} -> {clamped.magnitude:F4}  ok={ok}");
        }

        Debug.Log($"[CableController] Self check : {(pass ? "PASS" : "FAIL")}", this);
    }

    /// <summary>
    /// Memeriksa pemetaan state -> trim untuk KEDUA ujung lewat jalur yang
    /// sebenarnya dipakai (menulis CablePoint.State, bukan memanggil
    /// TrimForState langsung), lalu memastikan endpoint dan panjang kabel tidak
    /// bergeser sama sekali.
    /// </summary>
    [ContextMenu("Self Check Strip")]
    private void SelfCheckStrip()
    {
        if (!HasReferences() || jacketTrim == null)
        {
            Debug.LogWarning(
                "[CableController] Butuh PointA, PointB, ACC_Cable, dan JacketTrim.", this);
            return;
        }

        CableState restoreA = pointA.State;
        CableState restoreB = pointB.State;
        Vector3 posA = pointA.Position;
        Vector3 posB = pointB.Position;
        float path = CablePathLength;
        bool pass = true;

        // Empat kombinasi state A/B : masing-masing ujung diuji sendiri dan
        // bersamaan, supaya trim satu ujung terbukti tidak mengganggu ujung lain.
        (CableState a, CableState b)[] cases =
        {
            (CableState.Intact, CableState.Intact),
            (CableState.Stripped, CableState.Intact),
            (CableState.Intact, CableState.Stripped),
            (CableState.Stripped, CableState.Stripped),
        };

        foreach ((CableState a, CableState b) in cases)
        {
            // Setter early-return bila nilainya sama, jadi dipaksa lewat state lain.
            pointA.State = a == CableState.Intact ? CableState.Stripped : CableState.Intact;
            pointA.State = a;
            pointB.State = b == CableState.Intact ? CableState.Stripped : CableState.Intact;
            pointB.State = b;

            float expectA = TrimForState(a, stripLengthA);
            float expectB = TrimForState(b, stripLengthB);

            bool trimOkA = Mathf.Abs(jacketTrim.TrimLengthA - expectA) <= 1e-5f;
            bool trimOkB = Mathf.Abs(jacketTrim.TrimLengthB - expectB) <= 1e-5f;

            // Stripped harus benar-benar memotong band, bukan hanya menyetel
            // angka — dan tiap ujung diperiksa lewat boundary-nya sendiri.
            bool cutOkA = a == CableState.Intact
                ? jacketTrim.BoundaryRing == 0
                : jacketTrim.BoundaryRing >= 1;

            bool cutOkB = b == CableState.Intact
                ? jacketTrim.BoundaryRingB == jacketTrim.BandCount
                : jacketTrim.BoundaryRingB < jacketTrim.BandCount;

            bool frozen =
                pointA.Position == posA &&
                pointB.Position == posB &&
                Mathf.Abs(CablePathLength - path) <= 1e-4f;

            bool ok = trimOkA && trimOkB && cutOkA && cutOkB && frozen;
            pass &= ok;

            Debug.Log(
                $"A={a} B={b}  " +
                $"trimA={jacketTrim.TrimLengthA:F4}/{expectA:F4} " +
                $"trimB={jacketTrim.TrimLengthB:F4}/{expectB:F4}  " +
                $"bands={jacketTrim.BoundaryRing}..{jacketTrim.BoundaryRingB}/{jacketTrim.BandCount}  " +
                $"cutA={cutOkA} cutB={cutOkB} frozen={frozen}  ok={ok}");
        }

        pointA.State = restoreA;
        pointB.State = restoreB;

        Debug.Log($"[CableController] Strip self check : {(pass ? "PASS" : "FAIL")}", this);
    }
#endif
}
