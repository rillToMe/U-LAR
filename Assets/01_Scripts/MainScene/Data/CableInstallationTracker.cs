using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pembaca kondisi instalasi kabel UTP menjadi <see cref="CableInstallationState"/>.
/// Hanya MEMBACA : tidak pernah menulis posisi wire, tidak mengubah state
/// CablePoint, tidak menyentuh perilaku <see cref="InnerWireOrdering"/>, dan
/// tidak menilai apa pun — susunan pemain, salah maupun benar, dicatat apa
/// adanya sebagai susunan yang sedang berlaku.
///
/// Sumber data seluruhnya sistem yang sudah ada :
/// <see cref="CableInnerWiresView"/> (daftar wire + CablePoint),
/// <see cref="InnerWireOrdering"/> (event drop), dan
/// <see cref="InnerWireStageController"/> (event tahap). Endpoint B sengaja
/// ditoleransi null untuk Ordering/Stage-nya — urutannya tetap terbaca dari
/// posisi wire, dan begitu B punya sistemnya nanti tidak ada perubahan
/// yang diperlukan di sini.
///
/// Urutan per pin dihitung dari posisi world wire : sumbu lateral diturunkan
/// dari pasangan wire yang berjarak paling jauh (konsep yang sama dengan
/// live reorder di Ordering, tapi implementasinya di sini — helper itu tidak
/// ditambahkan ke Ordering), lalu wire diurutkan sepanjang sumbu tersebut.
/// Tidak bergantung pada posisi slot, tidak pada urutan registrasi, dan
/// posisi world tidak ikut disimpan.
///
/// Trigger snapshot : drop wire (OnDropResolved), perubahan state CablePoint,
/// perubahan tahap, dan sekali di OnEnable sebagai potret awal.
/// </summary>
public class CableInstallationTracker : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("ID instalasi ini. Belum ada sistem ID global — diisi manual dari Inspector.")]
    [SerializeField] private string cableId;
    [Tooltip("Mode koneksi yang dipilih pemain untuk instalasi ini.")]
    [SerializeField] private ConnectionType connectionType = ConnectionType.Straight;

    [Header("Endpoint A")]
    [SerializeField] private CableInnerWiresView wiresViewA;
    [Tooltip("Sumber event drop. Kosong = snapshot hanya di-trigger perubahan state/tahap.")]
    [SerializeField] private InnerWireOrdering orderingA;
    [Tooltip("Sumber event tahap. Kosong = tahap ujung ini dicatat null.")]
    [SerializeField] private InnerWireStageController stageA;

    [Header("Endpoint B (opsional)")]
    [Tooltip("Wire endpoint ini tetap terbaca walau B belum punya Ordering/Stage.")]
    [SerializeField] private CableInnerWiresView wiresViewB;
    [Tooltip("Sumber event drop endpoint B, kalau B sudah punya Ordering.")]
    [SerializeField] private InnerWireOrdering orderingB;
    [Tooltip("Sumber event tahap endpoint B, kalau B sudah punya StageController.")]
    [SerializeField] private InnerWireStageController stageB;

    /// <summary>Potret terakhir. Null sebelum OnEnable pertama.</summary>
    public CableInstallationState Current { get; private set; }

    /// <summary>
    /// Menyala setiap potret baru BERBEDA dari sebelumnya (termasuk potret
    /// awal). Payload-nya state terbaru — objek baru tiap perubahan, jadi
    /// pendengar bebas menyimpannya tanpa takut termutasi.
    /// </summary>
    public event Action<CableInstallationState> OnStateChanged;

    private void OnEnable()
    {
        SubscribeOrdering(orderingA);
        SubscribeOrdering(orderingB);

        SubscribePoint(wiresViewA);
        SubscribePoint(wiresViewB);

        SubscribeStage(stageA);
        SubscribeStage(stageB);

        // Potret awal : kondisi apa adanya saat tracker hidup. Dijalankan
        // SETELAH langganan supaya perubahan di antara keduanya tidak hilang.
        Snapshot();
    }

    private void OnDisable()
    {
        UnsubscribeOrdering(orderingA);
        UnsubscribeOrdering(orderingB);

        UnsubscribePoint(wiresViewA);
        UnsubscribePoint(wiresViewB);

        UnsubscribeStage(stageA);
        UnsubscribeStage(stageB);
    }

    /// <summary>
    /// Membaca seluruh sistem yang ada menjadi potret baru, menyimpannya sebagai
    /// <see cref="Current"/>, dan menyalakan <see cref="OnStateChanged"/> bila
    /// isinya berbeda dari potret sebelumnya. Aman dipanggil kapan pun —
    /// murni membaca, tidak menulis apa pun ke scene.
    /// </summary>
    public CableInstallationState Snapshot()
    {
        var next = new CableInstallationState
        {
            CableId = cableId,
            Type = connectionType,
            EndpointA = CaptureEndpoint(wiresViewA, stageA),
            EndpointB = CaptureEndpoint(wiresViewB, stageB)
        };

        bool changed = Current == null || !Current.SameAs(next);
        Current = next;

        if (changed)
        {
            Debug.Log($"[CableInstallation] snapshot berubah  A={OrderText(Current.EndpointA)}  B={OrderText(Current.EndpointB)}", this);
            OnStateChanged?.Invoke(Current);
        }

        return Current;
    }

    private void HandleOrderingDrop(Transform wire, bool info) => Snapshot();

    private void HandlePointStateChanged(CableState state) => Snapshot();

    private void HandleStageChanged(InnerWireStage stage) => Snapshot();

    private void SubscribeOrdering(InnerWireOrdering ordering)
    {
        if (ordering != null)
            ordering.OnDropResolved += HandleOrderingDrop;
    }

    private void UnsubscribeOrdering(InnerWireOrdering ordering)
    {
        if (ordering != null)
            ordering.OnDropResolved -= HandleOrderingDrop;
    }

    private void SubscribeStage(InnerWireStageController stage)
    {
        if (stage != null)
            stage.OnStageChanged += HandleStageChanged;
    }

    private void UnsubscribeStage(InnerWireStageController stage)
    {
        if (stage != null)
            stage.OnStageChanged -= HandleStageChanged;
    }

    private void SubscribePoint(CableInnerWiresView view)
    {
        if (view != null && view.Point != null)
            view.Point.OnStateChanged += HandlePointStateChanged;
    }

    private void UnsubscribePoint(CableInnerWiresView view)
    {
        if (view != null && view.Point != null)
            view.Point.OnStateChanged -= HandlePointStateChanged;
    }

    /// <summary>
    /// Membaca satu ujung : state CablePoint, tahap (null bila tak ada
    /// controller), dan urutan wire per pin dari posisi world.
    /// </summary>
    private static CableEndpointInstallation CaptureEndpoint(
        CableInnerWiresView view,
        InnerWireStageController stage)
    {
        return new CableEndpointInstallation
        {
            CableState = view != null && view.Point != null
                ? view.Point.State
                : CableState.Intact,
            Stage = stage != null ? stage.Stage : (InnerWireStage?)null,
            Order = CaptureOrder(view)
        };
    }

    /// <summary>
    /// Urutan 8 wire per pin dari posisi world :
    /// 1. sumbu lateral = arah pasangan wire terjauh (tanda sumbu
    ///    dikanonalkan supaya hasil tidak bergantung pada pasangan mana yang
    ///    kebetulan dipilih maupun urutan registrasi) ;
    /// 2. wire diurutkan menaik sepanjang sumbu itu — indeks 0 = pin 1 ;
    /// 3. tiap wire diterjemahkan namanya jadi <see cref="CableColor"/>.
    /// Null = urutan belum bisa ditentukan ; alasannya di-log.
    /// </summary>
    private static CableColor[] CaptureOrder(CableInnerWiresView view)
    {
        if (view == null)
            return null;

        var wires = new List<Transform>();

        foreach (GameObject wire in view.Wires)
            if (wire != null)
                wires.Add(wire.transform);

        if (wires.Count < 2)
        {
            Debug.LogWarning($"[CableInstallation] '{view.name}' punya {wires.Count} wire — sumbu lateral tidak bisa diturunkan.", view);
            return null;
        }

        Transform a = wires[0];
        Transform b = wires[1];
        float farthest = -1f;

        for (int i = 0; i < wires.Count; i++)
            for (int j = i + 1; j < wires.Count; j++)
            {
                float squared = (wires[j].position - wires[i].position).sqrMagnitude;

                if (squared > farthest)
                {
                    farthest = squared;
                    a = wires[i];
                    b = wires[j];
                }
            }

        if (farthest < 1e-8f)
        {
            Debug.LogWarning($"[CableInstallation] Semua wire '{view.name}' berimpit — urutan tidak bisa ditentukan.", view);
            return null;
        }

        Vector3 axis = CanonicalAxis((b.position - a.position).normalized);

        wires.Sort((x, y) =>
            Vector3.Dot(x.position, axis).CompareTo(Vector3.Dot(y.position, axis)));

        var order = new CableColor[wires.Count];

        for (int i = 0; i < wires.Count; i++)
        {
            if (!InnerWireColor.TryResolve(wires[i].name, out CableColor color))
            {
                Debug.LogWarning($"[CableInstallation] Nama wire '{wires[i].name}' tidak dikenal InnerWireColor — urutan '{view.name}' tidak disimpan.", wires[i]);
                return null;
            }

            order[i] = color;
        }

        return order;
    }

    /// <summary>
    /// Tanda sumbu dibakukan dari komponen dominannya : komponen terbesar
    /// (lalu komponen berikutnya bila seri) wajib positif. Hasilnya arah sort
    /// ditentukan geometri dunia, bukan oleh urutan wire di daftar atau
    /// pasangan terjauh mana yang kebetulan menang.
    /// </summary>
    private static Vector3 CanonicalAxis(Vector3 axis)
    {
        const float epsilon = 1e-6f;

        bool flip =
            axis.x < -epsilon ||
            (Mathf.Abs(axis.x) <= epsilon &&
                (axis.y < -epsilon ||
                 (Mathf.Abs(axis.y) <= epsilon && axis.z < -epsilon)));

        return flip ? -axis : axis;
    }

    private static string OrderText(CableEndpointInstallation endpoint)
    {
        if (endpoint == null || endpoint.Order == null)
            return "?";

        string[] names = new string[endpoint.Order.Length];

        for (int i = 0; i < endpoint.Order.Length; i++)
            names[i] = endpoint.Order[i].ToString();

        return "[" + string.Join(", ", names) + "]";
    }
}
