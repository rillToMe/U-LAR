using UnityEngine;

/// <summary>
/// Titik masuk untuk mengganti InputMode dari Inspector, UI Button,
/// atau script lain. Satu-satunya komponen yang boleh menulis InputModeService.
/// </summary>
public class InputModeSwitcher : MonoBehaviour
{
    [Header("Startup")]
    [SerializeField] private InputMode startupMode = InputMode.DragCable;

    private void Awake()
    {
        InputModeService.SetMode(startupMode);
    }

    public void SetMode(InputMode mode) => InputModeService.SetMode(mode);

    // Dipakai UnityEvent / UI Button yang tidak bisa mengirim enum.
    public void SetDragCable() => SetMode(InputMode.DragCable);
    public void SetUseTool() => SetMode(InputMode.UseTool);
    public void SetUI() => SetMode(InputMode.UI);

#if UNITY_EDITOR
    [ContextMenu("Mode/Drag Cable")]
    private void TestDragCable() => SetDragCable();

    [ContextMenu("Mode/Use Tool")]
    private void TestUseTool() => SetUseTool();
#endif
}
