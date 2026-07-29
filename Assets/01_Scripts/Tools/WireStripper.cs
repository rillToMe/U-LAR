using UnityEngine;

public class WireStripper : MonoBehaviour, ITool
{
    public bool Execute(RaycastHit hit)
    {
        CablePoint point = hit.collider.GetComponent<CablePoint>();
        
        if (point == null)
            return false;

        if (point.State != CableState.Intact)
            return false;

        point.State = CableState.Stripped;

        Debug.Log($"{point.name} stripped.");

        return true;
    }
}