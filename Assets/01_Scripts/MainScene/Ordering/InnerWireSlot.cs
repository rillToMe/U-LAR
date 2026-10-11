using UnityEngine;

/// <summary>
/// Satu area tujuan untuk satu inner wire. Data + geometri, tanpa aturan :
/// yang memutuskan wire mana masuk ke slot mana adalah
/// <see cref="InnerWireOrdering"/>. Slot hanya bisa menjawab dua hal —
/// "warna apa yang kuterima" dan "apakah titik ini di dalam areaku".
///
/// Slot harus DIAM saat wire digerakkan, jadi ia tidak boleh menjadi child dari
/// wire mana pun. <see cref="InnerWireOrdering"/> memverifikasi itu di Self Check.
///
/// Tidak ada raycast di sini. Containment diuji langsung ke collider lewat
/// <see cref="Collider.ClosestPoint"/>, jadi tidak ada query fisika kedua di
/// luar ToolInput dan tidak ada pencarian collider per frame.
/// </summary>
public class InnerWireSlot : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Warna wire yang boleh masuk ke slot ini. Harus unik antar slot.")]
    [SerializeField] private CableColor acceptedColor = CableColor.WhiteOrange;

    [Header("Area")]
    [Tooltip("Area tangkap slot. Kosong = dicari di GameObject ini sendiri. " +
             "Harus convex (Sphere/Box/Capsule) karena ClosestPoint dipakai.")]
    [SerializeField] private Collider area;

    public CableColor AcceptedColor => acceptedColor;

    /// <summary>Posisi wire setelah snap. Titik acuannya slot itu sendiri.</summary>
    public Vector3 SnapPosition => transform.position;

    /// <summary>Null bila belum ada collider sama sekali.</summary>
    public Collider Area => area != null ? area : area = GetComponent<Collider>();

    /// <summary>
    /// Apakah <paramref name="worldPoint"/> berada di dalam area slot.
    /// ClosestPoint mengembalikan titik itu sendiri bila di dalam collider, jadi
    /// perbandingannya eksak tanpa perlu radius manual.
    /// </summary>
    public bool Contains(Vector3 worldPoint)
    {
        Collider collider = Area;

        if (collider == null)
            return false;

        return (collider.ClosestPoint(worldPoint) - worldPoint).sqrMagnitude <= 1e-10f;
    }

    /// <summary>Dipakai creator slot supaya identitas tidak diketik dua kali.</summary>
    public void Configure(CableColor color, Collider slotArea)
    {
        acceptedColor = color;
        area = slotArea;
    }
}
