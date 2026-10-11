using System;
using UnityEngine;

/// <summary>
/// Pola urutan kabel (T568A / T568B / dst). Data murni : tidak tahu apa pun
/// soal Controller, Practice, atau View.
/// </summary>
[CreateAssetMenu(menuName = "Data/Cable Pattern", fileName = "CablePattern")]
public class CablePatternData : ScriptableObject
{
    public const int PinCount = 8;

    [Header("Identity")]
    [SerializeField] private string patternName;
    [SerializeField] private ConnectionType connectionType = ConnectionType.Straight;

    [Header("Wire Order")]
    [Tooltip("Pin 1 sampai pin 8, dari kiri ke kanan saat clip RJ45 menghadap ke bawah.")]
    [SerializeField] private CableColor[] wireOrder = new CableColor[PinCount];

    public string PatternName => patternName;

    public ConnectionType ConnectionType => connectionType;

    /// <summary>Pin 1..8. Di luar rentang itu error, bukan silent default.</summary>
    public CableColor ColorAt(int pin)
    {
        if (pin < 1 || pin > PinCount)
            throw new ArgumentOutOfRangeException(nameof(pin), pin, "Pin harus 1..8.");

        return wireOrder[pin - 1];
    }

    /// <summary>Semua 8 warna wajib beda. Pola dengan warna kembar = salah input.</summary>
    public bool IsValid()
    {
        if (wireOrder == null || wireOrder.Length != PinCount)
            return false;

        for (int i = 0; i < PinCount; i++)
            for (int j = i + 1; j < PinCount; j++)
                if (wireOrder[i] == wireOrder[j])
                    return false;

        return true;
    }

    private void OnValidate()
    {
        if (wireOrder == null || wireOrder.Length != PinCount)
            Array.Resize(ref wireOrder, PinCount);
    }

#if UNITY_EDITOR
    [ContextMenu("Test/Validate")]
    private void TestValidate()
    {
        Debug.Log($"{name} : Valid = {IsValid()}", this);

        for (int pin = 1; pin <= PinCount; pin++)
            Debug.Log($"Pin {pin} : {ColorAt(pin)}", this);
    }
#endif
}
