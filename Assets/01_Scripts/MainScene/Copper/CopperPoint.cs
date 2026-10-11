using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Model utama tembaga. Sama pola dengan <see cref="CablePoint"/> :
/// pemegang data, event edge-triggered, tanpa aturan transisi.
/// </summary>
public class CopperPoint : MonoBehaviour
{
    public const int WireCount = 8;

    [Header("Copper State")]
    [SerializeField] private CopperState state = CopperState.Hidden;

    [Header("Wires")]
    [SerializeField] private CopperWire[] wires = CreateDefaultWires();

    public event Action<CopperState> OnStateChanged;

    public CopperState State
    {
        get => state;
        set
        {
            if (state == value)
                return;

            state = value;

            OnStateChanged?.Invoke(state);
        }
    }

    /// <summary>Read-only : isi tiap wire boleh diubah, jumlah/urutan array tidak.</summary>
    public IReadOnlyList<CopperWire> Wires => wires;

    /// <summary>Wire ke-1..8. Di luar rentang itu error, bukan null diam-diam.</summary>
    public CopperWire WireAt(int index)
    {
        if (index < 1 || index > WireCount)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Wire harus 1..8.");

        return wires[index - 1];
    }

    private static CopperWire[] CreateDefaultWires()
    {
        // CableColor punya tepat 8 member, satu per inti.
        CableColor[] colors = (CableColor[])Enum.GetValues(typeof(CableColor));
        CopperWire[] created = new CopperWire[WireCount];

        for (int i = 0; i < WireCount; i++)
            created[i] = new CopperWire(colors[i]);

        return created;
    }

    private void OnValidate()
    {
        if (wires == null || wires.Length != WireCount)
            wires = CreateDefaultWires();
    }
}
