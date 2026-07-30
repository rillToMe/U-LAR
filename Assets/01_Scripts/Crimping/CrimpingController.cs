using UnityEngine;

public class CrimpController : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private CablePoint cablePoint;
    [SerializeField] private RJ45Point rj45Point;
    [SerializeField] private PracticeManager practiceManager;

    public bool TryCrimp()
    {
        if (cablePoint == null || rj45Point == null)
            return false;

        // Harus sudah dipasang RJ45
        if (rj45Point.State != RJ45State.Inserted)
            return false;

        // Harus sesuai step praktikum
        if (practiceManager != null &&
            !practiceManager.CanCrimp(cablePoint))
            return false;

        rj45Point.State = RJ45State.Crimped;

        practiceManager?.CompleteCurrentStep();

        Debug.Log($"{name} : RJ45 Crimped");

        return true;
    }

#if UNITY_EDITOR

    [ContextMenu("Test/Try Crimp")]
    private void TestTryCrimp()
    {
        bool result = TryCrimp();

        Debug.Log($"Result : {result}");
        Debug.Log($"RJ45 : {rj45Point.State}");
    }

#endif
}