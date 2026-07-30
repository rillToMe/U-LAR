using UnityEngine;

public class RJ45Tool : MonoBehaviour, ITool
{
    public ToolState Id => ToolState.RJ45Tool;

    public bool Execute(RaycastHit hit)
    {
        RJ45Controller controller = HitResolver.Resolve<RJ45Controller>(hit);

        if (controller == null)
            return false;

        return controller.TryInsert();
    }
}