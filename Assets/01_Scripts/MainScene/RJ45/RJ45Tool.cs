using UnityEngine;

/// <summary>
/// Tool memindahkan SATU konektor RJ45 : grab → drag → lepas. Polanya sama
/// dengan <see cref="InnerWireDragTool"/> — <see cref="ITool.Execute"/> hanya
/// MEMEGANG konektor saat pointer ditekan, pergerakannya di <see cref="Update"/>,
/// dan keputusan memasang diambil dari POSISI konektor terhadap area
/// pemasangan endpoint.
///
/// Klik TIDAK langsung memasang. Pemasangan hanya terjadi saat konektor
/// berada di dalam area pemasangan (<see cref="RJ45Controller.IsInInsertionArea"/>)
/// DAN semua gate <see cref="RJ45Controller.TryInsert"/> lolos — dicek tiap
/// frame saat drag dan sekali lagi saat pointer dilepas. Gagal (mis. tahap
/// sorting belum dinyatakan selesai) berarti drag berlanjut ; konektor tidak
/// pernah menempel setengah jalan, dan urutan wire tidak tersentuh.
///
/// Di luar area, konektor ditinggal persis di titik pelepasan — tidak ada
/// auto-return, tidak ada snap.
///
/// Satu tool untuk semua konektor : konektor mana yang dipegang ditentukan
/// collider yang kena raycast (controller ada di root konektor itu sendiri),
/// jadi RJ45_A dan RJ45_B nanti memakai komponen yang sama tanpa wiring
/// tambahan di tool ini.
/// </summary>
public class RJ45Tool : MonoBehaviour, ITool
{
    [Header("References")]
    [Tooltip("Kosong = Camera.main dipakai.")]
    [SerializeField] private Camera mainCamera;

    public ToolState Id => ToolState.RJ45Tool;

    private Transform dragged;
    private RJ45Controller controller;
    private Plane dragPlane;
    private Vector3 grabOffset;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void OnDisable()
    {
        // Drag terputus saat tool mati. Release() sengaja TIDAK dipanggil :
        // memasang konektor hanya boleh lewat pelepasan pointer yang nyata
        // di dalam area pemasangan, bukan karena komponen dimatikan.
        dragged = null;
        controller = null;
    }

    /// <summary>
    /// Dipanggil ToolInput pada frame pointer ditekan : hanya MEMEGANG
    /// konektor, tidak memasang. True = konektor valid sedang dipegang.
    /// </summary>
    public bool Execute(RaycastHit hit)
    {
        RJ45Controller target = HitResolver.Resolve<RJ45Controller>(hit);

        if (target == null)
            return false;

        // Sudah terpasang / ter-crimp : tidak bisa dipegang lagi.
        if (target.Point == null || target.Point.State != RJ45State.None)
        {
            Debug.Log(
                $"[RJ45Tool] REJECT '{hit.collider.name}' — state konektor " +
                $"{target.Point?.State}, tidak bisa dipegang.",
                this);
            return false;
        }

        dragged = target.transform;
        controller = target;

        // Bidang drag menghadap kamera dan melalui titik pegang, jadi konektor
        // bergerak pada bidang yang sedang dilihat pemain.
        dragPlane = new Plane(-mainCamera.transform.forward, dragged.position);

        // Konektor tidak melompat ke titik kursor saat pertama disentuh.
        grabOffset = ProjectOnPlane(PointerRay(), out Vector3 grabPoint)
            ? dragged.position - grabPoint
            : Vector3.zero;

        Debug.Log($"[RJ45Tool] GRAB '{dragged.name}'  pos={dragged.position:F4}", dragged);

        return true;
    }

    // Input tetap milik ToolInput : yang dibaca di sini hanya posisi pointer
    // dan status tekan lewat action yang sama — tidak ada InputAction baru.
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

        if (!ProjectOnPlane(PointerRay(), out Vector3 target))
            return;

        // Hanya position root konektor. Child, wire, dan endpoint tidak
        // pernah ditulis oleh tool ini.
        dragged.position = target + grabOffset;

        // Pemasangan otomatis begitu konektor berada di area pemasangan DAN
        // semua gate TryInsert lolos (state endpoint, state konektor, tahap
        // sorting). TryInsert yang menolak tidak mengubah apa pun — drag
        // berlanjut sampai pemain melepas pointer.
        if (controller.IsInInsertionArea(dragged.position) && controller.TryInsert())
        {
            Debug.Log($"[RJ45Tool] ATTACHED '{dragged.name}' — terpasang saat drag.", dragged);
            dragged = null;
            controller = null;
        }
    }

    /// <summary>
    /// Pelepasan pointer. Pemeriksaan area terakhir tetap dijalankan di sini
    /// supaya konektor yang masuk zona lalu dilepas pada frame yang sama
    /// tidak terlewat. Di luar zona : konektor ditinggal di tempat ia
    /// dijatuhkan — tidak dikembalikan, tidak di-snap.
    /// </summary>
    private void Release()
    {
        if (dragged != null && controller != null &&
            controller.IsInInsertionArea(dragged.position) && controller.TryInsert())
        {
            Debug.Log($"[RJ45Tool] ATTACHED '{dragged.name}' — terpasang saat release.", dragged);
        }
        else if (dragged != null)
        {
            Debug.Log(
                $"[RJ45Tool] RELEASE '{dragged.name}' di luar area pemasangan — " +
                $"ditinggal di tempat  pos={dragged.position:F4}",
                dragged);
        }

        dragged = null;
        controller = null;
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
