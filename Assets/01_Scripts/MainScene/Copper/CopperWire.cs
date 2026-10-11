using System;
using UnityEngine;

/// <summary>
/// Satu inti kabel tembaga. Model murni — tidak tahu slot fisik mana pun,
/// hanya nomor slotnya.
/// </summary>
[Serializable]
public class CopperWire
{
    [SerializeField] private CableColor color;
    [Tooltip("Slot 1..8 di dalam housing. 0 = belum ditempatkan.")]
    [SerializeField] private int currentSlot;
    [SerializeField] private bool isStraight;

    public CopperWire(CableColor color)
    {
        this.color = color;
    }

    public CableColor Color => color;

    public int CurrentSlot
    {
        get => currentSlot;
        set => currentSlot = value;
    }

    public bool IsStraight
    {
        get => isStraight;
        set => isStraight = value;
    }
}
