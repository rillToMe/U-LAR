using UnityEngine;

public class WireStripper : MonoBehaviour, ITool
{
    public ToolState Id => ToolState.WireStripper;

    public bool Execute(RaycastHit hit)
    {
        // Collider ada di "Handle", CablePoint ada di parent-nya.
        CablePoint point = HitResolver.Resolve<CablePoint>(hit);

        if (point == null)
            return false;

        if (point.State != CableState.Intact)
            return false;

        point.State = CableState.Stripped;

        Debug.Log($"{point.name} stripped.");

        return true;
    }
}