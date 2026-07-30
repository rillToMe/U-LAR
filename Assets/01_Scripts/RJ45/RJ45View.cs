using UnityEngine;

public class RJ45View : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private RJ45Point rj45Point;

    [Header("Visual")]
    [SerializeField] private GameObject model;

    private void OnEnable()
    {
        rj45Point.OnStateChanged += Refresh;
    }

    private void OnDisable()
    {
        rj45Point.OnStateChanged -= Refresh;
    }

    private void Start()
    {
        Refresh(rj45Point.State);
    }

    private void Refresh(RJ45State state)
    {
        if (model == null)
            return;

        switch (state)
        {
            case RJ45State.None:
                model.SetActive(false);
                break;

            case RJ45State.Inserted:
            case RJ45State.Crimped:
                model.SetActive(true);
                break;
        }
    }
}