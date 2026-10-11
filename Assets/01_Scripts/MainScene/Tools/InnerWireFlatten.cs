using System.Collections.Generic;
using UnityEngine;

/// <summary>Status kerapian satu inner wire. Runtime saja, terpisah dari CablePoint.State.</summary>
public enum InnerWireFlattenState
{
    Loose,
    Flattened
}

/// <summary>
/// Fase MERAPIKAN 8 inner wire di satu endpoint : dari bundel bulat menjadi
/// susunan datar, sebelum urutannya dipersoalkan.
///
/// Flattening BUKAN ordering. Komponen ini tidak tahu T568A/T568B, tidak tahu
/// warna, dan sengaja tidak memegang referensi ke <see cref="InnerWireOrdering"/>
/// maupun <see cref="InnerWireSlot"/> — <c>Flattened</c> hanya berarti "sudah
/// dirapikan", tidak pernah berarti "urutannya benar". Self Check menegakkan
/// ketiadaan referensi itu lewat refleksi.
///
/// Yang mendefinisikan "datar" adalah KEPIPIHAN zone, bukan perhitungan planar
/// tambahan : zone adalah satu box tipis yang dirotasi ke attachment frame
/// endpoint, jadi wire yang masih menonjol keluar bidang otomatis berada di luar
/// area dan harus digeser. Karena frame-nya milik endpoint, hasilnya tetap benar
/// saat kabel diputar — bukan rapi terhadap world axis.
///
/// SUMBU TIPIS HARUS BISA DI-DRAG. Wire digeser pada satu bidang saja
/// (<see cref="InnerWireDragTool.DragPlaneNormal"/>), sehingga bila sumbu tipis
/// zone kebetulan sejajar normal bidang itu, tidak ada gerakan pointer yang bisa
/// memasukkan wire ke zone — setiap drop akan INVALID tanpa sebab yang terlihat.
/// <see cref="ThinAxis"/> memilih sumbu tipis dari ukuran box yang sesungguhnya,
/// dan Self Check menolak konfigurasi yang tidak terjangkau, jadi kondisi itu
/// gagal di Editor alih-alih membingungkan saat Play Mode.
///
/// Wire TIDAK di-snap. Ia tetap di tempat pemain melepasnya; yang berubah hanya
/// statusnya. localScale, localRotation, dan mesh tidak pernah ditulis dari sini.
/// Satu-satunya penulisan Transform adalah <c>position</c> saat drop tidak valid
/// dan wire dikembalikan ke posisi sebelum drag.
///
/// PENEMPATAN : pasang pada GameObject endpoint (mis. "A"), ancestor dari
/// GameObject yang memegang <see cref="CableInnerWiresView"/>.
/// <see cref="InnerWireDragTool"/> menemukannya lewat HitResolver sekali per grab
/// — tanpa raycast tambahan, tanpa pencarian per frame. Komponen ini juga tidak
/// punya Update sama sekali.
///
/// Milestone ini hanya endpoint A. Wire dari endpoint lain ditolak
/// <see cref="Owns"/>, jadi B tetap seperti sebelumnya.
/// </summary>
public class InnerWireFlatten : MonoBehaviour
{
    [Header("Reference")]
    [Tooltip("Sumber daftar 8 wire endpoint ini. Daftarnya tidak diduplikasi di sini.")]
    [SerializeField] private CableInnerWiresView wiresView;

    [Header("Flatten Zone")]
    [Tooltip("Area datar tujuan. Ketebalannya yang memaksa wire menjadi planar. " +
             "Harus convex (Box/Sphere/Capsule) karena ClosestPoint dipakai. " +
             "JANGAN dijadikan child wire mana pun — zone harus diam.")]
    [SerializeField] private Collider flattenZone;

    // Runtime saja : status kerapian tidak diserialisasi supaya scene tidak
    // menyimpan progres prototype.
    private readonly Dictionary<Transform, Vector3> dragOrigin = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, InnerWireFlattenState> state =
        new Dictionary<Transform, InnerWireFlattenState>();

    // Pose sebelum flatten + pose tujuan, diisi PrepareFlatten. Runtime saja.
    // dragOrigin tidak ikut disimpan : jalur flatten di bawah tidak pernah
    // menulisnya, jadi tidak ada yang perlu dipulihkan.
    private readonly Dictionary<Transform, Vector3> preFlattenPosition = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Vector3> flattenTarget = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, InnerWireFlattenState> preFlattenState =
        new Dictionary<Transform, InnerWireFlattenState>();

    private bool prepared;

    public CableInnerWiresView WiresView => wiresView;

    public Collider Zone => flattenZone;

    /// <summary>Apakah <paramref name="wire"/> memang salah satu wire endpoint ini.</summary>
    public bool Owns(Transform wire) =>
        wire != null && wiresView != null && wiresView.ResolveWire(wire) == wire;

    public InnerWireFlattenState StateOf(Transform wire) =>
        wire != null && state.TryGetValue(wire, out InnerWireFlattenState value)
            ? value
            : InnerWireFlattenState.Loose;

    /// <summary>Jumlah wire terdaftar yang sudah rapi.</summary>
    public int FlattenedCount
    {
        get
        {
            if (wiresView == null)
                return 0;

            int count = 0;

            foreach (GameObject wire in wiresView.Wires)
                if (wire != null && StateOf(wire.transform) == InnerWireFlattenState.Flattened)
                    count++;

            return count;
        }
    }

    /// <summary>
    /// Seluruh 8 wire sudah rapi. Selama ini masih false, fase flatten belum
    /// tuntas dan tahap berikutnya tidak boleh berjalan.
    /// </summary>
    public bool IsComplete
    {
        get
        {
            if (wiresView == null)
                return false;

            int registered = 0;

            foreach (GameObject wire in wiresView.Wires)
            {
                if (wire == null)
                    continue;

                registered++;

                if (StateOf(wire.transform) != InnerWireFlattenState.Flattened)
                    return false;
            }

            return registered > 0;
        }
    }

    /// <summary>
    /// Apakah titik ini berada di dalam zone. Nol raycast.
    ///
    /// Collider yang disabled (atau GameObject-nya inactive) membuat
    /// <see cref="Collider.ClosestPoint"/> memulangkan titik input apa adanya —
    /// artinya SEMUA titik akan lolos sebagai "di dalam". Itu diukur, bukan
    /// diduga, jadi keaktifannya diperiksa lebih dulu.
    /// </summary>
    public bool InZone(Vector3 worldPoint)
    {
        if (!ZoneUsable)
            return false;

        return (flattenZone.ClosestPoint(worldPoint) - worldPoint).sqrMagnitude <= 1e-10f;
    }

    /// <summary>Zone ada, komponennya enabled, dan GameObject-nya aktif.</summary>
    public bool ZoneUsable =>
        flattenZone != null && flattenZone.enabled && flattenZone.gameObject.activeInHierarchy;

    /// <summary>
    /// Arah dunia sumbu TERTIPIS zone — inilah arah yang harus dirapikan.
    /// Diambil dari ukuran dunia box yang sesungguhnya (sudah memperhitungkan
    /// scale parent), bukan diasumsikan sumbu Y.
    /// </summary>
    public Vector3 ThinAxis
    {
        get
        {
            var box = flattenZone as BoxCollider;

            if (box == null)
                return flattenZone != null ? flattenZone.transform.up : Vector3.up;

            return AxisVector(box, SmallestAxisIndex(WorldSize));
        }
    }

    /// <summary>
    /// Arah dunia tempat 8 lane flatten dibariskan : sumbu TERLEBAR zone di luar
    /// sumbu tipis, jadi barisan punya ruang paling banyak dan tetap berada di
    /// dalam bidang datar yang sama.
    ///
    /// Diambil dari ukuran zone, bukan dari posisi slot : komponen ini tidak
    /// boleh tahu apa pun soal slot maupun warna.
    /// </summary>
    public Vector3 LaneAxis
    {
        get
        {
            var box = flattenZone as BoxCollider;

            if (box == null)
                return flattenZone != null ? flattenZone.transform.right : Vector3.right;

            Vector3 size = WorldSize;

            return AxisVector(box, WidestAxisIndexExcept(size, SmallestAxisIndex(size)));
        }
    }

    /// <summary>Panjang dunia zone sepanjang <see cref="LaneAxis"/>.</summary>
    public float LaneExtent
    {
        get
        {
            if (flattenZone == null)
                return 0f;

            var box = flattenZone as BoxCollider;

            if (box == null)
                // Collider non-box : dipakai komponen AABB terkecil supaya
                // barisan tetap di dalam bentuk apa pun (sphere/capsule).
                return SmallestComponent(flattenZone.bounds.size);

            Vector3 size = WorldSize;

            return size[WidestAxisIndexExcept(size, SmallestAxisIndex(size))];
        }
    }

    /// <summary>Titik tengah zone yang sesungguhnya (bukan AABB), ikut rotasi & scale.</summary>
    public Vector3 ZoneCenter
    {
        get
        {
            var box = flattenZone as BoxCollider;

            if (box != null)
                return box.transform.TransformPoint(box.center);

            return flattenZone != null ? flattenZone.bounds.center : Vector3.zero;
        }
    }

    /// <summary>Sudah <see cref="PrepareFlatten"/> dan belum di-restore.</summary>
    public bool IsPrepared => prepared;

    /// <summary>
    /// Menyiapkan pose tujuan : 8 lane berjajar di dalam zone, sepanjang
    /// <see cref="LaneAxis"/>, semuanya tepat di titik tengah sumbu tipis —
    /// jadi barisannya planar karena ZONE yang mendefinisikannya, bukan karena
    /// ada perhitungan planar kedua di sini.
    ///
    /// Urutan lane DI-SHUFFLE (Fisher-Yates) dan permutasi identitas ditolak,
    /// supaya hasil flatten tidak pernah kebetulan sama dengan urutan wire
    /// terdaftar — urutan yang juga dipakai pembuat slot. Tanpa itu, tiap wire
    /// mendarat persis di depan slotnya sendiri dan sorting kehilangan isinya.
    /// Shuffle tidak membaca warna maupun slot; ia hanya menukar indeks.
    /// </summary>
    /// <returns>false = pose tujuan tidak bisa disusun; alasannya di-log.</returns>
    public bool PrepareFlatten()
    {
        if (wiresView == null)
        {
            Debug.LogError(
                $"[{nameof(InnerWireFlatten)}] CableInnerWiresView belum diassign — " +
                $"pose flatten tidak bisa disusun.", this);
            return false;
        }

        if (!ZoneUsable)
        {
            Debug.LogError(
                $"[{nameof(InnerWireFlatten)}] Zone tidak dapat dipakai " +
                $"(belum diassign, disabled, atau GameObject-nya inactive) — " +
                $"pose flatten tidak bisa disusun.", this);
            return false;
        }

        var mesh = flattenZone as MeshCollider;

        if (mesh != null && !mesh.convex)
        {
            Debug.LogError(
                $"[{nameof(InnerWireFlatten)}] Zone '{flattenZone.name}' adalah MeshCollider " +
                $"non-convex — ClosestPoint tidak sahih di atasnya.", flattenZone);
            return false;
        }

        var wires = new List<Transform>();

        foreach (GameObject wire in wiresView.Wires)
            if (wire != null)
                wires.Add(wire.transform);

        if (wires.Count == 0)
        {
            Debug.LogError(
                $"[{nameof(InnerWireFlatten)}] Belum ada wire diassign di " +
                $"CableInnerWiresView.", wiresView);
            return false;
        }

        float extent = LaneExtent;

        if (extent <= 0f)
        {
            Debug.LogError(
                $"[{nameof(InnerWireFlatten)}] Lebar zone terukur {extent:F6} m — " +
                $"tidak ada ruang untuk membariskan wire.", flattenZone);
            return false;
        }

        // Pose awal & status disimpan lengkap SEBELUM apa pun ditulis, supaya
        // RestorePreFlatten bisa memulangkan keadaan apa adanya.
        preFlattenPosition.Clear();
        preFlattenState.Clear();
        flattenTarget.Clear();

        foreach (Transform wire in wires)
        {
            preFlattenPosition[wire] = wire.position;
            preFlattenState[wire] = StateOf(wire);
        }

        int[] lane = ShuffledLanes(wires.Count);

        Vector3 center = ZoneCenter;
        Vector3 axis = LaneAxis.normalized;

        // 80% lebar zone : lane terluar tetap jauh dari dinding, jadi InZone
        // tidak bergantung pada presisi tepi collider.
        float spacing = extent * 0.8f / wires.Count;

        for (int i = 0; i < wires.Count; i++)
        {
            float offset = (lane[i] - (wires.Count - 1) * 0.5f) * spacing;

            flattenTarget[wires[i]] = center + axis * offset;
        }

        prepared = true;

        Debug.Log(
            $"[{nameof(InnerWireFlatten)}] PREPARE  wires={wires.Count}  " +
            $"laneExtent={extent * 1000f:F2} mm  spacing={spacing * 1000f:F2} mm\n" +
            $"  laneAxis={axis:F3}  thinAxis={ThinAxis.normalized:F3}  center={center:F4}\n" +
            $"  lane order = {string.Join(",", lane)}  (indeks terdaftar -> posisi barisan)",
            this);

        return true;
    }

    /// <summary>
    /// Menggerakkan wire dari pose awal ke pose flatten. <paramref name="t"/> 0 =
    /// pose awal, 1 = pose flatten; nilainya di-clamp. Hanya <c>position</c> yang
    /// ditulis — localScale, localRotation, dan mesh tidak disentuh.
    ///
    /// Pada t = 1, status tiap wire ditetapkan dari HASIL PENGUKURAN
    /// (<see cref="InZone"/>), bukan diasumsikan : kalau ternyata ada yang tidak
    /// sampai di dalam zone, <see cref="IsComplete"/> tetap false dan kegagalan
    /// itu terlihat.
    ///
    /// Tidak ada Update di komponen ini. Yang memanggil per frame adalah pemilik
    /// tahap (InnerWireStageController).
    /// </summary>
    public void ApplyFlatten(float t)
    {
        if (!prepared)
        {
            Debug.LogWarning(
                $"[{nameof(InnerWireFlatten)}] ApplyFlatten dipanggil sebelum PrepareFlatten — " +
                $"tidak ada pose tujuan, jadi diabaikan.", this);
            return;
        }

        float clamped = Mathf.Clamp01(t);

        // Ease in-out : gerakannya mulai dan berhenti halus, seperti tangan
        // menekan. Murni pemetaan waktu, tidak mengubah pose ujungnya.
        float eased = clamped * clamped * (3f - 2f * clamped);

        foreach (KeyValuePair<Transform, Vector3> entry in flattenTarget)
        {
            Transform wire = entry.Key;

            if (wire == null || !preFlattenPosition.TryGetValue(wire, out Vector3 from))
                continue;

            wire.position = Vector3.Lerp(from, entry.Value, eased);
        }

        if (clamped < 1f)
            return;

        foreach (KeyValuePair<Transform, Vector3> entry in flattenTarget)
        {
            Transform wire = entry.Key;

            if (wire == null)
                continue;

            state[wire] = InZone(wire.position)
                ? InnerWireFlattenState.Flattened
                : InnerWireFlattenState.Loose;
        }

        Debug.Log(
            $"[{nameof(InnerWireFlatten)}] FLATTEN DONE  {FlattenedCount} wire rapi  " +
            $"complete={IsComplete}", this);
    }

    /// <summary>
    /// Memulangkan pose dan status yang disimpan <see cref="PrepareFlatten"/>.
    /// Dipakai saat flatten dibatalkan, gagal, atau setelah pemeriksaan.
    /// </summary>
    public void RestorePreFlatten()
    {
        if (!prepared)
            return;

        foreach (KeyValuePair<Transform, Vector3> entry in preFlattenPosition)
            if (entry.Key != null)
                entry.Key.position = entry.Value;

        foreach (KeyValuePair<Transform, InnerWireFlattenState> entry in preFlattenState)
        {
            if (entry.Key == null)
                continue;

            if (entry.Value == InnerWireFlattenState.Loose)
                state.Remove(entry.Key);
            else
                state[entry.Key] = entry.Value;
        }

        preFlattenPosition.Clear();
        preFlattenState.Clear();
        flattenTarget.Clear();

        prepared = false;

        Debug.Log(
            $"[{nameof(InnerWireFlatten)}] RESTORE pose sebelum flatten  " +
            $"{FlattenedCount} wire rapi  complete={IsComplete}", this);
    }

    /// <summary>
    /// Fisher-Yates, lalu permutasi identitas ditolak dan diulang. Hanya indeks
    /// yang ditukar — tidak ada warna, slot, maupun posisi yang dibaca.
    /// </summary>
    private static int[] ShuffledLanes(int count)
    {
        int[] lane = new int[count];

        for (int i = 0; i < count; i++)
            lane[i] = i;

        if (count < 2)
            return lane;

        // Batas percobaan : dengan 8 elemen, peluang identitas 1/40320, jadi
        // batas ini praktis tidak pernah tercapai — ia hanya menutup kemungkinan
        // loop tak berujung.
        for (int attempt = 0; attempt < 16; attempt++)
        {
            for (int i = count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);

                (lane[i], lane[j]) = (lane[j], lane[i]);
            }

            bool identity = true;

            for (int i = 0; i < count; i++)
                if (lane[i] != i)
                {
                    identity = false;
                    break;
                }

            if (!identity)
                return lane;
        }

        // Kalau sampai di sini, tukar dua elemen pertama : apa pun lebih baik
        // daripada memulangkan urutan terdaftar apa adanya.
        (lane[0], lane[1]) = (lane[1], lane[0]);

        return lane;
    }

    private static int SmallestAxisIndex(Vector3 size)
    {
        if (size.x <= size.y && size.x <= size.z)
            return 0;

        return size.y <= size.z ? 1 : 2;
    }

    /// <summary>Sumbu terlebar selain <paramref name="skip"/>. Seri = indeks terkecil.</summary>
    private static int WidestAxisIndexExcept(Vector3 size, int skip)
    {
        int best = -1;

        for (int i = 0; i < 3; i++)
        {
            if (i == skip)
                continue;

            if (best < 0 || size[i] > size[best])
                best = i;
        }

        return best;
    }

    private static Vector3 AxisVector(BoxCollider box, int index) =>
        index == 0 ? box.transform.right :
        index == 1 ? box.transform.up :
                     box.transform.forward;

    private static float SmallestComponent(Vector3 value) =>
        Mathf.Min(value.x, Mathf.Min(value.y, value.z));

    /// <summary>Ukuran dunia efektif zone (box saja). Zero untuk collider lain.</summary>
    public Vector3 WorldSize
    {
        get
        {
            var box = flattenZone as BoxCollider;

            if (box == null)
                return Vector3.zero;

            Vector3 scale = box.transform.lossyScale;

            return new Vector3(
                Mathf.Abs(box.size.x * scale.x),
                Mathf.Abs(box.size.y * scale.y),
                Mathf.Abs(box.size.z * scale.z));
        }
    }

    /// <summary>
    /// Dicatat saat wire diambil.
    /// </summary>
    /// <returns>
    /// false = drop wire ini bukan urusan fase flatten (bukan wire endpoint ini,
    /// atau fase sudah tuntas). Pemanggil lalu meneruskannya ke tahap berikutnya.
    /// </returns>
    public bool Grab(Transform wire)
    {
        if (!Owns(wire) || IsComplete)
            return false;

        // Zone mati = fase ini tidak bisa memutuskan apa pun. Menolak grab
        // membuatnya terlihat sebagai "flatten tidak jalan", bukan sebagai
        // "semua drop valid".
        if (!ZoneUsable)
        {
            Debug.LogWarning(
                $"[InnerWireFlatten] Zone tidak dapat dipakai " +
                $"(belum diassign, disabled, atau GameObject-nya inactive) — " +
                $"drag '{wire.name}' dilewatkan dari fase flatten.", this);
            return false;
        }

        dragOrigin[wire] = wire.position;

        Debug.Log($"[InnerWireFlatten] GRAB '{wire.name}'", wire);

        return true;
    }

    /// <summary>
    /// Dipanggil saat pointer dilepas. Di dalam zone : wire tetap di tempatnya
    /// dan menjadi Flattened. Di luar zone : wire kembali ke posisi sebelum drag.
    /// Hanya <c>position</c> yang ditulis.
    /// </summary>
    public bool Drop(Transform wire)
    {
        if (!Owns(wire))
            return false;

        if (InZone(wire.position))
        {
            state[wire] = InnerWireFlattenState.Flattened;

            Debug.Log(
                $"[InnerWireFlatten] DROP '{wire.name}' → FLATTENED  " +
                $"({FlattenedCount}/8)",
                wire);

            return true;
        }

        Debug.Log($"[InnerWireFlatten] DROP '{wire.name}' → INVALID", wire);

        state[wire] = InnerWireFlattenState.Loose;

        if (dragOrigin.TryGetValue(wire, out Vector3 origin))
        {
            wire.position = origin;

            Debug.Log($"[InnerWireFlatten] RESET '{wire.name}'", wire);
        }

        return false;
    }

#if UNITY_EDITOR
    [Header("Zone Creator (Editor)")]
    [Tooltip("Induk zone. Kosong = GameObject komponen ini (endpoint), yang " +
             "scale-nya 1 sehingga angka di bawah = milimeter dunia sungguhan. " +
             "JANGAN diisi wire, dan hindari parent ber-scale.")]
    [SerializeField] private Transform zoneParent;
    [Tooltip("Jarak pusat zone dari endpoint, sepanjang arah keluar kabel (meter). " +
             "Pakai 'center Z' dari Log Status.")]
    [SerializeField] private float zoneForwardOffset = 0.0753f;
    [Tooltip("Lebar zone menyamping (meter). Boleh longgar — sumbu ini bisa " +
             "di-drag, jadi memperbesarnya hanya mempermudah, tidak melemahkan " +
             "syarat datar.")]
    [SerializeField] private float zoneWidth = 0.06f;
    [Tooltip("KETEBALAN zone (meter). Inilah SATU-SATUNYA syarat datar : harus " +
             "LEBIH KECIL dari 'spread Y' di Log Status, kalau tidak bundel yang " +
             "sekarang sudah dianggap datar dan fase ini tanpa arti.")]
    [SerializeField] private float zoneThickness = 0.004f;
    [Tooltip("Kedalaman zone sepanjang arah keluar kabel (meter). Boleh longgar — " +
             "sumbu ini juga bisa di-drag. Toleransi maju-mundur saja.")]
    [SerializeField] private float zoneDepth = 0.06f;

    /// <summary>
    /// Membuat satu zone box pipih, dirotasi ke attachment frame endpoint.
    /// Tidak ada MeshFilter/Renderer yang dibuat — collider saja.
    ///
    /// MENGUBAH SCENE. Terdaftar di Undo (Ctrl+Z).
    /// </summary>
    [ContextMenu("Flatten/Create Zone")]
    private void CreateZone()
    {
        if (wiresView == null)
        {
            Debug.LogError($"[{nameof(InnerWireFlatten)}] CableInnerWiresView belum diassign.", this);
            return;
        }

        CablePoint point = wiresView.Point;

        if (point == null)
        {
            Debug.LogError(
                $"[{nameof(InnerWireFlatten)}] CableInnerWiresView belum punya CablePoint — " +
                $"frame endpoint tidak diketahui.", this);
            return;
        }

        Transform parent = zoneParent != null ? zoneParent : transform;

        // Parent ber-scale membuat angka Inspector tidak sama dengan ukuran dunia.
        // Dilaporkan, bukan dikompensasi diam-diam : yang memilih parent adalah
        // kamu, dan kompensasi otomatis akan menyembunyikan sebab bila scale
        // parent berubah nanti.
        Vector3 parentScale = parent.lossyScale;
        bool unscaled =
            Mathf.Approximately(parentScale.x, 1f) &&
            Mathf.Approximately(parentScale.y, 1f) &&
            Mathf.Approximately(parentScale.z, 1f);

        if (!unscaled)
            Debug.LogWarning(
                $"[{nameof(InnerWireFlatten)}] Parent '{parent.name}' ber-scale " +
                $"{parentScale:F4} — ukuran dunia zone jadi {zoneWidth * parentScale.x:F4} x " +
                $"{zoneThickness * parentScale.y:F4} x {zoneDepth * parentScale.z:F4} m, " +
                $"bukan angka di Inspector. Pakai parent tanpa scale " +
                $"(kosongkan Zone Parent) supaya angkanya jujur.", this);

        var go = new GameObject("FlattenZone_" + point.Endpoint);
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Flatten Zone");

        go.transform.SetParent(parent, false);

        // Rotasi mengikuti arah keluar kabel : sumbu tipis zone jadi tegak lurus
        // bidang datar yang diinginkan, bukan tegak lurus world Y.
        Quaternion frame = point.AttachmentRotation;

        go.transform.SetPositionAndRotation(
            point.Position + frame * new Vector3(0f, 0f, zoneForwardOffset),
            frame);

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(zoneWidth, zoneThickness, zoneDepth);

        UnityEditor.Undo.RecordObject(this, "Create Flatten Zone");
        flattenZone = box;
        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log(
            $"[{nameof(InnerWireFlatten)}] Zone dibuat di '{parent.name}'\n" +
            $"  size Inspector = {box.size:F4} m\n" +
            $"  size DUNIA     = {WorldSize:F4} m  (tebal efektif = " +
            $"{SmallestComponent(WorldSize) * 1000f:F2} mm)\n" +
            $"  {InsideCount()}/8 wire sudah di dalam zone sebelum dirapikan " +
            $"(idealnya kecil — 8/8 berarti tebal zone melebihi sebaran wire)",
            this);
    }

    [ContextMenu("Flatten/Log Status")]
    private void LogStatus()
    {
        if (wiresView == null)
        {
            Debug.LogWarning($"[{nameof(InnerWireFlatten)}] CableInnerWiresView belum diassign.", this);
            return;
        }

        CablePoint point = wiresView.Point;
        Quaternion inverse = point != null
            ? Quaternion.Inverse(point.AttachmentRotation)
            : Quaternion.identity;
        Vector3 anchor = point != null ? point.Position : transform.position;

        string lines = string.Empty;
        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;

        foreach (GameObject wire in wiresView.Wires)
        {
            if (wire == null)
                continue;

            // Diukur di ruang endpoint, bukan world : angka ini yang dipakai untuk
            // menyetel Zone Width / Thickness / Forward Offset.
            Vector3 local = inverse * (wire.transform.position - anchor);

            min = Vector3.Min(min, local);
            max = Vector3.Max(max, local);

            lines += $"\n  {wire.name,-14} state={StateOf(wire.transform),-9} " +
                     $"inZone={InZone(wire.transform.position),-5} " +
                     $"local mm=({local.x * 1000f,7:F2},{local.y * 1000f,7:F2},{local.z * 1000f,7:F2})";
        }

        Vector3 spread = max - min;

        Debug.Log(
            $"[{nameof(InnerWireFlatten)}] zone={(flattenZone != null ? flattenZone.name : "none")}  " +
            $"flattened={FlattenedCount}/8  complete={IsComplete}\n" +
            $"  spread wire mm : X={spread.x * 1000f:F2}  Y={spread.y * 1000f:F2}  Z={spread.z * 1000f:F2}\n" +
            $"  center Z mm    = {(min.z + max.z) * 500f:F2}   " +
            $"(setel Zone Forward Offset ≈ ini/1000)\n" +
            $"  zone size DUNIA= {WorldSize:F4} m  tebal={SmallestComponent(WorldSize) * 1000f:F2} mm  " +
            $"(harus < spread Y, kalau tidak fase ini tanpa arti)\n" +
            $"{Reachability()}" +
            lines,
            this);
    }

    /// <summary>
    /// Apakah sumbu tipis zone benar-benar bisa dicapai oleh drag.
    ///
    /// Wire hanya bergerak pada satu bidang. Bila sumbu tipis zone sejajar normal
    /// bidang itu, tidak ada gerakan pointer yang bisa memasukkan wire ke zone —
    /// gejalanya "DROP selalu INVALID" tanpa sebab yang terlihat. Diperiksa di
    /// Editor supaya tidak perlu ditemukan lewat coba-coba di Play Mode.
    /// </summary>
    private string Reachability()
    {
        if (!ZoneUsable)
            return "  reachability   : zone belum siap\n";

        var tool = FindFirstObjectByType<InnerWireDragTool>();

        if (tool == null)
            return "  reachability   : InnerWireDragTool tidak ada di scene\n";

        Vector3 normal = tool.DragPlaneNormal.normalized;
        Vector3 thin = ThinAxis.normalized;

        // Komponen sumbu tipis yang terletak DI DALAM bidang drag. 0 = mustahil.
        float alignment = Mathf.Abs(Vector3.Dot(thin, normal));
        float reachable = Mathf.Sqrt(Mathf.Max(0f, 1f - alignment * alignment));

        string verdict =
            reachable < 0.05f
                ? "MUSTAHIL — sumbu tipis tegak lurus bidang drag, tiap DROP akan INVALID. " +
                  "Putar kamera (mis. ke oblique) atau ubah orientasi zone."
                : reachable < 0.3f
                    ? "SANGAT SULIT — sumbu tipis hampir tegak lurus bidang drag."
                    : "OK";

        return
            $"  dragPlaneNormal= {normal:F3}  zoneThinAxis={thin:F3}\n" +
            $"  reachability   = {reachable:F3}  -> {verdict}\n";
    }

    private int InsideCount()
    {
        if (wiresView == null)
            return 0;

        int count = 0;

        foreach (GameObject wire in wiresView.Wires)
            if (wire != null && InZone(wire.transform.position))
                count++;

        return count;
    }

    /// <summary>
    /// Berjalan tanpa Play Mode. Selain memeriksa pendaftaran, ia benar-benar
    /// menjalankan <see cref="Grab"/> dan <see cref="Drop"/> — jalur yang sama
    /// dengan runtime — untuk kasus valid dan tidak valid, lalu mengembalikan
    /// posisi dan status ke keadaan semula.
    /// </summary>
    [ContextMenu("Flatten/Self Check")]
    private void SelfCheck()
    {
        bool pass = true;

        if (wiresView == null)
        {
            Debug.LogError($"[{nameof(InnerWireFlatten)}] CableInnerWiresView belum diassign.", this);
            return;
        }

        IReadOnlyList<GameObject> wires = wiresView.Wires;

        // ---- 1, 2 : pendaftaran wire. ----
        int wireCount = 0;
        bool noNull = true;

        for (int i = 0; i < wires.Count; i++)
        {
            if (wires[i] == null)
            {
                noNull = false;
                Debug.LogError(
                    $"[{nameof(InnerWireFlatten)}] Inner Wires slot [{i}] kosong di " +
                    $"CableInnerWiresView.", wiresView);
                continue;
            }

            wireCount++;
        }

        pass &= Check("8 wire ditemukan", wireCount == 8, $"{wireCount}/8");
        pass &= Check("tidak ada wire null", noNull, string.Empty);

        // ---- 3 : zone valid. ----
        bool zoneOk = flattenZone != null;
        bool zoneUsable = ZoneUsable;
        bool zoneConvex = false;
        bool zoneDetached = true;
        bool zoneNoMesh = false;

        if (zoneOk)
        {
            // ClosestPoint hanya sahih pada collider convex. MeshCollider
            // non-convex akan melempar, jadi ditolak di sini bukan saat runtime.
            var mesh = flattenZone as MeshCollider;
            zoneConvex = mesh == null || mesh.convex;

            zoneNoMesh = flattenZone.GetComponent<MeshFilter>() == null &&
                         flattenZone.GetComponent<Renderer>() == null;

            foreach (GameObject wire in wires)
            {
                if (wire == null)
                    continue;

                if (flattenZone.transform.IsChildOf(wire.transform))
                {
                    zoneDetached = false;
                    Debug.LogError(
                        $"[{nameof(InnerWireFlatten)}] Zone '{flattenZone.name}' adalah child " +
                        $"dari wire '{wire.name}' — zone akan ikut bergerak saat wire di-drag.",
                        flattenZone);
                }
            }
        }

        pass &= Check("flatten zone terpasang", zoneOk, string.Empty);
        pass &= Check(
            "zone enabled & GameObject aktif",
            zoneUsable,
            zoneOk
                ? $"enabled={flattenZone.enabled} active={flattenZone.gameObject.activeInHierarchy}"
                : "no zone");
        pass &= Check("zone convex (ClosestPoint sahih)", zoneConvex, string.Empty);
        pass &= Check("zone bukan child wire", zoneDetached, string.Empty);

        // ---- 13 : tidak ada mesh baru. ----
        pass &= Check("zone tanpa mesh/renderer baru", zoneNoMesh, string.Empty);

        if (!zoneUsable || !zoneConvex)
        {
            Debug.Log($"[{nameof(InnerWireFlatten)}] Self Check : FAIL (zone belum siap)", this);
            return;
        }

        // ---- Zone harus terjangkau oleh drag, dan masih menuntut kerapian. ----
        InnerWireDragTool tool = FindFirstObjectByType<InnerWireDragTool>();
        float reachable = 0f;

        if (tool != null)
        {
            Vector3 normal = tool.DragPlaneNormal.normalized;
            Vector3 thin = ThinAxis.normalized;
            float alignment = Mathf.Abs(Vector3.Dot(thin, normal));
            reachable = Mathf.Sqrt(Mathf.Max(0f, 1f - alignment * alignment));
        }

        pass &= Check(
            "sumbu tipis zone terjangkau drag",
            tool == null || reachable >= 0.3f,
            tool != null
                ? $"reachability={reachable:F3} (butuh >= 0.30; 0 = tiap DROP pasti INVALID)"
                : "no InnerWireDragTool di scene");

        Vector3 worldSize = WorldSize;

        if (worldSize != Vector3.zero)
        {
            float thickness = SmallestComponent(worldSize);
            int alreadyInside = InsideCount();

            pass &= Check(
                "zone masih menuntut kerapian",
                alreadyInside < 8,
                $"{alreadyInside}/8 wire sudah di dalam zone sebelum dirapikan; " +
                $"tebal dunia={thickness * 1000f:F2} mm");
        }

        // ---- 4 : flatten system tidak berada di Cable Module. ----
        string scriptPath = UnityEditor.AssetDatabase.GetAssetPath(
            UnityEditor.MonoScript.FromMonoBehaviour(this));

        pass &= Check(
            "flatten bukan di Cable Module",
            !scriptPath.Replace('\\', '/').Contains("/01_Scripts/Cable/"),
            scriptPath);

        // ---- 5 : InnerWireDragTool tidak memegang CablePoint. ----
        pass &= Check(
            "InnerWireDragTool tanpa field CablePoint",
            !HasFieldOfType(typeof(InnerWireDragTool), typeof(CablePoint)),
            string.Empty);

        // ---- 14 : tidak ada InputAction baru. ----
        bool noInput =
            !HasFieldOfType(typeof(InnerWireFlatten), typeof(UnityEngine.InputSystem.InputAction)) &&
            !HasFieldOfType(typeof(InnerWireFlatten), typeof(UnityEngine.InputSystem.InputActionReference)) &&
            !HasFieldOfType(typeof(InnerWireFlatten), typeof(UnityEngine.InputSystem.InputActionAsset));

        pass &= Check("flatten tanpa InputAction sendiri", noInput, string.Empty);

        // ---- 15 : tidak ada raycast per frame. Komponen ini tidak punya Update. ----
        const System.Reflection.BindingFlags Any =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public;

        bool noTick =
            typeof(InnerWireFlatten).GetMethod("Update", Any) == null &&
            typeof(InnerWireFlatten).GetMethod("LateUpdate", Any) == null &&
            typeof(InnerWireFlatten).GetMethod("FixedUpdate", Any) == null;

        pass &= Check("flatten tanpa Update/LateUpdate/FixedUpdate", noTick, string.Empty);

        // ---- 16 : ordering tidak dijalankan oleh flattening. ----
        bool noOrdering =
            !HasFieldOfType(typeof(InnerWireFlatten), typeof(InnerWireOrdering)) &&
            !HasFieldOfType(typeof(InnerWireFlatten), typeof(InnerWireSlot)) &&
            !HasFieldOfType(typeof(InnerWireFlatten), typeof(InnerWireSlot[]));

        pass &= Check("flatten tidak mereferensi Ordering/Slot", noOrdering, string.Empty);

        // ================= Simulasi jalur runtime =================
        CablePoint point = wiresView.Point;
        CablePoint otherPoint = FindOtherEndpoint(point);
        CableJacketTrim trim = point != null
            ? HitResolver.Resolve<CableJacketTrim>(point.transform)
            : null;
        CableController controller = point != null
            ? HitResolver.Resolve<CableController>(point.transform)
            : null;

        Vector3 pointBefore = point != null ? point.Position : Vector3.zero;
        Vector3 otherBefore = otherPoint != null ? otherPoint.Position : Vector3.zero;
        CableState stateBefore = point != null ? point.State : default;
        CableState otherStateBefore = otherPoint != null ? otherPoint.State : default;
        float trimABefore = trim != null ? trim.AchievedTrim : 0f;
        float trimBBefore = trim != null ? trim.AchievedTrimB : 0f;
        int bandBefore = trim != null ? trim.BandCount : 0;
        int boundaryABefore = trim != null ? trim.BoundaryRing : 0;
        int boundaryBBefore = trim != null ? trim.BoundaryRingB : 0;
        float pathBefore = controller != null ? controller.CablePathLength : 0f;

        var scaleBefore = new Dictionary<Transform, Vector3>();
        var rotationBefore = new Dictionary<Transform, Quaternion>();
        var positionBefore = new Dictionary<Transform, Vector3>();

        foreach (GameObject wire in wires)
        {
            if (wire == null)
                continue;

            Transform t = wire.transform;
            scaleBefore[t] = t.localScale;
            rotationBefore[t] = t.localRotation;
            positionBefore[t] = t.position;
        }

        // Simpan status agar Self Check tidak mengubah progres pemain.
        var savedState = new Dictionary<Transform, InnerWireFlattenState>(state);
        var savedOrigin = new Dictionary<Transform, Vector3>(dragOrigin);

        // Titik uji : pusat zone (pasti di dalam untuk collider convex), dan
        // satu titik jauh di luar zone.
        Vector3 inside = flattenZone.ClosestPoint(flattenZone.bounds.center);
        Vector3 outside = flattenZone.bounds.center +
                          flattenZone.transform.up * (flattenZone.bounds.size.magnitude + 1f);

        pass &= Check("pusat zone dianggap di dalam", InZone(inside), $"{inside:F4}");
        pass &= Check("titik jauh dianggap di luar", !InZone(outside), string.Empty);

        // ---- 12 : posisi wire dapat berubah, dan drop valid -> Flattened. ----
        bool validPath = true;
        bool moved = false;

        foreach (GameObject wire in wires)
        {
            if (wire == null)
                continue;

            Transform t = wire.transform;

            // Fase harus terbuka untuk tiap wire yang belum rapi.
            state.Remove(t);

            if (!Grab(t))
            {
                validPath = false;
                Debug.LogError($"[{nameof(InnerWireFlatten)}] Grab('{wire.name}') ditolak.", wire);
                continue;
            }

            t.position = inside;

            if (t.position != positionBefore[t])
                moved = true;

            if (!Drop(t) || StateOf(t) != InnerWireFlattenState.Flattened)
            {
                validPath = false;
                Debug.LogError(
                    $"[{nameof(InnerWireFlatten)}] Drop di dalam zone tidak menjadikan " +
                    $"'{wire.name}' Flattened.", wire);
            }
        }

        pass &= Check("drop di dalam zone -> Flattened", validPath, string.Empty);
        pass &= Check("posisi wire dapat berubah", moved, string.Empty);

        // ---- Drop tidak valid -> reset ke posisi sebelum drag. ----
        bool invalidPath = true;

        foreach (GameObject wire in wires)
        {
            if (wire == null)
                continue;

            Transform t = wire.transform;

            state.Remove(t);

            if (!Grab(t))
            {
                invalidPath = false;
                continue;
            }

            Vector3 origin = t.position;
            t.position = outside;

            if (Drop(t))
            {
                invalidPath = false;
                Debug.LogError(
                    $"[{nameof(InnerWireFlatten)}] Drop di luar zone dianggap valid untuk " +
                    $"'{wire.name}'.", wire);
                continue;
            }

            if (t.position != origin)
            {
                invalidPath = false;
                Debug.LogError(
                    $"[{nameof(InnerWireFlatten)}] '{wire.name}' tidak kembali ke posisi " +
                    $"sebelum drag : {origin:F6} -> {t.position:F6}", wire);
            }

            if (StateOf(t) != InnerWireFlattenState.Loose)
            {
                invalidPath = false;
                Debug.LogError(
                    $"[{nameof(InnerWireFlatten)}] '{wire.name}' tidak kembali Loose setelah " +
                    $"drop tidak valid.", wire);
            }
        }

        pass &= Check("drop di luar zone -> reset + Loose", invalidPath, string.Empty);

        // ---- 10, 11 : scale & rotation beku sepanjang jalur di atas. ----
        bool scaleFrozen = true;
        bool rotationFrozen = true;

        foreach (GameObject wire in wires)
        {
            if (wire == null)
                continue;

            Transform t = wire.transform;

            if (t.localScale != scaleBefore[t])
            {
                scaleFrozen = false;
                Debug.LogError(
                    $"[{nameof(InnerWireFlatten)}] Scale '{wire.name}' berubah : " +
                    $"{scaleBefore[t]:F6} -> {t.localScale:F6}", wire);
            }

            if (t.localRotation != rotationBefore[t])
            {
                rotationFrozen = false;
                Debug.LogError(
                    $"[{nameof(InnerWireFlatten)}] Rotation '{wire.name}' berubah — " +
                    $"milestone ini tidak memakai rotation.", wire);
            }
        }

        pass &= Check("localScale wire beku", scaleFrozen, string.Empty);
        pass &= Check("localRotation wire beku", rotationFrozen, string.Empty);

        // ---- Pulihkan posisi & status : Self Check tidak meninggalkan jejak. ----
        foreach (KeyValuePair<Transform, Vector3> entry in positionBefore)
            entry.Key.position = entry.Value;

        state.Clear();
        foreach (KeyValuePair<Transform, InnerWireFlattenState> entry in savedState)
            state[entry.Key] = entry.Value;

        dragOrigin.Clear();
        foreach (KeyValuePair<Transform, Vector3> entry in savedOrigin)
            dragOrigin[entry.Key] = entry.Value;

        // ---- 6, 7, 17 : endpoint & state kabel tidak tersentuh. ----
        pass &= Check(
            $"endpoint {(point != null ? point.Endpoint.ToString() : "?")} tidak bergerak",
            point == null || point.Position == pointBefore,
            point != null ? $"{pointBefore:F6} -> {point.Position:F6}" : "no CablePoint");

        pass &= Check(
            $"endpoint {(otherPoint != null ? otherPoint.Endpoint.ToString() : "?")} tidak bergerak",
            otherPoint == null || otherPoint.Position == otherBefore,
            otherPoint != null ? $"{otherBefore:F6} -> {otherPoint.Position:F6}" : "no other CablePoint");

        pass &= Check(
            "CablePoint.State tidak berubah",
            (point == null || point.State == stateBefore) &&
            (otherPoint == null || otherPoint.State == otherStateBefore),
            point != null ? $"{stateBefore} -> {point.State}" : "no CablePoint");

        // ---- 8 : panjang kabel. ----
        pass &= Check(
            "CablePathLength tidak berubah",
            controller == null || Mathf.Approximately(controller.CablePathLength, pathBefore),
            controller != null ? $"{pathBefore:F6} -> {controller.CablePathLength:F6}" : "no CableController");

        // ---- 9 : trim jacket. ----
        pass &= Check(
            "CableJacketTrim tidak berubah",
            trim == null ||
            (Mathf.Approximately(trim.AchievedTrim, trimABefore) &&
             Mathf.Approximately(trim.AchievedTrimB, trimBBefore) &&
             trim.BandCount == bandBefore &&
             trim.BoundaryRing == boundaryABefore &&
             trim.BoundaryRingB == boundaryBBefore),
            trim != null
                ? $"trimA={trim.AchievedTrim:F6}/{trimABefore:F6}  band={trim.BandCount}/{bandBefore}"
                : "no CableJacketTrim");

        Debug.Log(
            $"[{nameof(InnerWireFlatten)}] Self Check : {(pass ? "PASS" : "FAIL")}  " +
            $"(status pemain dipulihkan : flattened={FlattenedCount}/8)",
            this);
    }

    private static bool HasFieldOfType(System.Type owner, System.Type fieldType) =>
        System.Array.Exists(
            owner.GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public),
            f => f.FieldType == fieldType);

    /// <summary>
    /// Memeriksa jalur flatten OTOMATIS (yang dipakai button), terpisah dari
    /// Self Check di atas yang memeriksa jalur drag manual.
    ///
    /// Dijalankan lewat API yang sesungguhnya — <see cref="PrepareFlatten"/>,
    /// <see cref="ApplyFlatten"/>, <see cref="RestorePreFlatten"/> — dan tetap
    /// BUTA WARNA : yang diperiksa hanya bahwa barisan hasil flatten berbeda dari
    /// urutan wire terdaftar. Apakah barisan itu benar menurut T568 bukan urusan
    /// komponen ini; itu diperiksa Self Check InnerWireStageController.
    /// </summary>
    [ContextMenu("Flatten/Self Check Auto Flatten")]
    private void SelfCheckAutoFlatten()
    {
        bool pass = true;

        if (wiresView == null)
        {
            Debug.LogError($"[{nameof(InnerWireFlatten)}] CableInnerWiresView belum diassign.", this);
            return;
        }

        var wires = new List<Transform>();

        foreach (GameObject wire in wiresView.Wires)
            if (wire != null)
                wires.Add(wire.transform);

        pass &= Check("8 wire terdaftar", wires.Count == 8, $"{wires.Count}/8");

        var positionBefore = new Dictionary<Transform, Vector3>();
        var scaleBefore = new Dictionary<Transform, Vector3>();
        var rotationBefore = new Dictionary<Transform, Quaternion>();

        foreach (Transform wire in wires)
        {
            positionBefore[wire] = wire.position;
            scaleBefore[wire] = wire.localScale;
            rotationBefore[wire] = wire.localRotation;
        }

        var savedState = new Dictionary<Transform, InnerWireFlattenState>(state);

        // Tidak ada dependency Puzzle/UI/Ordering/InputAction yang menyelinap
        // bersama API baru.
        pass &= Check(
            "flatten tidak mereferensi Ordering/Slot",
            !HasFieldOfType(typeof(InnerWireFlatten), typeof(InnerWireOrdering)) &&
            !HasFieldOfType(typeof(InnerWireFlatten), typeof(InnerWireSlot)) &&
            !HasFieldOfType(typeof(InnerWireFlatten), typeof(InnerWireSlot[])),
            string.Empty);

        pass &= Check(
            "flatten tidak mereferensi InnerWireStageController",
            !HasFieldOfType(typeof(InnerWireFlatten), typeof(InnerWireStageController)),
            string.Empty);

        bool prepareOk = PrepareFlatten();
        pass &= Check("PrepareFlatten berhasil", prepareOk, string.Empty);

        if (!prepareOk)
        {
            Debug.Log($"[{nameof(InnerWireFlatten)}] Auto Flatten Self Check : FAIL", this);
            return;
        }

        pass &= Check("IsPrepared setelah Prepare", IsPrepared, string.Empty);

        // t = 0 harus benar-benar pose awal, bukan sekadar dekat.
        ApplyFlatten(0f);

        bool atZero = true;

        foreach (Transform wire in wires)
            if (wire.position != positionBefore[wire])
                atZero = false;

        pass &= Check("ApplyFlatten(0) = pose awal", atZero, string.Empty);

        // Setengah jalan : gerakannya memang bertahap, bukan langsung melompat.
        ApplyFlatten(0.5f);

        bool midMoved = false;

        foreach (Transform wire in wires)
            if (wire.position != positionBefore[wire])
            {
                midMoved = true;
                break;
            }

        pass &= Check("ApplyFlatten(0.5) menggerakkan wire", midMoved, string.Empty);

        ApplyFlatten(1f);

        int inside = 0;

        foreach (Transform wire in wires)
            if (InZone(wire.position))
                inside++;

        pass &= Check(
            "8/8 wire berada di dalam zone",
            inside == wires.Count,
            $"{inside}/{wires.Count}");

        pass &= Check("IsComplete setelah flatten", IsComplete, $"{FlattenedCount} rapi");

        // Barisan hasil flatten tidak boleh sama dengan urutan terdaftar :
        // pembuat slot memakai urutan itu, jadi kesamaan berarti wire mendarat
        // persis di depan slotnya sendiri.
        Vector3 axis = LaneAxis.normalized;
        var ranked = new List<Transform>(wires);

        ranked.Sort((a, b) => Vector3.Dot(a.position, axis).CompareTo(Vector3.Dot(b.position, axis)));

        bool shuffled = false;

        for (int i = 0; i < ranked.Count; i++)
            if (ranked[i] != wires[i])
            {
                shuffled = true;
                break;
            }

        pass &= Check(
            "barisan flatten != urutan wire terdaftar",
            shuffled,
            shuffled ? string.Empty : "identity permutation — shuffle tidak bekerja");

        // Hanya position yang boleh ditulis.
        bool scaleFrozen = true;
        bool rotationFrozen = true;

        foreach (Transform wire in wires)
        {
            if (wire.localScale != scaleBefore[wire])
            {
                scaleFrozen = false;
                Debug.LogError(
                    $"[{nameof(InnerWireFlatten)}] Scale '{wire.name}' berubah saat flatten : " +
                    $"{scaleBefore[wire]:F6} -> {wire.localScale:F6}", wire);
            }

            if (wire.localRotation != rotationBefore[wire])
            {
                rotationFrozen = false;
                Debug.LogError(
                    $"[{nameof(InnerWireFlatten)}] Rotation '{wire.name}' berubah saat flatten.", wire);
            }
        }

        pass &= Check("localScale wire beku", scaleFrozen, string.Empty);
        pass &= Check("localRotation wire beku", rotationFrozen, string.Empty);

        // Restore harus memulangkan pose DAN status.
        RestorePreFlatten();

        pass &= Check("IsPrepared kembali false", !IsPrepared, string.Empty);

        bool restored = true;

        foreach (Transform wire in wires)
            if (wire.position != positionBefore[wire])
            {
                restored = false;
                Debug.LogError(
                    $"[{nameof(InnerWireFlatten)}] '{wire.name}' tidak kembali ke pose awal : " +
                    $"{positionBefore[wire]:F6} -> {wire.position:F6}", wire);
            }

        pass &= Check("RestorePreFlatten memulangkan pose", restored, string.Empty);

        bool stateRestored = true;

        foreach (Transform wire in wires)
        {
            savedState.TryGetValue(wire, out InnerWireFlattenState expected);

            if (StateOf(wire) != expected)
            {
                stateRestored = false;
                Debug.LogError(
                    $"[{nameof(InnerWireFlatten)}] Status '{wire.name}' tidak dipulihkan : " +
                    $"{expected} -> {StateOf(wire)}", wire);
            }
        }

        pass &= Check("RestorePreFlatten memulangkan status", stateRestored, string.Empty);

        Debug.Log(
            $"[{nameof(InnerWireFlatten)}] Auto Flatten Self Check : {(pass ? "PASS" : "FAIL")}  " +
            $"(flattened={FlattenedCount})",
            this);
    }

    /// <summary>Endpoint pasangan, dicari lewat CableController yang memegang keduanya.</summary>
    private static CablePoint FindOtherEndpoint(CablePoint point)
    {
        if (point == null)
            return null;

        CableController controller = HitResolver.Resolve<CableController>(point.transform);

        if (controller == null)
            return null;

        foreach (CablePoint candidate in controller.GetComponentsInChildren<CablePoint>(true))
            if (candidate != point)
                return candidate;

        return null;
    }

    private static bool Check(string label, bool condition, string detail)
    {
        string suffix = string.IsNullOrEmpty(detail) ? string.Empty : $"  ({detail})";

        if (condition)
            Debug.Log($"  PASS  {label}{suffix}");
        else
            Debug.LogWarning($"  FAIL  {label}{suffix}");

        return condition;
    }
#endif
}
