using UnityEngine;

/// <summary>
/// Menggeser satu inner wire dengan pointer. Satu komponen menangani kedelapan
/// wire : yang menentukan wire mana adalah collider yang kena raycast, bukan
/// script per wire.
///
/// Ditaruh di module Tools, bukan di Cable : ini interaction, dan Cable tidak
/// boleh tahu soal pointer. Dependency tetap satu arah —
/// InnerWireDragTool -> CableInnerWiresView -> (daftar wire).
///
/// Yang digeser hanya <see cref="Transform.position"/> wire. localScale dan mesh
/// tidak pernah disentuh, jadi panjang wire fixed. CablePoint, CableController,
/// CableJacketTrim, dan ACC_Cable tidak ikut tersentuh sama sekali.
///
/// Aturan penempatan TIDAK ada di sini. Tool ini hanya tahu satu hal : pointer
/// ditekan, ditahan, dilepas. Pergeseran wire lain saat dilewati (live reorder)
/// dan pencatatan drop tetap urusan <see cref="InnerWireOrdering"/> — tanpa
/// snap, tanpa reset : susunan hasil seret pemain dibiarkan apa adanya, dan
/// validasi akhir menunggu modul LAN Tester.
///
/// GATE TAHAP. Sebelum apa pun dijalankan, <see cref="InnerWireStageController"/>
/// ditanya apakah sorting sudah boleh. Selama kabel masih acak (Messy), selagi
/// tangan meratakan (Flattening), dan setelah selesai (Completed), klik ditolak
/// di baris pertama — jadi <c>InnerWireOrdering.Grab</c> tidak pernah terpanggil
/// dan collider wire boleh tetap seperti desain yang sudah ada tanpa risiko
/// men-trigger sorting secara tidak sengaja. Gate hanya menolak atau meneruskan;
/// ia tidak mengubah cara drag bekerja.
///
/// SUMBU DRAG. Saat drag berada di bawah <see cref="InnerWireOrdering"/>
/// (sorting), gerak wire dikunci ke sumbu lateral barisan wire — dibaca dari
/// Ordering lewat <c>TryGetLaneAxis</c>, bukan konstanta world axis, jadi ikut
/// benar saat kabel dirotasi. Di luar sorting (endpoint tanpa aturan, barisan
/// tidak valid), drag tetap bebas pada bidang penuh seperti sebelumnya.
///
/// Endpoint tanpa InnerWireOrdering (mis. B pada milestone ini) tidak punya
/// tahap untuk dijaga, jadi wire-nya tetap bebas digeser seperti sebelumnya.
/// </summary>
public class InnerWireDragTool : MonoBehaviour, ITool
{
    [Header("References")]
    [Tooltip("Kosong = Camera.main dipakai.")]
    [SerializeField] private Camera mainCamera;
    [Tooltip("Sumber daftar 8 wire. Kosong = dicari dari collider yang kena " +
             "raycast lewat HitResolver.")]
    [SerializeField] private CableInnerWiresView wiresView;

    [Header("Drag Plane")]
    [Tooltip("Drag pada plane horizontal setinggi wire. Matikan untuk plane " +
             "yang menghadap kamera — biasanya lebih enak untuk wire yang " +
             "keluar mendatar dari ujung kabel.")]
    [SerializeField] private bool horizontalPlane = false;

    public ToolState Id => ToolState.CopperTool;

    /// <summary>
    /// Normal bidang tempat wire digeser. Wire TIDAK bisa bergerak sepanjang
    /// sumbu ini — dan itu menentukan area mana yang mungkin dicapai.
    ///
    /// Dipublikasikan (read-only) supaya pemeriksaan keterjangkauan bisa
    /// dilakukan di Editor, sebelum Play Mode, alih-alih terlihat sebagai
    /// "drop selalu gagal" saat dites. Tidak mengubah perilaku drag.
    /// </summary>
    public Vector3 DragPlaneNormal
    {
        get
        {
            if (horizontalPlane)
                return Vector3.up;

            Camera camera = mainCamera != null ? mainCamera : Camera.main;

            return camera != null ? -camera.transform.forward : Vector3.up;
        }
    }

    private Transform dragged;
    private Plane dragPlane;
    private Vector3 grabOffset;

    // Penerima drop untuk drag yang sedang berjalan. Dipilih sekali saat grab —
    // bukan per frame — dan tanpa raycast tambahan : HitResolver hanya
    // menelusuri hierarchy.
    private InnerWireOrdering ordering;

    // Dicatat saat grab, dibandingkan saat release : bukti panjang & scale wire
    // tidak berubah gara-gara drag.
    private Vector3 grabScale;

    // Posisi wire saat grab : baseline komponen tegak-lurus sumbu lane selama
    // drag sorting dikunci (lihat Update). Local, tidak diserialisasi.
    private Vector3 grabPosition;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void OnDisable()
    {
        dragged = null;
        ordering = null;
    }

    /// <summary>
    /// Dipanggil ToolInput pada frame pointer ditekan. Hanya memilih wire —
    /// pergerakannya di <see cref="Update"/>, karena ITool tidak punya konsep
    /// "sedang ditahan".
    /// </summary>
    public bool Execute(RaycastHit hit)
    {
        CableInnerWiresView view = wiresView != null
            ? wiresView
            : HitResolver.Resolve<CableInnerWiresView>(hit);

        if (view == null)
        {
            Debug.Log(
                $"[{nameof(InnerWireDragTool)}] REJECT '{hit.collider.name}' — " +
                $"CableInnerWiresView tidak ketemu dari " +
                $"'{hit.collider.transform.name}'. Komponennya belum dipasang, " +
                $"atau tidak ada di jalur parent collider ini.",
                this);
            return false;
        }

        Transform wire = view.ResolveWire(hit.collider.transform);

        if (wire == null)
        {
            Debug.Log(
                $"[{nameof(InnerWireDragTool)}] REJECT '{hit.collider.name}' — " +
                $"tidak ada parent-nya yang terdaftar di Inner Wires. " +
                $"assigned={view.AssignedWireNames()}",
                this);
            return false;
        }

        // GATE TAHAP. Diperiksa sebelum apa pun ditulis dan sebelum Ordering
        // disentuh : selama sorting belum dibuka, klik ditolak di sini sehingga
        // wire tidak bergerak sedikit pun dan Grab() tidak pernah terpanggil.
        //
        // Governs() wajib : HitResolver sengaja menembus cabang sibling, jadi
        // dari view endpoint B ia juga menemukan stage controller milik endpoint
        // A. Tanpa uji itu, tahap A akan memblokir drag wire B.
        //
        // Tanpa stage controller (endpoint yang belum punya tahap), gate tidak
        // ikut campur — perilakunya persis seperti sebelumnya.
        InnerWireStageController stage =
            HitResolver.Resolve<InnerWireStageController>(view.transform);

        if (stage != null && stage.Governs(view) && !stage.CanSort)
        {
            Debug.Log(
                $"[{nameof(InnerWireDragTool)}] REJECT '{wire.name}' — " +
                $"stage={stage.Stage}, sorting belum dibuka. " +
                $"Tekan button FLATTEN CABLE lebih dulu.",
                this);
            return false;
        }

        // Jacket masih utuh : wire memang ada (demi tampilan), tapi belum boleh
        // ditarik. Guard-nya di sini, bukan dengan mematikan collider —
        // dua syarat lain (mode UseTool & tool aktif) sudah dijamin ToolInput,
        // jadi cukup satu syarat ini yang perlu diperiksa.
        CablePoint point = view.Point;

        if (point != null && point.State == CableState.Intact)
        {
            Debug.Log(
                $"[{nameof(InnerWireDragTool)}] REJECT '{wire.name}' — " +
                $"CablePoint {point.Endpoint} masih {point.State}, jacket belum dilepas.",
                this);
            return false;
        }

        dragged = wire;
        grabScale = wire.localScale;
        grabPosition = wire.position;

        // Ordering-nya dicari dari view, bukan dari collider, supaya tidak
        // bergantung pada letak collider di hierarchy. Grab() yang menolak
        // (mis. wire milik endpoint lain) membuat wire kembali ke drag bebas
        // seperti sebelumnya — perilaku existing, tidak diubah.
        ordering = HitResolver.Resolve<InnerWireOrdering>(view.transform);

        if (ordering != null && !ordering.Grab(wire))
            ordering = null;

        dragPlane = horizontalPlane
            ? new Plane(Vector3.up, wire.position)
            : new Plane(-mainCamera.transform.forward, wire.position);

        // Wire tidak melompat ke titik kursor saat pertama disentuh.
        grabOffset = ProjectOnPlane(PointerRay(), out Vector3 grabPoint)
            ? wire.position - grabPoint
            : Vector3.zero;

        Debug.Log(
            $"[{nameof(InnerWireDragTool)}] GRAB '{wire.name}'  " +
            $"pos={wire.position:F4}  localScale={grabScale:F4}",
            wire);

        return true;
    }

    // Input tetap milik ToolInput : yang dibaca di sini hanya posisi pointer
    // dan status tekan, memakai action yang sama lewat ToolInput. Tidak ada
    // InputAction baru yang dibuat komponen ini.
    private void Update()
    {
        if (dragged == null)
            return;

        if (!InputModeService.Is(InputMode.UseTool))
        {
            Release();
            return;
        }

        if (!ToolInput.PointerHeld)
        {
            Release();
            return;
        }

        // Hanya position. localScale, localRotation, dan mesh tidak disentuh,
        // jadi wire bergeser tanpa memanjang.
        if (ProjectOnPlane(PointerRay(), out Vector3 target))
        {
            Vector3 free = target + grabOffset;

            // Drag sorting dikunci ke sumbu lane : hanya komponen lateral
            // pointer yang berlaku; maju/mundur dan atas/bawah wire tetap di
            // posisi saat grab. Di luar sorting (tanpa konteks lane valid),
            // drag tetap bebas seperti sebelumnya.
            if (ordering != null && ordering.TryGetLaneAxis(dragged, out Vector3 laneAxis))
                dragged.position =
                    grabPosition + laneAxis * Vector3.Dot(free - grabPosition, laneAxis);
            else
                dragged.position = free;

            // Live reorder : wire lain menyisih saat dilewati. Tanpa Ordering
            // (endpoint tanpa aturan), drag tetap bebas seperti sebelumnya.
            ordering?.NotifyDragged(dragged);
        }
    }

    private void Release()
    {
        bool scaleFrozen = dragged.localScale == grabScale;

        Debug.Log(
            $"[{nameof(InnerWireDragTool)}] RELEASE '{dragged.name}'  " +
            $"pos={dragged.position:F4}  " +
            $"localScale={dragged.localScale:F4}/{grabScale:F4}  " +
            $"scaleFrozen={scaleFrozen}",
            dragged);

        if (!scaleFrozen)
            Debug.LogWarning(
                $"[{nameof(InnerWireDragTool)}] Scale '{dragged.name}' berubah — " +
                $"ada komponen lain yang menulis localScale.", dragged);

        // Snap / reset bukan urusan tool. Penerimanya sudah dipilih saat grab.
        // Tanpa Ordering, wire tetap di tempat ia dilepas.
        ordering?.Drop(dragged);

        dragged = null;
        ordering = null;
    }

    private bool ProjectOnPlane(Ray ray, out Vector3 point)
    {
        if (dragPlane.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    private Ray PointerRay() => mainCamera.ScreenPointToRay(ToolInput.PointerPosition);
}
