using UnityEngine;

/// <summary>
/// DEPRECATED — tidak melakukan apa pun lagi.
///
/// Dulu komponen ini merentangkan satu mesh visual sepanjang garis A-B tiap
/// LateUpdate : posisi ke midpoint, rotasi ke chord, localScale.y = jarak
/// endpoint. Pendekatan itu sudah digantikan sepenuhnya :
///
///   - bentuk kabel  -> ACC_Cable (mesh prosedural, dikendalikan CableController)
///   - visual ujung  -> CableEndVisual (mengikuti attachment frame CablePoint)
///
/// Keduanya menulis transform yang sama (CableGulung), dan LateUpdate di sini
/// selalu menang atas CableEndVisual — sehingga visual ujung tertarik ke tengah
/// kabel dan ikut melar. Body-nya dihapus untuk menghentikan konflik itu.
///
/// Class-nya sengaja masih ada supaya komponen yang sudah terserialisasi di
/// MainScene tidak menjadi missing script. Hapus komponennya di Editor, lalu
/// file ini boleh dihapus.
/// </summary>
public class CableVisualController : MonoBehaviour
{
}