using UnityEngine;

public class RJ45Controller : MonoBehaviour
{
    [SerializeField] private CablePoint cablePoint;
    [SerializeField] private RJ45Point rj45Point;

    public bool TryInsert()
    {
        if (cablePoint == null || rj45Point == null)
            return false;

        if (cablePoint.State != CableState.Stripped)
            return false;

        if (rj45Point.State != RJ45State.None)
            return false;

        rj45Point.State = RJ45State.Inserted;

        Debug.Log($"{name} : RJ45 Inserted");

        return true;
    }
#if UNITY_EDITOR
    [ContextMenu("Test/Try Insert")]
    private void TestTryInsert()
    {
        bool result = TryInsert();

        Debug.Log($"Result : {result}");
        Debug.Log($"Cable : {cablePoint.State}");
        Debug.Log($"RJ45 : {rj45Point.State}");
    }
#endif
}