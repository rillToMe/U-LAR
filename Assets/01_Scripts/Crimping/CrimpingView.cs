using UnityEngine;

public class CrimpView : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private RJ45Point rj45Point;

    [Header("Visual")]
    [SerializeField] private GameObject crimpIndicator;

    private void OnEnable()
    {
        if (rj45Point != null)
            rj45Point.OnStateChanged += Refresh;
    }

    private void OnDisable()
    {
        if (rj45Point != null)
            rj45Point.OnStateChanged -= Refresh;
    }

    private void Start()
    {
        if (rj45Point != null)
            Refresh(rj45Point.State);
    }

    private void Refresh(RJ45State state)
    {
        if (crimpIndicator == null)
            return;

        crimpIndicator.SetActive(state == RJ45State.Crimped);
    }
}