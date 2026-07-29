using UnityEngine;

public class CablePointView : MonoBehaviour
{
    [SerializeField] private CablePoint cablePoint;

    [Header("Visual")]
    [SerializeField] private GameObject outerJacket;
    [SerializeField] private GameObject copperBundle;
    [SerializeField] private GameObject rj45;

    private void OnEnable()
    {
        cablePoint.OnStateChanged += Refresh;
    }

    private void OnDisable()
    {
        cablePoint.OnStateChanged -= Refresh;
    }

    private void Start()
    {
        Refresh(cablePoint.State);
    }

    private void Refresh(CableState state)
    {
        if (outerJacket != null)
            outerJacket.SetActive(state == CableState.Intact);

        if (copperBundle != null)
            copperBundle.SetActive(
                state == CableState.Stripped ||
                state == CableState.RJ45Mounted ||
                state == CableState.Connected);

        if (rj45 != null)
            rj45.SetActive(
                state == CableState.RJ45Mounted ||
                state == CableState.Connected);
    }
}