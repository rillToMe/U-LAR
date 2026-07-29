using UnityEngine;
using UnityEngine.Splines;

public class CableController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SplineContainer splineContainer;

    [SerializeField] private CablePoint pointA;
    [SerializeField] private CablePoint pointB;

    private Spline spline;

    private void Awake()
    {
        spline = splineContainer.Spline;
    }

    private void LateUpdate()
    {
        UpdateSpline();
    }

    private void UpdateSpline()
    {
        if (spline.Count < 2)
            return;

        BezierKnot knotA = new BezierKnot(pointA.Position);
        BezierKnot knotB = new BezierKnot(pointB.Position);

        spline.SetKnot(0, knotA);
        spline.SetKnot(1, knotB);
    }
}