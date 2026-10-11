using UnityEngine;

/// <summary>
/// Hanya merespons perubahan state. Tidak ada business logic, tidak pernah
/// menulis state.
/// </summary>
public class CopperView : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private CopperPoint copperPoint;

    [Header("Visual")]
    [Tooltip("Kabel utuh sebelum stripping.")]
    [SerializeField] private GameObject cableFirst;
    [Tooltip("Tembaga sudah terbuka, masih melengkung.")]
    [SerializeField] private GameObject cableKupas;
    [Tooltip("Tembaga sudah diluruskan.")]
    [SerializeField] private GameObject cableLurus;

    private void OnEnable()
    {
        if (copperPoint != null)
            copperPoint.OnStateChanged += Refresh;
    }

    private void OnDisable()
    {
        if (copperPoint != null)
            copperPoint.OnStateChanged -= Refresh;
    }

    private void Start()
    {
        // Event tidak pernah fire untuk state awal.
        if (copperPoint != null)
            Refresh(copperPoint.State);
    }

    private void Refresh(CopperState state)
    {
        if (cableFirst != null)
            cableFirst.SetActive(state == CopperState.Hidden);

        if (cableKupas != null)
            cableKupas.SetActive(state == CopperState.Exposed);

        // Ordered & Trimmed belum punya mesh sendiri; tetap pakai bentuk lurus
        // supaya kabel tidak hilang saat state maju.
        if (cableLurus != null)
            cableLurus.SetActive(
                state == CopperState.Straightened ||
                state == CopperState.Ordered ||
                state == CopperState.Trimmed);
    }
}
