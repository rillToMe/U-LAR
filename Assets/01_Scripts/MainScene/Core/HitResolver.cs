using UnityEngine;

/// <summary>
/// Menerjemahkan RaycastHit menjadi komponen gameplay.
/// Dibutuhkan karena collider dan komponen target tidak selalu berada di
/// GameObject yang sama: pada Cable, collider ada di "Handle" sedangkan
/// CablePoint ada di parent-nya dan RJ45Controller ada di cabang sibling.
/// </summary>
public static class HitResolver
{
    public static T Resolve<T>(RaycastHit hit) where T : Component
    {
        return hit.collider == null ? null : Resolve<T>(hit.collider.transform);
    }

    public static T Resolve<T>(Transform origin) where T : Component
    {
        if (origin == null) return null;

        // 1. Diri sendiri lalu naik ke atas.
        T found = origin.GetComponentInParent<T>();
        if (found != null) return found;

        // 2. Naik satu per satu, tiap level cari ke bawah agar cabang
        //    sibling ikut terjangkau.
        for (Transform parent = origin.parent; parent != null; parent = parent.parent)
        {
            found = parent.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }

        return null;
    }
}
