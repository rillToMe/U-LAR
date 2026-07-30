using System;
using UnityEngine;

/// <summary>
/// Arbitrase input global. Hanya satu mode yang aktif dalam satu waktu,
/// sehingga CableInput dan ToolInput tidak pernah memproses klik yang sama.
/// </summary>
public static class InputModeService
{
    private static InputMode current = InputMode.DragCable;

    public static event Action<InputMode> OnModeChanged;

    public static InputMode Current => current;

    public static bool Is(InputMode mode) => current == mode;

    public static void SetMode(InputMode mode)
    {
        if (current == mode) return;

        current = mode;
        OnModeChanged?.Invoke(mode);

        Debug.Log($"Input Mode : {mode}");
    }

    // State statis tidak ikut ter-reset saat Domain Reload dimatikan.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        current = InputMode.DragCable;
        OnModeChanged = null;
    }
}
