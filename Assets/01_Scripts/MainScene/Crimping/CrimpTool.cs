using UnityEngine;

/// <summary>
/// Tool memindahkan SATU crimping tool : grab → drag → lepas. Polanya sama
/// dengan <see cref="RJ45Tool"/> (yang sendiri mengikuti InnerWireDragTool) —
/// <see cref="ITool.Execute"/> hanya MEMEGANG crimper saat pointer ditekan dan
/// pergerakannya di <see cref="Update"/>.
///
/// Tool ini TIDAK men-crimp. Ia murni memindahkan crimper ; keputusan men-crimp
/// diambil tombol UI (<c>CrimpButton</c>) yang membaca
/// <see cref="CrimpController.CanCrimpNow"/> lalu memanggil
/// <see cref="CrimpController.TryCrimp"/>. Jadi tidak ada state yang berubah
/// di tengah drag, pemain melihat crimper benar-benar duduk di area dulu, dan
/// urutan wire tidak pernah tersentuh.
///
/// Crimper ditinggal persis di titik pelepasan — tidak ada auto-return, tidak
/// ada snap (crimper adalah tool tangan, bukan bagian kabel). Setelah crimp
/// berhasil, <see cref="CrimpController"/> sendiri yang menyembunyikannya lagi.
/// </summary>
public class CrimpTool : MonoBehaviour, ITool
{
    [Header("References")]
    [Tooltip("Kosong = Camera.main dipakai.")]
    [SerializeField] private Camera mainCamera;

    public ToolState Id => ToolState.CrimpingTool;

    private Transform dragged;
    private Plane dragPlane;
    private Vector3 grabOffset;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void OnDisable()
    {
        // Drag terputus saat tool mati. Tidak ada state yang perlu
        // dipulihkan : tool ini tidak pernah menulis state apa pun.
        dragged = null;
    }

    /// <summary>
    /// Dipanggil ToolInput pada frame pointer ditekan : hanya MEMEGANG
    /// crimper. True = crimper valid sedang dipegang.
    /// </summary>
    public bool Execute(RaycastHit hit)
    {
        CrimpController target = HitResolver.Resolve<CrimpController>(hit);

        if (target == null)
            return false;

        dragged = target.transform;

        // Bidang drag menghadap kamera dan melalui titik pegang, jadi crimper
        // bergerak pada bidang yang sedang dilihat pemain.
        dragPlane = new Plane(-mainCamera.transform.forward, dragged.position);

        // Crimper tidak melompat ke titik kursor saat pertama disentuh.
        grabOffset = ProjectOnPlane(PointerRay(), out Vector3 grabPoint)
            ? dragged.position - grabPoint
            : Vector3.zero;

        Debug.Log($"[CrimpTool] GRAB '{dragged.name}'  pos={dragged.position:F4}", dragged);

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

        // Hanya position root crimper. Child, wire, kabel, dan konektor
        // tidak pernah ditulis oleh tool ini. Men-crimp bukan urusan tool ini
        // — tombol CRIMP yang memutuskannya lewat CrimpController.
        dragged.position = target + grabOffset;
    }

    /// <summary>
    /// Pelepasan pointer : crimper ditinggal di tempat ia dijatuhkan — tidak
    /// dikembalikan, tidak di-snap, dan tidak men-crimp. Kalau pemain melepas
    /// di dalam area, tombol CRIMP tetap tersedia (posisinya masih di area).
    /// </summary>
    private void Release()
    {
        if (dragged != null)
        {
            Debug.Log(
                $"[CrimpTool] RELEASE '{dragged.name}'  pos={dragged.position:F4}",
                dragged);
        }

        dragged = null;
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
