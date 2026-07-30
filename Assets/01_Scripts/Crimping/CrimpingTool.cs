using UnityEngine;

public class CrimpTool : MonoBehaviour, ITool
{
    public ToolState Id => ToolState.CrimpingTool;

    public bool Execute(RaycastHit hit)
    {
        CrimpController controller = HitResolver.Resolve<CrimpController>(hit);

        if (controller == null)
            return false;

        return controller.TryCrimp();
    }
}