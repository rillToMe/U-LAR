using System;
using UnityEngine;

public class RJ45Point : MonoBehaviour
{
    [Header("RJ45 State")]
    [SerializeField] private RJ45State state = RJ45State.None;

    public event Action<RJ45State> OnStateChanged;

    public RJ45State State
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