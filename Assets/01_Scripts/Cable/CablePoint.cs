using System;
using UnityEngine;

public class CablePoint : MonoBehaviour
{
    [Header("Cable State")]
    [SerializeField] private CableState state = CableState.Intact;

    public event Action<CableState> OnStateChanged;

    public Vector3 Position
    {
        get => transform.position;
        set => transform.position = value;
    }

    public CableState State
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
}