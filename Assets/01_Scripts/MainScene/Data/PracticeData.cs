using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Urutan langkah satu sesi praktikum. Data murni : hanya menyimpan urutan,
/// tidak tahu siapa yang menjalankannya.
/// </summary>
[CreateAssetMenu(menuName = "Data/Practice", fileName = "Practice")]
public class PracticeData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string practiceName;

    [Header("Steps")]
    [Tooltip("Dijalankan dari atas ke bawah. Langkah terakhir sebaiknya Finished.")]
    [SerializeField] private List<PracticeStep> steps = new List<PracticeStep>();

    public string PracticeName => practiceName;

    public IReadOnlyList<PracticeStep> Steps => steps;

    /// <summary>Langkah kembar bikin navigasi by-index ambigu, jadi dianggap invalid.</summary>
    public bool IsValid()
    {
        if (steps == null || steps.Count == 0)
            return false;

        for (int i = 0; i < steps.Count; i++)
            for (int j = i + 1; j < steps.Count; j++)
                if (steps[i] == steps[j])
                    return false;

        return true;
    }

#if UNITY_EDITOR
    [ContextMenu("Test/Validate")]
    private void TestValidate()
    {
        Debug.Log($"{name} : Valid = {IsValid()}, Steps = {steps.Count}", this);
    }
#endif
}
