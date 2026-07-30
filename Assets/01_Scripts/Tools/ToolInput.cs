using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Generic pointer dispatcher: Mouse/Touch -> RaycastHit -> ITool.Execute.
/// Tidak mengenal CablePoint, RJ45, Crimp, maupun Tester — resolusi target
/// sepenuhnya urusan masing-masing tool.
/// </summary>
public class ToolInput : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionReference pointerPosition;
    [SerializeField] private InputActionReference pointerPress;

    [Header("References")]
    [SerializeField] private ToolManager toolManager;
    [SerializeField] private Camera mainCamera;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        pointerPress.action.Enable();
        pointerPosition.action.Enable();

        pointerPress.action.performed += OnPointerPressed;
    }

    private void OnDisable()
    {
        pointerPress.action.performed -= OnPointerPressed;

        pointerPress.action.Disable();
        pointerPosition.action.Disable();
    }

    private void OnPointerPressed(InputAction.CallbackContext context)
    {
        // Arbitrase: klik hanya diproses saat mode UseTool.
        if (!InputModeService.Is(InputMode.UseTool))
            return;

        ITool tool = toolManager.CurrentTool;
        if (tool == null)
            return;

        Vector2 screenPosition = pointerPosition.action.ReadValue<Vector2>();
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        tool.Execute(hit);
    }
}
