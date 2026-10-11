using UnityEngine;

/// <summary>
/// Aturan transisi tembaga. Tidak tahu apa pun soal RJ45, ToolManager, UI,
/// AR, maupun LAN tester.
/// </summary>
public class CopperController : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private CopperPoint copperPoint;

    [Header("Practice")]
    [Tooltip("Opsional. Dibiarkan kosong = flow lama jalan tanpa gate practice.")]
    [SerializeField] private PracticeManager practiceManager;
    [Tooltip("Hanya dipakai untuk mengenali ujung mana ini saat bertanya ke PracticeManager. State-nya tidak pernah ditulis dari sini.")]
    [SerializeField] private CablePoint cablePoint;

    public bool Expose()
    {
        if (practiceManager != null && !practiceManager.CanStrip(cablePoint))
            return false;

        if (!TrySetState(CopperState.Hidden, CopperState.Exposed))
            return false;

        practiceManager?.CompleteCurrentStep();

        return true;
    }

    public bool Straighten()
    {
        // TODO: gate dengan practiceManager.CanStraighten(cablePoint) begitu
        // PracticeAction.Straighten + PracticeStep.StraightenPoint* ada.
        // PERINGATAN: selama step itu belum ada, CompleteCurrentStep() di bawah
        // memajukan langkah yang bukan miliknya. Biarkan field practiceManager
        // kosong sampai step-nya ditambahkan.
        if (!TrySetState(CopperState.Exposed, CopperState.Straightened))
            return false;

        practiceManager?.CompleteCurrentStep();

        return true;
    }

    public bool Order() => TrySetState(CopperState.Straightened, CopperState.Ordered);

    public bool Trim() => TrySetState(CopperState.Ordered, CopperState.Trimmed);

    /// <summary>
    /// Guard-nya hanya state saat ini. Validasi urutan warna belum ada di
    /// sprint ini.
    /// </summary>
    private bool TrySetState(CopperState from, CopperState to)
    {
        if (copperPoint == null)
            return false;

        if (copperPoint.State != from)
            return false;

        copperPoint.State = to;

        Debug.Log($"{name} : Copper {to}");

        return true;
    }

#if UNITY_EDITOR
    [ContextMenu("Test/Expose")]
    private void TestExpose() => LogResult("Expose", Expose());

    [ContextMenu("Test/Straighten")]
    private void TestStraighten() => LogResult("Straighten", Straighten());

    [ContextMenu("Test/Order")]
    private void TestOrder() => LogResult("Order", Order());

    [ContextMenu("Test/Trim")]
    private void TestTrim() => LogResult("Trim", Trim());

    private void LogResult(string action, bool result)
    {
        Debug.Log($"{action} : {result}");
        Debug.Log($"Copper : {copperPoint?.State}");
    }
#endif
}
