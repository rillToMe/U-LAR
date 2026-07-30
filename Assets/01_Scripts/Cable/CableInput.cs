using UnityEngine;
using UnityEngine.InputSystem;

public class CableInput : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;

    private GameInput input;

    private CablePoint selectedPoint;

    private Plane dragPlane;

    private void Awake()
    {
        input = new GameInput();
    }

    private void OnEnable()
    {
        input.Enable();
    }

    private void OnDisable()
    {
        input.Disable();
    }

    private void Update()
    {
        // Arbitrase: drag hanya diproses saat mode DragCable.
        if (!InputModeService.Is(InputMode.DragCable))
        {
            selectedPoint = null;
            return;
        }

        HandlePointerDown();
        HandleDrag();
        HandlePointerUp();
    }

    private void HandlePointerDown()
    {
        if (!input.Gameplay.PointerPress.WasPressedThisFrame())
            return;

        Ray ray = mainCamera.ScreenPointToRay(
            input.Gameplay.PointerPosition.ReadValue<Vector2>());

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        CablePoint point = hit.collider.GetComponent<CablePoint>();

        if (point == null)
            point = hit.collider.GetComponentInParent<CablePoint>();

        if (point == null)
            return;

        selectedPoint = point;

        // Plane horizontal mengikuti tinggi endpoint
        dragPlane = new Plane(Vector3.up, point.transform.position);

        Debug.Log($"Selected : {point.name}");
    }

    private void HandleDrag()
    {
        if (selectedPoint == null)
            return;

        if (!input.Gameplay.PointerPress.IsPressed())
            return;

        Ray ray = mainCamera.ScreenPointToRay(
            input.Gameplay.PointerPosition.ReadValue<Vector2>());

        if (dragPlane.Raycast(ray, out float enter))
        {
            selectedPoint.Position = ray.GetPoint(enter);
        }
    }

    private void HandlePointerUp()
    {
        if (input.Gameplay.PointerPress.WasReleasedThisFrame())
        {
            selectedPoint = null;
        }
    }
}