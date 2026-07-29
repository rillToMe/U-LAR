using UnityEngine;
using UnityEngine.InputSystem;

public class ToolInput : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionReference pointerPosition;
    [SerializeField] private InputActionReference pointerPress;

    [Header("References")]
    [SerializeField] private ToolManager toolManager;
    
    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;
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
        Vector2 screenPosition = pointerPosition.action.ReadValue<Vector2>();

        Ray ray = _mainCamera.ScreenPointToRay(screenPosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        CablePoint point = hit.collider.GetComponent<CablePoint>();

        if (point == null)
            return;

        toolManager.CurrentTool?.Execute(hit);
    }
}