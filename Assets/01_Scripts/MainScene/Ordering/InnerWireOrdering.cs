using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Status penempatan satu inner wire. Terpisah total dari CablePoint.State.</summary>
public enum InnerWirePlacement
{
    Loose,
    Placed
}

/// <summary>
/// Susunan manual 8 inner wire di SATU endpoint, plus pergeseran lane saat drag.
///
/// PENEMPATAN MANUAL : tidak ada snap, tidak ada reset, tidak ada penilaian
/// yang mengubah posisi saat drop. Wire ditinggal persis di titik pelepasannya
/// dan susunan hasil seret pemain adalah susunan yang resmi — kesalahan memang
/// dibiarkan salah sampai pemain memperbaikinya sendiri dengan menyeret ulang.
/// Validasi akhir menunggu modul LAN Tester, bukan interaksi drag/drop ini.
/// Slot tetap dipertahankan sebagai geometri pemeriksaan (info drop sekarang,
/// bahan LAN Tester nanti), bukan sebagai target yang menarik wire.
///
/// LIVE REORDER : saat wire yang dipegang menyeberangi wire lain, wire yang
/// dilewati pindah ke posisi yang barusan ditinggalkan (gap) — seperti
/// menyisipkan item pada daftar horizontal. Urutan relatif wire lain terjaga.
///
/// Pemilik aturan, bukan pemilik data : daftar 8 wire tetap milik
/// <see cref="CableInnerWiresView"/> dan hanya dibaca dari sana, sehingga tidak
/// ada daftar wire kedua yang bisa desync. Yang disimpan di sini hanya hal yang
/// memang milik ordering — slot dan status penempatan.
///
/// Diletakkan di module Ordering, bukan Cable : Cable Module hanya bertanggung
/// jawab atas CableInput -> CablePoint -> CableController -> ACC_Cable. Dependency
/// tetap satu arah — InnerWireDragTool -> InnerWireOrdering -> CableInnerWiresView.
/// Komponen ini tidak membaca input, tidak meraycast, tidak menulis
/// CablePoint.State maupun CablePoint.Position, dan tidak pernah menyentuh
/// localScale/mesh wire.
///
/// Milestone ini sengaja hanya endpoint A. Wire dari endpoint lain ditolak lewat
/// <see cref="Owns"/>, jadi B tetap memakai drag bebas seperti sebelumnya.
///
/// PENEMPATAN : pasang komponen ini pada GameObject endpoint (mis. "A"), yaitu
/// ancestor dari GameObject yang memegang <see cref="CableInnerWiresView"/>.
/// InnerWireDragTool menemukannya lewat HitResolver dari view — tanpa raycast
/// tambahan, hanya menelusuri hierarchy sekali per grab. Bila komponen ini
/// dipasang di parent yang dibagi kedua ujung, wire ujung yang salah akan
/// ditolak <see cref="Owns"/> dan kembali ke drag bebas, bukan salah snap.
/// </summary>
public class InnerWireOrdering : MonoBehaviour
{
    [Header("Reference")]
    [Tooltip("Sumber daftar 8 wire endpoint ini. Daftarnya tidak diduplikasi di sini.")]
    [SerializeField] private CableInnerWiresView wiresView;

    [Header("Slots")]
    [Tooltip("8 slot tujuan. Tiap slot punya satu warna, dan warnanya wajib unik. " +
             "Slot tidak boleh menjadi child dari wire mana pun.")]
    [SerializeField] private InnerWireSlot[] slots = new InnerWireSlot[8];

    // Runtime saja : tidak diserialisasi supaya status prototype tidak ikut
    // tersimpan ke scene. Sejak penempatan manual tidak ada lagi penulis
    // Placed — pembacaannya (IsComplete/PlacedCount) menunggu alur validasi
    // LAN Tester.
    private readonly Dictionary<Transform, InnerWirePlacement> placement =
        new Dictionary<Transform, InnerWirePlacement>();

    // ---- Live reorder (runtime saja) ----
    // Modelnya menyisipkan item pada daftar horizontal : wire yang dipegang
    // meninggalkan satu "gap", dan tiap kali ia menyeberangi wire lain, wire
    // itu berpindah ke gap sementara gap berpindah ke posisi lamanya. Hanya
    // position yang ditulis, hanya sepanjang arah lane.
    private readonly List<Transform> lane = new List<Transform>();
    private Transform reorderDragged;
    private Vector3 laneOrigin;
    private Vector3 laneAxis = Vector3.right;
    private float gapLateral;
    private int insertIndex;

    public CableInnerWiresView WiresView => wiresView;

    public IReadOnlyList<InnerWireSlot> Slots => slots;

    /// <summary>
    /// Satu drop selesai diproses : wire mana, dan apakah posisi pelepasannya
    /// berimpit dengan slot warnanya — informasi saja, tidak ada tindakan yang
    /// mengikuti dan posisi wire tidak diubah oleh event ini.
    ///
    /// Ini satu-satunya jalan keluar dari komponen ini. Dipakai pemilik tahap
    /// (Puzzle) untuk memajukan Flattened -> Sorting tanpa mem-polling per
    /// frame. Penilaian benar/salah yang sesungguhnya menunggu LAN Tester.
    /// </summary>
    public event Action<Transform, bool> OnDropResolved;

    /// <summary>
    /// Seluruh wire terdaftar sudah <see cref="InnerWirePlacement.Placed"/>.
    ///
    /// Membaca status yang SUDAH ada (<see cref="placement"/>) — bukan
    /// pemeriksaan urutan kedua. Sejak penempatan manual, drag/drop tidak lagi
    /// menulis Placed, jadi selama alur validasi (LAN Tester) belum ada yang
    /// mengisinya, nilai ini tetap false — memang begitu desainnya.
    ///
    /// False bila belum ada wire terdaftar : "nol wire" bukan "sudah selesai".
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

                if (PlacementOf(wire.transform) != InnerWirePlacement.Placed)
                    return false;
            }

            return registered > 0;
        }
    }

    /// <summary>Jumlah wire terdaftar yang sudah masuk slotnya.</summary>
    public int PlacedCount
    {
        get
        {
            if (wiresView == null)
                return 0;

            int count = 0;

            foreach (GameObject wire in wiresView.Wires)
                if (wire != null && PlacementOf(wire.transform) == InnerWirePlacement.Placed)
                    count++;

            return count;
        }
    }

    /// <summary>Apakah <paramref name="wire"/> memang salah satu wire endpoint ini.</summary>
    public bool Owns(Transform wire) =>
        wire != null && wiresView != null && wiresView.ResolveWire(wire) == wire;

    public InnerWirePlacement PlacementOf(Transform wire) =>
        wire != null && placement.TryGetValue(wire, out InnerWirePlacement state)
            ? state
            : InnerWirePlacement.Loose;

    /// <summary>
    /// Dicatat saat wire diambil, sekaligus menyiapkan konteks live-reorder :
    /// lane (wire lain, terurut menyamping), arah lane, dan gap di posisi
    /// yang ditinggalkan wire ini.
    /// </summary>
    public bool Grab(Transform wire)
    {
        if (!Owns(wire))
            return false;

        BuildReorder(wire);

        Debug.Log($"[InnerWire] GRAB '{wire.name}'", wire);

        return true;
    }

    /// <summary>
    /// Dipanggil saat pointer dilepas. PENEMPATAN MANUAL : tidak ada snap, tidak
    /// ada reset, tidak ada penilaian yang mengubah apa pun — wire dibiarkan
    /// persis di titik pelepasannya. Kesalahan diperbaiki pemain sendiri dengan
    /// menyeret ulang; validasi akhir menunggu modul LAN Tester.
    /// </summary>
    public bool Drop(Transform wire)
    {
        if (!Owns(wire))
            return false;

        // Informasi saja — tidak ada tindakan yang mengikuti.
        InnerWireSlot slot = SlotAt(wire.position);
        bool onMatchSlot = slot != null && Accepts(slot, wire);

        Debug.Log(
            $"[InnerWire] DROP '{wire.name}' → manual, dibiarkan di tempat. " +
            $"slot='{(slot != null ? slot.name : "none")}' info={(onMatchSlot ? "cocok" : "belum")}",
            wire);

        ClearReorder();

        OnDropResolved?.Invoke(wire, onMatchSlot);

        return true;
    }

    /// <summary>
    /// Dipanggil InnerWireDragTool tiap frame selama wire digeser. Ketika wire
    /// yang dipegang menyeberangi wire lain, wire yang dilewati berpindah ke
    /// gap — menyisih memberi ruang, seperti menyisipkan item pada daftar
    /// horizontal. Wire yang tidak dilewati tidak bergerak, jadi urutan relatif
    /// mereka terjaga. Histeresis alami : membatalkan pergeseran menuntut wire
    /// menyeberangi wire yang sama di posisi barunya.
    /// </summary>
    public void NotifyDragged(Transform wire)
    {
        if (reorderDragged != wire || lane.Count == 0)
            return;

        float t = LateralOf(wire);

        // Bergerak ke kiri : wire yang barusan dilewati menyisih ke kanan.
        while (insertIndex > 0 && t < LateralOf(lane[insertIndex - 1]))
        {
            ShiftIntoGap(lane[insertIndex - 1]);
            insertIndex--;
        }

        // Bergerak ke kanan : wire yang barusan dilewati menyisih ke kiri.
        while (insertIndex < lane.Count && t > LateralOf(lane[insertIndex]))
        {
            ShiftIntoGap(lane[insertIndex]);
            insertIndex++;
        }
    }

    /// <summary>
    /// Sumbu lateral barisan wire untuk drag yang sedang berjalan, bila ada.
    /// Dipakai <see cref="InnerWireDragTool"/> supaya wire yang dipegang hanya
    /// bergerak sepanjang barisannya — komponen maju/mundur dan atas/bawah
    /// tetap dipertahankan tool dari posisi saat grab.
    ///
    /// False bila drag ini tidak punya konteks reorder yang valid (wire bukan
    /// yang sedang dipegang, barisan terlalu sedikit / berimpit, atau sudah
    /// di-Drop) — pemanggil lalu memakai drag bebas seperti sebelumnya.
    /// Sumbunya bukan konstanta : dihitung dari posisi wire sesungguhnya saat
    /// <see cref="Grab"/>, jadi ikut benar saat kabel dirotasi.
    /// </summary>
    public bool TryGetLaneAxis(Transform wire, out Vector3 axis)
    {
        axis = laneAxis;
        return reorderDragged == wire && lane.Count > 0;
    }

    /// <summary>
    /// Menyiapkan lane untuk satu drag : wire lain diurutkan menyamping, arah
    /// lane diambil dari pasangan wire lain yang berjarak paling jauh (hasil
    /// flatten memang berjajar), dan gap diletakkan di posisi awal wire yang
    /// dipegang. Barisan yang terlalu sedikit / berimpit mematikan reorder
    /// untuk drag ini — wire tetap bisa digeser bebas.
    /// </summary>
    private void BuildReorder(Transform dragged)
    {
        ClearReorder();

        if (wiresView == null)
            return;

        foreach (GameObject wire in wiresView.Wires)
        {
            if (wire == null || wire.transform == dragged)
                continue;

            lane.Add(wire.transform);
        }

        if (lane.Count < 2)
        {
            lane.Clear();
            return;
        }

        Transform a = lane[0];
        Transform b = lane[1];
        float farthest = -1f;

        for (int i = 0; i < lane.Count; i++)
            for (int j = i + 1; j < lane.Count; j++)
            {
                float squared = (lane[j].position - lane[i].position).sqrMagnitude;

                if (squared > farthest)
                {
                    farthest = squared;
                    a = lane[i];
                    b = lane[j];
                }
            }

        if (farthest < 1e-8f)
        {
            lane.Clear();
            return;
        }

        laneAxis = (b.position - a.position).normalized;
        laneOrigin = dragged.position;

        lane.Sort(CompareLateral);

        float t = LateralOf(dragged);

        while (insertIndex < lane.Count && LateralOf(lane[insertIndex]) < t)
            insertIndex++;

        gapLateral = t;
        reorderDragged = dragged;
    }

    /// <summary>Wire berpindah ke gap, gap berpindah ke posisi lama wire.</summary>
    private void ShiftIntoGap(Transform wire)
    {
        float from = LateralOf(wire);

        wire.position += laneAxis * (gapLateral - from);
        gapLateral = from;

        Debug.Log($"[InnerWire] SHIFT '{wire.name}' menyisih ke gap", wire);
    }

    private void ClearReorder()
    {
        reorderDragged = null;
        lane.Clear();
        laneAxis = Vector3.right;
        laneOrigin = Vector3.zero;
        gapLateral = 0f;
        insertIndex = 0;
    }

    private float LateralOf(Transform wire) =>
        Vector3.Dot(wire.position - laneOrigin, laneAxis);

    private int CompareLateral(Transform x, Transform y) =>
        LateralOf(x).CompareTo(LateralOf(y));

    private void OnDisable()
    {
        // Drag pasti terputus saat komponen mati ; konteks reorder tidak boleh
        // bocor ke grab berikutnya.
        ClearReorder();
    }

    /// <summary>Slot pertama yang memuat titik ini. Null = di luar semua slot.</summary>
    public InnerWireSlot SlotAt(Vector3 worldPoint)
    {
        if (slots == null)
            return null;

        foreach (InnerWireSlot slot in slots)
            if (slot != null && slot.Contains(worldPoint))
                return slot;

        return null;
    }

    /// <summary>Slot yang seharusnya menerima wire ini. Null = warnanya tidak dikenal.</summary>
    public InnerWireSlot SlotFor(Transform wire)
    {
        if (slots == null || wire == null)
            return null;

        if (!InnerWireColor.TryResolve(wire.name, out CableColor color))
            return null;

        foreach (InnerWireSlot slot in slots)
            if (slot != null && slot.AcceptedColor == color)
                return slot;

        return null;
    }

    private static bool Accepts(InnerWireSlot slot, Transform wire) =>
        InnerWireColor.TryResolve(wire.name, out CableColor color) && slot.AcceptedColor == color;

#if UNITY_EDITOR
    [Header("Slot Creator (Editor)")]
    [Tooltip("Induk 8 slot yang dibuat. Kosong = GameObject komponen ini. " +
             "JANGAN diisi wire mana pun : slot harus diam saat wire digerakkan.")]
    [SerializeField] private Transform slotParent;
    [Tooltip("Jarak slot dari titik acuan, sepanjang arah keluar kabel (meter). " +
             "Harus lebih jauh dari posisi wire supaya wire benar-benar ditarik.")]
    [SerializeField] private float slotForwardOffset = 0.1f;
    [Tooltip("Jarak antar slot, menyamping terhadap arah keluar kabel (meter). " +
             "Harus lebih besar dari 2x Slot Radius, kalau tidak slot saling tumpang tindih.")]
    [SerializeField] private float slotSpacing = 0.008f;
    [Tooltip("Radius area tangkap tiap slot (meter). Jaga di bawah setengah Slot Spacing.")]
    [SerializeField] private float slotRadius = 0.003f;

    /// <summary>
    /// Membuat 8 slot sekaligus, berjajar tegak lurus arah keluar kabel, dengan
    /// urutan warna mengikuti urutan wire yang sudah terdaftar di
    /// <see cref="CableInnerWiresView"/> — bukan urutan T568, karena standar itu
    /// belum didefinisikan di project ini.
    ///
    /// MENGUBAH SCENE. Terdaftar di Undo, jadi bisa dibatalkan dengan Ctrl+Z.
    /// </summary>
    [ContextMenu("Ordering/Create Slots")]
    private void CreateSlots()
    {
        if (wiresView == null)
        {
            Debug.LogError($"[{nameof(InnerWireOrdering)}] CableInnerWiresView belum diassign.", this);
            return;
        }

        IReadOnlyList<GameObject> wires = wiresView.Wires;
        Transform parent = slotParent != null ? slotParent : transform;

        CablePoint point = wiresView.Point;
        Quaternion frame = point != null ? point.AttachmentRotation : Quaternion.identity;
        Vector3 anchor = point != null ? point.Position : parent.position;

        InnerWireSlot[] created = new InnerWireSlot[wires.Count];
        int index = 0;

        for (int i = 0; i < wires.Count; i++)
        {
            if (wires[i] == null)
                continue;

            if (!InnerWireColor.TryResolve(wires[i].name, out CableColor color))
            {
                Debug.LogError(
                    $"[{nameof(InnerWireOrdering)}] Nama wire '{wires[i].name}' tidak dikenal " +
                    $"InnerWireColor — slot untuk wire ini tidak dibuat.", wires[i]);
                continue;
            }

            var go = new GameObject($"Slot_{wires[i].name}");
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Inner Wire Slots");

            go.transform.SetParent(parent, false);

            // Berjajar simetris di sekitar sumbu kabel, digeser keluar sepanjang
            // arah attachment. Dinyatakan di dalam frame endpoint supaya tetap
            // benar saat kabel diputar.
            float lateral = (i - (wires.Count - 1) * 0.5f) * slotSpacing;
            go.transform.position = anchor + frame * new Vector3(lateral, 0f, slotForwardOffset);

            SphereCollider area = go.AddComponent<SphereCollider>();
            area.radius = slotRadius;
            area.isTrigger = true;

            InnerWireSlot slot = go.AddComponent<InnerWireSlot>();
            slot.Configure(color, area);

            created[index++] = slot;
        }

        System.Array.Resize(ref created, index);

        UnityEditor.Undo.RecordObject(this, "Create Inner Wire Slots");
        slots = created;
        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log($"[{nameof(InnerWireOrdering)}] {index} slot dibuat di '{parent.name}'.", this);
    }

    [ContextMenu("Ordering/Log Status")]
    private void LogStatus()
    {
        string wireList = wiresView != null ? wiresView.AssignedWireNames() : "no view";
        string slotList = string.Empty;
        int assigned = 0;

        if (slots != null)
            foreach (InnerWireSlot slot in slots)
            {
                if (slot == null)
                    continue;

                assigned++;
                slotList += (slotList.Length > 0 ? ", " : string.Empty) +
                            $"{slot.name}:{slot.AcceptedColor}";
            }

        Debug.Log(
            $"[{nameof(InnerWireOrdering)}] wires={wireList}\n" +
            $"slots={assigned}/{(slots != null ? slots.Length : 0)} [{slotList}]",
            this);
    }

    /// <summary>
    /// Berjalan tanpa Play Mode. Selain memeriksa pendaftaran, ia benar-benar
    /// men-snap tiap wire ke slotnya lalu mengembalikannya, sehingga invarian
    /// yang diperiksa adalah invarian jalur yang sesungguhnya dipakai — bukan
    /// asumsi. Yang ditulis hanya position; scale/rotation/mesh tidak disentuh.
    /// </summary>
    [ContextMenu("Ordering/Self Check")]
    private void SelfCheck()
    {
        bool pass = true;

        if (wiresView == null)
        {
            Debug.LogError($"[{nameof(InnerWireOrdering)}] CableInnerWiresView belum diassign.", this);
            return;
        }

        IReadOnlyList<GameObject> wires = wiresView.Wires;

        // 1. 8 wire terdaftar.
        int wireCount = 0;
        foreach (GameObject wire in wires)
            if (wire != null)
                wireCount++;

        pass &= Check("8 wire terdaftar", wireCount == 8, $"{wireCount}/8");

        // 2. 8 slot terdaftar.
        int slotCount = 0;
        if (slots != null)
            foreach (InnerWireSlot slot in slots)
                if (slot != null)
                    slotCount++;

        pass &= Check("8 slot terdaftar", slotCount == 8, $"{slotCount}/8");

        // 3. Tidak ada identitas slot kembar.
        var seen = new Dictionary<CableColor, InnerWireSlot>();
        bool unique = true;

        if (slots != null)
            foreach (InnerWireSlot slot in slots)
            {
                if (slot == null)
                    continue;

                if (seen.TryGetValue(slot.AcceptedColor, out InnerWireSlot other))
                {
                    unique = false;
                    Debug.LogError(
                        $"[{nameof(InnerWireOrdering)}] Slot '{slot.name}' dan '{other.name}' " +
                        $"sama-sama menerima {slot.AcceptedColor}.", slot);
                    continue;
                }

                seen.Add(slot.AcceptedColor, slot);
            }

        pass &= Check("identitas slot unik", unique, $"{seen.Count} warna berbeda");

        // 4. Tiap slot punya area, dan tidak ada slot yang jadi child wire.
        bool areaOk = true;
        bool detached = true;

        if (slots != null)
            foreach (InnerWireSlot slot in slots)
            {
                if (slot == null)
                    continue;

                if (slot.Area == null)
                {
                    areaOk = false;
                    Debug.LogError($"[{nameof(InnerWireOrdering)}] Slot '{slot.name}' tanpa Collider.", slot);
                }

                foreach (GameObject wire in wires)
                {
                    if (wire == null)
                        continue;

                    if (slot.transform.IsChildOf(wire.transform))
                    {
                        detached = false;
                        Debug.LogError(
                            $"[{nameof(InnerWireOrdering)}] Slot '{slot.name}' adalah child dari " +
                            $"wire '{wire.name}' — slot akan ikut bergerak saat wire di-drag.", slot);
                    }
                }
            }

        pass &= Check("tiap slot punya Collider", areaOk, string.Empty);
        pass &= Check("slot bukan child wire", detached, string.Empty);

        // 4b. Slot tidak boleh saling tumpang tindih : SlotAt mengembalikan slot
        //     PERTAMA yang memuat titik, jadi area yang beririsan membuat drop
        //     yang benar bisa dinilai WRONG tergantung urutan array.
        bool separated = true;

        if (slots != null)
            for (int i = 0; i < slots.Length; i++)
                for (int j = i + 1; j < slots.Length; j++)
                {
                    if (slots[i] == null || slots[j] == null)
                        continue;

                    if (!slots[i].Contains(slots[j].SnapPosition) &&
                        !slots[j].Contains(slots[i].SnapPosition))
                        continue;

                    separated = false;
                    Debug.LogError(
                        $"[{nameof(InnerWireOrdering)}] Area slot '{slots[i].name}' dan " +
                        $"'{slots[j].name}' tumpang tindih — perbesar Slot Spacing atau " +
                        $"perkecil Slot Radius.", slots[i]);
                }

        pass &= Check("area slot tidak tumpang tindih", separated, string.Empty);

        // 5. Tiap wire punya tepat satu slot yang benar.
        bool matched = true;

        foreach (GameObject wire in wires)
        {
            if (wire == null)
                continue;

            if (!InnerWireColor.TryResolve(wire.name, out CableColor color))
            {
                matched = false;
                Debug.LogError(
                    $"[{nameof(InnerWireOrdering)}] Nama wire '{wire.name}' tidak dikenal " +
                    $"InnerWireColor.", wire);
                continue;
            }

            int hits = 0;

            if (slots != null)
                foreach (InnerWireSlot slot in slots)
                    if (slot != null && slot.AcceptedColor == color)
                        hits++;

            if (hits != 1)
            {
                matched = false;
                Debug.LogError(
                    $"[{nameof(InnerWireOrdering)}] Wire '{wire.name}' ({color}) cocok ke " +
                    $"{hits} slot, seharusnya tepat 1.", wire);
            }
        }

        pass &= Check("tiap wire tepat 1 slot", matched, string.Empty);

        // ---- Simulasi snap : bukti invarian pada jalur yang sebenarnya. ----
        CablePoint point = wiresView.Point;
        CableJacketTrim trim = point != null
            ? HitResolver.Resolve<CableJacketTrim>(point.transform)
            : null;
        CableController controller = point != null
            ? HitResolver.Resolve<CableController>(point.transform)
            : null;

        CablePoint otherPoint = FindOtherEndpoint(point);

        Vector3 pointBefore = point != null ? point.Position : Vector3.zero;
        Vector3 otherBefore = otherPoint != null ? otherPoint.Position : Vector3.zero;
        float trimABefore = trim != null ? trim.AchievedTrim : 0f;
        float trimBBefore = trim != null ? trim.AchievedTrimB : 0f;
        int bandBefore = trim != null ? trim.BandCount : 0;
        int boundaryBefore = trim != null ? trim.BoundaryRing : 0;
        float pathBefore = controller != null ? controller.CablePathLength : 0f;

        var scaleBefore = new Dictionary<Transform, Vector3>();
        var rotationBefore = new Dictionary<Transform, Quaternion>();
        var positionBefore = new Dictionary<Transform, Vector3>();

        foreach (GameObject wire in wires)
        {
            if (wire == null)
                continue;

            scaleBefore[wire.transform] = wire.transform.localScale;
            rotationBefore[wire.transform] = wire.transform.localRotation;
            positionBefore[wire.transform] = wire.transform.position;
        }

        foreach (GameObject wire in wires)
        {
            if (wire == null)
                continue;

            InnerWireSlot slot = SlotFor(wire.transform);

            if (slot != null)
                wire.transform.position = slot.SnapPosition;
        }

        bool scaleFrozen = true;
        bool rotationFrozen = true;
        bool landedInSlot = true;

        foreach (GameObject wire in wires)
        {
            if (wire == null)
                continue;

            Transform t = wire.transform;

            if (t.localScale != scaleBefore[t])
            {
                scaleFrozen = false;
                Debug.LogError(
                    $"[{nameof(InnerWireOrdering)}] Scale '{wire.name}' berubah saat snap : " +
                    $"{scaleBefore[t]:F6} -> {t.localScale:F6}", wire);
            }

            if (t.localRotation != rotationBefore[t])
            {
                rotationFrozen = false;
                Debug.LogError(
                    $"[{nameof(InnerWireOrdering)}] Rotation '{wire.name}' berubah saat snap.", wire);
            }

            // Titik snap harus benar-benar dianggap "di dalam" slotnya, kalau
            // tidak drop yang sah pun akan dinilai WRONG.
            InnerWireSlot slot = SlotFor(t);

            if (slot != null && !slot.Contains(t.position))
            {
                landedInSlot = false;
                Debug.LogError(
                    $"[{nameof(InnerWireOrdering)}] Posisi snap '{wire.name}' di luar area " +
                    $"slot '{slot.name}' — radius collider terlalu kecil.", slot);
            }
        }

        // Kembalikan ke posisi semula : Self Check tidak boleh meninggalkan jejak.
        foreach (KeyValuePair<Transform, Vector3> entry in positionBefore)
            entry.Key.position = entry.Value;

        pass &= Check("scale wire beku saat snap", scaleFrozen, string.Empty);
        pass &= Check("rotation wire beku saat snap", rotationFrozen, string.Empty);
        pass &= Check("titik snap di dalam slot", landedInSlot, string.Empty);

        bool endpointFrozen = point == null || point.Position == pointBefore;
        bool otherFrozen = otherPoint == null || otherPoint.Position == otherBefore;

        pass &= Check(
            $"endpoint {(point != null ? point.Endpoint.ToString() : "?")} tidak bergerak",
            endpointFrozen,
            point != null ? $"{pointBefore:F6} -> {point.Position:F6}" : "no CablePoint");

        pass &= Check(
            $"endpoint {(otherPoint != null ? otherPoint.Endpoint.ToString() : "?")} tidak bergerak",
            otherFrozen,
            otherPoint != null ? $"{otherBefore:F6} -> {otherPoint.Position:F6}" : "no other CablePoint");

        bool trimFrozen = trim == null ||
            (Mathf.Approximately(trim.AchievedTrim, trimABefore) &&
             Mathf.Approximately(trim.AchievedTrimB, trimBBefore) &&
             trim.BandCount == bandBefore &&
             trim.BoundaryRing == boundaryBefore);

        pass &= Check(
            "CableJacketTrim tidak terganggu",
            trimFrozen,
            trim != null
                ? $"trimA={trim.AchievedTrim:F6}/{trimABefore:F6} band={trim.BandCount}/{bandBefore}"
                : "no CableJacketTrim");

        bool meshFrozen = controller == null ||
            Mathf.Approximately(controller.CablePathLength, pathBefore);

        pass &= Check(
            "panjang kabel tidak berubah",
            meshFrozen,
            controller != null ? $"{controller.CablePathLength:F6}/{pathBefore:F6}" : "no CableController");

        // InnerWireDragTool tidak boleh menulis CablePoint : diperiksa di level
        // tipe, bukan diasumsikan. Tool hanya boleh menyentuh Transform wire.
        bool toolClean = !System.Array.Exists(
            typeof(InnerWireDragTool).GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public),
            f => f.FieldType == typeof(CablePoint));

        pass &= Check("InnerWireDragTool tidak memegang CablePoint", toolClean, string.Empty);

        Debug.Log($"[{nameof(InnerWireOrdering)}] Self Check : {(pass ? "PASS" : "FAIL")}", this);
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
