using UnityEngine;

public interface ITool
{

    ToolState Id { get; }

    bool Execute(RaycastHit hit);
}
