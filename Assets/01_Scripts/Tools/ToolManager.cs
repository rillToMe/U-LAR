using UnityEngine;

public class ToolManager : MonoBehaviour
{
    [Header("Current Tool")]
    [SerializeField] private ToolState currentToolState = ToolState.None;
    
    [SerializeField] private MonoBehaviour currentToolBehaviour;
    
    public ToolState CurrentToolState => currentToolState;

    public ITool CurrentTool
    {
        get
        {
            return currentToolBehaviour as ITool;
        }
    }

    public void SetCurrentTool(ToolState state, MonoBehaviour tool)
    {
        currentToolState = state;
        currentToolBehaviour = tool;
    }
}