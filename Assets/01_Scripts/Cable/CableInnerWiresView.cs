using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Daftar 8 inner wire di satu ujung kabel, plus resolusi collider -> wire.
///
/// Visibility sengaja TIDAK diatur di sini. Inner wire selalu ada, seperti kabel
/// sungguhan : yang menyembunyikannya adalah geometri jacket, bukan SetActive.
/// Saat CableJacketTrim memotong jacket di ujung A, wire yang tadinya tertutup
/// jadi terlihat dengan sendirinya. Menambahkan toggle di atas itu hanya
/// membuat wire menghilang di dalam jacket yang sudah terbuka.
///
/// Karena itu komponen ini murni registry : tidak subscribe event, tidak
/// menyentuh transform, tidak tahu tool/RJ45/mesh ACC. Posisi wire datang dari
/// hierarchy — wire adalah child dari visual ujung yang sudah dipasang
/// CableEndVisual ke attachment frame endpoint.
/// </summary>
public class CableInnerWiresView : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private CablePoint cablePoint;

    [Header("Wires")]
    [Tooltip("8 inner wire di ujung ini. Urutan belum dipakai — nanti untuk " +
             "slot T568. Slot kosong diabaikan.")]
    [SerializeField] private GameObject[] innerWires = new GameObject[8];

    public CablePoint Point => cablePoint;

    /// <summary>
    /// Daftar 8 wire, read-only. Konsumen yang butuh mengiterasi wire (ordering,
    /// diagnostik) membacanya dari sini supaya tidak ada daftar kedua. Slot yang
    /// belum diassign tetap muncul sebagai null — pemanggil yang memutuskan
    /// apakah itu error.
    /// </summary>
    public IReadOnlyList<GameObject> Wires => innerWires;

    /// <summary>Nama wire yang benar-benar terassign, untuk diagnostik.</summary>
    public string AssignedWireNames()
    {
        string names = string.Empty;
        int assigned = 0;

        foreach (GameObject wire in innerWires)
        {
            if (wire == null)
                continue;

            assigned++;
            names += (names.Length > 0 ? ", " : string.Empty) + wire.name;
        }

        return $"{assigned}/{(innerWires != null ? innerWires.Length : 0)} [{names}]";
    }

    /// <summary>
    /// Wire mana (dari 8 yang diassign) yang memuat <paramref name="candidate"/>.
    /// Dipakai konsumen yang cuma punya collider hasil raycast : collider ada di
    /// child, wire-nya di parent. Null = bukan bagian dari wire mana pun.
    ///
    /// Resolusi ditaruh di sini karena komponen ini yang memegang daftarnya —
    /// jadi tidak ada duplikat daftar 8 wire di tempat lain.
    /// </summary>
    public Transform ResolveWire(Transform candidate)
    {
        for (Transform current = candidate; current != null; current = current.parent)
            foreach (GameObject wire in innerWires)
                if (wire != null && wire.transform == current)
                    return current;

        return null;
    }

#if UNITY_EDITOR
    [ContextMenu("Log Status")]
    private void LogStatus()
    {
        Debug.Log(
            $"[{nameof(CableInnerWiresView)}] endpoint=" +
            $"{(cablePoint != null ? cablePoint.Endpoint.ToString() : "?")}  " +
            $"state={(cablePoint != null ? cablePoint.State.ToString() : "?")}  " +
            $"wires={AssignedWireNames()}",
            this);
    }

    /// <summary>
    /// Wire harus tetap menempel ke endpoint : offset-nya diukur di ruang
    /// endpoint (jadi rotasi kabel tidak terhitung sebagai pergeseran) dan tidak
    /// boleh berubah saat state kabel maju.
    /// </summary>
    [ContextMenu("Self Check")]
    private void SelfCheck()
    {
        if (cablePoint == null)
        {
            Debug.LogWarning($"[{nameof(CableInnerWiresView)}] CablePoint belum diassign.", this);
            return;
        }

        GameObject sample = null;

        foreach (GameObject wire in innerWires)
            if (wire != null)
            {
                sample = wire;
                break;
            }

        if (sample == null)
        {
            Debug.LogWarning($"[{nameof(CableInnerWiresView)}] Belum ada wire diassign.", this);
            return;
        }

        CableState restore = cablePoint.State;

        Vector3 Local() =>
            Quaternion.Inverse(cablePoint.AttachmentRotation) *
            (sample.transform.position - cablePoint.Position);

        Vector3 baseline = Local();
        bool pass = true;

        foreach (CableState state in new[] { CableState.Intact, CableState.Stripped })
        {
            // Setter early-return bila nilainya sama, jadi dipaksa lewat state lain.
            cablePoint.State = state == CableState.Intact ? CableState.Stripped : CableState.Intact;
            cablePoint.State = state;

            bool frozen = (Local() - baseline).sqrMagnitude <= 1e-8f;
            pass &= frozen;

            Debug.Log(
                $"state={state}  active={sample.activeSelf}  " +
                $"localOffset={Local():F4}  positionFrozen={frozen}");
        }

        cablePoint.State = restore;

        Debug.Log($"[{nameof(CableInnerWiresView)}] Self check : {(pass ? "PASS" : "FAIL")}", this);
    }
#endif
}
