using UnityEngine;
using UnityEngine.UI;

public class SwitchToggle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Toggle toggle;
    [SerializeField] private RectTransform handle;

    [Header("Settings")]
    [SerializeField] private bool defaultOn = false;
    [SerializeField] private float offPosition = -25f;
    [SerializeField] private float onPosition = 25f;
    [SerializeField] private float smoothTime = 0.12f;

    private float targetX;
    private float velocity;

    private void Awake()
    {
        // Set kondisi awal
        toggle.SetIsOnWithoutNotify(defaultOn);

        // Set target posisi
        SetTarget(defaultOn);

        // Langsung tempatkan handle di posisi awal
        Vector2 position = handle.anchoredPosition;
        position.x = targetX;
        handle.anchoredPosition = position;

        // Dengarkan perubahan Toggle
        toggle.onValueChanged.AddListener(OnToggleChanged);
    }

    private void Update()
    {
        Vector2 position = handle.anchoredPosition;

        position.x = Mathf.SmoothDamp(
            position.x,
            targetX,
            ref velocity,
            smoothTime
        );

        handle.anchoredPosition = position;
    }

    private void OnToggleChanged(bool isOn)
    {
        SetTarget(isOn);
    }

    private void SetTarget(bool isOn)
    {
        targetX = isOn ? onPosition : offPosition;
    }

    private void OnDestroy()
    {
        toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }
}