using UnityEngine;

public class RJ45Tool : MonoBehaviour, ITool
{
    public bool Execute(RaycastHit hit)
    {
        RJ45Controller controller =
            hit.collider.GetComponentInParent<RJ45Controller>();

        if (controller == null)
            return false;

        return controller.TryInsert();
    }
}