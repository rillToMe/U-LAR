using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Milestone 3 : mouse/touch drag untuk memindahkan CablePoint.
/// Satu-satunya tempat input pointer untuk kabel. Tidak tahu apa pun soal
/// ACC maupun CableController — cukup menulis CablePoint.Position, sisanya
/// mengalir lewat event PositionChanged.
/// </summary>
public class CableInput : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Kosong = Camera.main dipakai.")]
    [SerializeField] private Camera mainCamera;

    [Header("Drag")]
    [Tooltip("Collider endpoint saja yang perlu kena. Default: semua layer.")]
    [SerializeField] private LayerMask raycastLayers = ~0;
    [Tooltip("Drag pada plane horizontal setinggi endpoint. Matikan untuk plane menghadap kamera.")]
    [SerializeField] private bool horizontalPlane = true;

    private GameInput input;

    private CablePoint dragged;
    private Plane dragPlane;
    private Vector3 grabOffset;

    private void Awake()
    {
        input = new GameInput();

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void OnEnable() => input.Enable();

    private void OnDisable()
    {
        input.Disable();
        dragged = null;
    }

    private void Update()
    {
        // Arbitrase: klik hanya diproses saat mode DragCable, supaya tidak
        // bentrok dengan ToolInput.
        if (!InputModeService.Is(InputMode.DragCable))
        {
            dragged = null;
            return;
        }

        if (mainCamera == null)
            return;

        if (input.Gameplay.PointerPress.WasPressedThisFrame())
            TryGrab();

        if (input.Gameplay.PointerPress.WasReleasedThisFrame())
            dragged = null;

        if (dragged != null && input.Gameplay.PointerPress.IsPressed())
            DragToPointer();
    }

    private void TryGrab()
    {
        if (!Physics.Raycast(PointerRay(), out RaycastHit hit, Mathf.Infinity, raycastLayers))
            return;

        // Hanya diri sendiri dan ancestor : collider boleh berada di child
        // endpoint, tapi mesh kabel tidak boleh ikut menarik endpoint.
        CablePoint point = hit.collider.GetComponentInParent<CablePoint>();
        if (point == null)
            return;

        dragged = point;

        dragPlane = horizontalPlane
            ? new Plane(Vector3.up, point.Position)
            : new Plane(-mainCamera.transform.forward, point.Position);

        // Endpoint tidak melompat ke titik kursor saat pertama disentuh.
        grabOffset = ProjectOnPlane(PointerRay(), out Vector3 grabHit)
            ? point.Position - grabHit
            : Vector3.zero;
    }

    private void DragToPointer()
    {
        if (ProjectOnPlane(PointerRay(), out Vector3 target))
            dragged.Position = target + grabOffset;
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

    private Ray PointerRay() =>
        mainCamera.ScreenPointToRay(input.Gameplay.PointerPosition.ReadValue<Vector2>());
}
