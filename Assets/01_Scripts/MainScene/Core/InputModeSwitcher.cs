using UnityEngine;

/// <summary>
/// Pemilik mode awal, dan satu-satunya komponen yang boleh menulis
/// <see cref="InputModeService"/>.
///
/// Startup mode ditetapkan di OnEnable, bukan Awake. Alasannya lifecycle :
/// <c>RuntimeInitializeOnLoadMethod(SubsystemRegistration)</c> milik
/// InputModeService bisa berjalan *setelah* Awake komponen scene dalam beberapa
/// urutan startup (paling sering saat Domain Reload dimatikan, atau saat scene
/// dimuat lewat Boot). Kalau startup mode ditulis di Awake, reset itu
/// menghapusnya, dan yang tersisa adalah nilai default enum — DragCable —
/// sehingga ToolInput memblokir semua klik walau Inspector jelas menunjukkan
/// UseTool.
///
/// OnEnable berjalan setelah seluruh Awake dan setelah SubsystemRegistration,
/// jadi penulisannya tidak bisa lagi tertimpa. Start juga aman, tapi OnEnable
/// lebih awal sehingga mode sudah benar sebelum Start komponen lain berjalan.
/// </summary>
public class InputModeSwitcher : MonoBehaviour
{
    [Header("Startup")]
    [SerializeField] private InputMode startupMode = InputMode.DragCable;

    private void OnEnable()
    {
        // Menulis apa adanya, tanpa memeriksa IsConfigured : startup mode dari
        // Inspector memang berhak menang atas apa pun yang tersisa dari sesi
        // sebelumnya.
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
