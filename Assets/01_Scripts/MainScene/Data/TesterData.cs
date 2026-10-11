using UnityEngine;

/// <summary>Hasil pembacaan LAN tester.</summary>
public enum TesterResult
{
    Pass,
    Open,
    Short,
    Cross
}

/// <summary>
/// Teks & tampilan untuk satu hasil tester. Belum ada logic pengujiannya —
/// asset ini hanya menjelaskan hasilnya ke pemain.
/// </summary>
[CreateAssetMenu(menuName = "Data/Tester Result", fileName = "TesterResult")]
public class TesterData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private TesterResult result = TesterResult.Pass;
    [SerializeField] private string displayName;
    [TextArea]
    [SerializeField] private string description;

    [Header("Presentation")]
    [SerializeField] private Color indicatorColor = Color.white;
    [SerializeField] private AudioClip audioClip;

    public TesterResult Result => result;

    public string DisplayName => displayName;

    public string Description => description;

    public Color IndicatorColor => indicatorColor;

    public AudioClip AudioClip => audioClip;
}
