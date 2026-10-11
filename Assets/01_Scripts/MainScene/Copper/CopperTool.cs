using UnityEngine;

/// <summary>
/// Satu tool untuk seluruh tahapan tembaga. Tidak menyimpan state sendiri :
/// tiap method controller punya from-state yang saling eksklusif, jadi paling
/// banyak satu yang berhasil per klik.
/// </summary>
public class CopperTool : MonoBehaviour, ITool
{
    public ToolState Id => ToolState.CopperTool;

    public bool Execute(RaycastHit hit)
    {
        CopperController controller = HitResolver.Resolve<CopperController>(hit);

        if (controller == null)
            return false;

        return controller.Expose()
            || controller.Straighten()
            || controller.Order()
            || controller.Trim();
    }
}
