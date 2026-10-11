using UnityEngine;

/// <summary>
/// Presentasi satu tool (nama, ikon, suara, animasi). Belum dipakai —
/// <see cref="ITool"/> tetap yang menjalankan aksinya, ini hanya datanya.
/// </summary>
[CreateAssetMenu(menuName = "Data/Tool", fileName = "Tool")]
public class ToolData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Menghubungkan asset ini ke ITool.Id yang sama.")]
    [SerializeField] private ToolState toolState = ToolState.None;
    [SerializeField] private string displayName;
    [TextArea]
    [SerializeField] private string description;

    [Header("Presentation")]
    [SerializeField] private Sprite icon;
    [SerializeField] private AudioClip audioClip;
    [SerializeField] private string animationName;

    public ToolState ToolState => toolState;

    public string DisplayName => displayName;

    public string Description => description;

    public Sprite Icon => icon;

    public AudioClip AudioClip => audioClip;

    public string AnimationName => animationName;
}
