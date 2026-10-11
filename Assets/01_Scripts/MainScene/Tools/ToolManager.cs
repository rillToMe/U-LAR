using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Satu-satunya sumber kebenaran tool aktif adalah <see cref="currentToolState"/>.
/// Implementasi ITool ditemukan sendiri dari child object, jadi tidak ada
/// referensi Inspector yang bisa desync dengan enum-nya.
/// </summary>
public class ToolManager : MonoBehaviour
{
    [Header("Current Tool")]
    [SerializeField] private ToolState currentToolState = ToolState.None;

    [Header("Registry")]
    [Tooltip("Root pencarian ITool. Kosongkan untuk memakai object ini sendiri. " +
             "Isi dengan parent bila ToolManager dipindah ke child object terpisah.")]
    [SerializeField] private Transform toolsRoot;

    private readonly Dictionary<ToolState, ITool> registry = new Dictionary<ToolState, ITool>();

    public ToolState CurrentToolState => currentToolState;

    public ITool CurrentTool =>
        registry.TryGetValue(currentToolState, out ITool tool) ? tool : null;

    private void Awake()
    {
        BuildRegistry();
    }

    private void BuildRegistry()
    {
        registry.Clear();

        Transform root = toolsRoot != null ? toolsRoot : transform;

        // true = ikut menyertakan tool yang GameObject-nya sedang nonaktif.
        ITool[] tools = root.GetComponentsInChildren<ITool>(true);

        foreach (ITool tool in tools)
        {
            if (tool.Id == ToolState.None)
            {
                Debug.LogError($"{tool} memakai ToolState.None sebagai Id.", this);
                continue;
            }

            if (registry.ContainsKey(tool.Id))
            {
                Debug.LogError($"ToolState '{tool.Id}' terdaftar lebih dari satu kali.", this);
                continue;
            }

            registry.Add(tool.Id, tool);
        }
    }

    public void SetCurrentTool(ToolState state)
    {
        if (currentToolState == state) return;

        currentToolState = state;

        Debug.Log($"Current Tool : {state}");
    }

    /// <summary>Dipakai UnityEvent / UI Button yang tidak bisa mengirim enum.</summary>
    public void SetCurrentTool(int state) => SetCurrentTool((ToolState)state);

    public void ClearCurrentTool() => SetCurrentTool(ToolState.None);
}
