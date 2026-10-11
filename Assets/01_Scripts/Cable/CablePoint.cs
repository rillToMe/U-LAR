using System;
using UnityEngine;

public enum CableEndpoint
{
    A,
    B
}

public enum CableState
{
    Intact,
    Stripped,
    RJ45Mounted,
    Connected
}

public class CablePoint : MonoBehaviour
{
    [Header("Endpoint")]
    [SerializeField] private CableEndpoint endpoint = CableEndpoint.A;

    [Header("Cable State")]
    [SerializeField] private CableState state = CableState.Intact;

    public event Action<CablePoint> PositionChanged;
    public event Action<CableState> OnStateChanged;

    public Vector3 Position
    {
        get => transform.position;
        set
        {
            if (transform.position == value)
                return;

            transform.position = value;

            // lastPosition ikut di-update supaya Update() tidak fire ulang
            // untuk perubahan yang sudah dilaporkan di sini.
            lastPosition = value;

            PositionChanged?.Invoke(this);
        }
    }

    public CableEndpoint Endpoint => endpoint;

    /// <summary>
    /// Arah keluar kabel di ujung ini (menjauh dari badan kabel), ternormalisasi.
    /// Diisi oleh CableController karena hanya dia yang tahu bentuk kabel.
    /// CablePoint cuma menyimpan dan meneruskan — tidak tahu ACC, tidak tahu
    /// RJ45, tidak menghitung kurva.
    /// </summary>
    public Vector3 AttachmentDirection { get; private set; } = Vector3.forward;

    public event Action<CablePoint> AttachmentChanged;

    public void SetAttachmentDirection(Vector3 outwardDirection)
    {
        if (outwardDirection.sqrMagnitude <= 1e-10f)
            return;

        Vector3 normalized = outwardDirection.normalized;

        if (normalized == AttachmentDirection)
            return;

        AttachmentDirection = normalized;

        AttachmentChanged?.Invoke(this);
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

    private Vector3 lastPosition;

    private void Awake()
    {
        lastPosition = transform.position;
    }

    private void Update()
    {
        if (transform.position == lastPosition)
            return;

        lastPosition = transform.position;
        PositionChanged?.Invoke(this);
    }

    /// <summary>
    /// Frame attachment ujung kabel : posisi + orientasi, tanpa muatan gameplay.
    /// Konsumen (visual endpoint, nanti RJ45) hanya perlu ini.
    /// </summary>
    public Quaternion AttachmentRotation =>
        Quaternion.LookRotation(AttachmentDirection, Vector3.up);
}
