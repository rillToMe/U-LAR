using UnityEngine;
using NOT_Lonely;

public class AccCableRuntimeTest : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ACC_Cable cable;
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;

    [Header("PointB Motion (temporary milestone driver)")]
    [SerializeField] private bool animatePointB = true;
    [SerializeField] private float amplitude = 0.75f;
    [SerializeField] private float speed = 1f;

    private Vector3 pointBBasePosition;

    private void Start()
    {
        if (pointB != null) pointBBasePosition = pointB.position;
        ApplyEndpoints();
    }

    private void Update()
    {
        if (animatePointB && pointB != null)
        {
            float t = Time.time * speed;
            pointB.position = pointBBasePosition + new Vector3(Mathf.Sin(t) * amplitude, 0f, Mathf.Cos(t * 0.5f) * amplitude);
        }

        ApplyEndpoints();
    }

    [ContextMenu("Apply Endpoints Once")]
    private void ApplyEndpoints()
    {
        if (cable == null || pointA == null || pointB == null) return;

        cable.cableStart = pointA.position;
        cable.cableEnd = pointB.position;
        cable.ReDraw();
    }
}
