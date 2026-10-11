using System;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Penunjuk kamera per tahap praktikum. Memilih Virtual Camera mana yang
/// hidup; perpindahan halusnya milik <see cref="CinemachineBrain"/> (blend),
/// jadi komponen ini tidak pernah menulis transform Main Camera.
///
/// Peta state-nya diturunkan dari EVENT yang sudah ada — tidak ada jalur input
/// baru dan tidak ada pemanggilan dari Cable Module :
///   - <see cref="CablePoint.OnStateChanged"/> → Stripped  : fokus ke endpoint itu
///   - <see cref="RJ45Point.OnStateChanged"/>  → Inserted  : naik ke view crimping
///   - <see cref="RJ45Point.OnStateChanged"/>  → Crimped    : kembali ke overview
/// Karena dipicu oleh AKSI (bukan klik), urutan bebas tetap benar : pemain
/// boleh mengerjakan B lebih dulu, kamera ikut ujung yang benar-benar dikerjakan.
///
/// WAJIB : semua vcam menghadap LURUS ke bawah (rotasi 90,0,0). Tool drag
/// (InnerWireDragTool/RJ45Tool/CrimpTool) membangun bidang drag dari
/// -camera.forward; kalau kamera miring, bidangnya ikut miring sementara
/// konektor tetap di y≈0, sehingga area insert/crimp bisa jadi mustahil
/// dijangkau. Karena itu komponen ini hanya menggeser POSISI vcam (zoom/pan)
/// dan tidak pernah menyentuh rotasinya.
///
/// AR : ini rig layar datar (kamera dunia yang digerakkan). Di AR, kamera
/// adalah perangkat pemain, jadi komponen ini tinggal dimatikan.
/// </summary>
public class CameraDirector : MonoBehaviour
{
    /// <summary>Satu endpoint dan view fokusnya. Diisi di Inspector.</summary>
    [Serializable]
    public struct EndpointView
    {
        public CableEndpoint endpoint;
        public CinemachineCamera view;
    }

    [Header("Views")]
    [Tooltip("Tampilan awal : kedua ujung kelihatan.")]
    [SerializeField] private CinemachineCamera overview;
    [Tooltip("View crimping. Posisi X/Z-nya digeser ke konektor terpasang; " +
             "tinggi (Y) dan rotasinya dipakai apa adanya dari Inspector.")]
    [SerializeField] private CinemachineCamera crimp;
    [Tooltip("View fokus per endpoint.")]
    [SerializeField] private EndpointView[] endpointViews;

    [Header("Blend")]
    [Tooltip("Prioritas vcam yang sedang hidup. Vcam lain diturunkan ke 0.")]
    [SerializeField] private int activePriority = 10;
    [Tooltip("Durasi blend di CinemachineBrain (detik). 0 = potong langsung.")]
    [SerializeField] private float blendDuration = 0.25f;

    [Header("Event Sources")]
    [Tooltip("Endpoint yang di-strip memfokuskan kamera ke ujungnya.")]
    [SerializeField] private CablePoint[] points;
    [Tooltip("Konektornya dipakai untuk pindah ke view crimping saat Inserted, " +
             "dan kembali ke overview saat Crimped.")]
    [SerializeField] private RJ45Controller[] connectors;

    private Action<CableState>[] pointHandlers;
    private Action<RJ45State>[] connectorHandlers;
    private CinemachineCamera current;
    private CinemachineBrain brain;

    /// <summary>View yang sedang hidup — untuk debug/pemeriksaan.</summary>
    public CinemachineCamera CurrentView => current;

    private void Awake()
    {
        brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;

        if (brain == null)
        {
            Debug.LogWarning(
                $"[{nameof(CameraDirector)}] CinemachineBrain tidak ada di Main Camera — " +
                $"perpindahan view akan POTONG langsung, bukan blend.", this);
        }
        else
        {
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut, blendDuration);
        }

        BuildHandlers();
        WarnIfNotTopDown();
    }

    private void OnEnable()
    {
        if (points != null)
            for (int i = 0; i < points.Length; i++)
                if (points[i] != null && pointHandlers != null)
                    points[i].OnStateChanged += pointHandlers[i];

        if (connectors != null)
            for (int i = 0; i < connectors.Length; i++)
                if (connectors[i] != null && connectors[i].Point != null && connectorHandlers != null)
                    connectors[i].Point.OnStateChanged += connectorHandlers[i];

        ShowOverview();
    }

    private void OnDisable()
    {
        if (points != null)
            for (int i = 0; i < points.Length; i++)
                if (points[i] != null && pointHandlers != null)
                    points[i].OnStateChanged -= pointHandlers[i];

        if (connectors != null)
            for (int i = 0; i < connectors.Length; i++)
                if (connectors[i] != null && connectors[i].Point != null && connectorHandlers != null)
                    connectors[i].Point.OnStateChanged -= connectorHandlers[i];
    }

    /// <summary>
    /// Delegate dibuat sekali dan disimpan, supaya langganan di OnDisable
    /// melepas instance yang sama — event CablePoint/RJ45Point tidak membawa
    /// referensi dirinya, jadi endpoint-nya harus ditangkap per indeks.
    /// </summary>
    private void BuildHandlers()
    {
        pointHandlers = new Action<CableState>[points != null ? points.Length : 0];

        for (int i = 0; i < pointHandlers.Length; i++)
        {
            CablePoint point = points[i];

            pointHandlers[i] = state =>
            {
                if (state == CableState.Stripped)
                    FocusEndpoint(point.Endpoint);
            };
        }

        connectorHandlers = new Action<RJ45State>[connectors != null ? connectors.Length : 0];

        for (int i = 0; i < connectorHandlers.Length; i++)
        {
            RJ45Controller connector = connectors[i];

            connectorHandlers[i] = state =>
            {
                if (connector == null || connector.Point == null)
                    return;

                if (state == RJ45State.Inserted)
                    ShowCrimp(connector.Point.transform.position);
                else if (state == RJ45State.Crimped)
                    ShowOverview();
            };
        }
    }

    /// <summary>Kedua ujung kelihatan — keadaan netral sebelum/setelah tahap.</summary>
    public void ShowOverview() => Activate(overview);

    /// <summary>Fokus ke satu ujung.</summary>
    public void FocusEndpoint(CableEndpoint endpoint)
    {        if (endpointViews == null)
            return;

        foreach (EndpointView entry in endpointViews)
        {
            if (entry.endpoint != endpoint)
                continue;

            if (entry.view == null)
                break;

            Activate(entry.view);
            return;
        }

        Debug.LogWarning(
            $"[{nameof(CameraDirector)}] Tidak ada view untuk endpoint {endpoint} — " +
            $"kamera tidak dipindah.", this);
    }

    /// <summary>
    /// Apakah kamera SEDANG berada di view fokus ujung ini.
    ///
    /// Dipakai tool yang beralih dua langkah (pilih → kerjakan), mis. strip
    /// kabel : klik pertama ke sebuah ujung hanya memindahkan kamera ke ujung
    /// itu, aksinya baru jalan saat kamera memang sudah di sana. Sumber
    /// kebenarannya view yang hidup — kalau tool menyimpan bool sendiri, ia bisa
    /// desync dengan kamera (mis. setelah crimp mengembalikan view ke overview).
    /// </summary>
    public bool IsFocusedOn(CableEndpoint endpoint)
    {
        if (endpointViews == null)
            return false;

        foreach (EndpointView entry in endpointViews)
        {
            if (entry.endpoint != endpoint || entry.view == null)
                continue;

            return current == entry.view;
        }

        return false;
    }

    /// <summary>
    /// Dekat ke konektor terpasang. Hanya X/Z yang digeser supaya tinggi dan
    /// orientasi lurus-ke-bawah yang disetel di Inspector tetap dipakai.
    /// </summary>
    public void ShowCrimp(Vector3 targetPosition)
    {
        if (crimp == null)
        {
            Debug.LogWarning(
                $"[{nameof(CameraDirector)}] View crimping belum diassign — " +
                $"kamera tidak dipindah.", this);
            return;
        }

        Vector3 position = crimp.transform.position;

        crimp.transform.position = new Vector3(targetPosition.x, position.y, targetPosition.z);

        Activate(crimp);
    }

    /// <summary>
    /// Memilih vcam yang hidup lewat prioritas — blending-nya urusan Brain.
    /// Vcam yang tidak aktif diturunkan ke 0 supaya tidak ada dua view yang
    /// berebut saat prioritas authored-nya berbeda-beda.
    /// </summary>
    private void Activate(CinemachineCamera view)
    {
        if (view == null)
        {
            Debug.LogWarning(
                $"[{nameof(CameraDirector)}] View belum diassign — kamera dibiarkan " +
                $"apa adanya.", this);
            return;
        }

        if (view == current)
            return;

        current = view;

        SetPriority(overview, view == overview ? activePriority : 0);
        SetPriority(crimp, view == crimp ? activePriority : 0);

        if (endpointViews != null)
            foreach (EndpointView entry in endpointViews)
                SetPriority(entry.view, entry.view == view ? activePriority : 0);
    }

    private static void SetPriority(CinemachineCamera view, int value)
    {
        if (view == null)
            return;

        PrioritySettings priority = view.Priority;
        priority.Enabled = true;
        priority.Value = value;
        view.Priority = priority;
    }

    /// <summary>
    /// Rotasi vcam adalah syarat keras sistem drag, bukan selera tampilan.
    /// Dilaporkan di Editor supaya salah setel ketahuan sebelum jadi bug yang
    /// membingungkan ("area crimping tidak bisa dijangkau").
    /// </summary>
    private void WarnIfNotTopDown()
    {
        WarnOne(overview);
        WarnOne(crimp);

        if (endpointViews != null)
            foreach (EndpointView entry in endpointViews)
                WarnOne(entry.view);
    }

    private void WarnOne(CinemachineCamera view)
    {
        if (view == null)
            return;

        if (Vector3.Dot(view.transform.forward, Vector3.down) >= 0.99f)
            return;

        Debug.LogWarning(
            $"[{nameof(CameraDirector)}] '{view.name}' tidak menghadap lurus ke bawah " +
            $"(forward={view.transform.forward:F3}). Bidang drag tool dibangun dari " +
            $"-camera.forward, jadi kamera miring membuat area insert/crimp sulit " +
            $"dijangkau. Pertahankan rotasi (90, 0, 0).", view);
    }

#if UNITY_EDITOR
    [ContextMenu("Camera/Log Status")]
    private void LogStatus()
    {
        string views = $"overview={(overview != null ? overview.name : "-")}  " +
                       $"crimp={(crimp != null ? crimp.name : "-")}";

        if (endpointViews != null)
            foreach (EndpointView entry in endpointViews)
                views += $"  {entry.endpoint}={(entry.view != null ? entry.view.name : "-")}";

        Debug.Log(
            $"[{nameof(CameraDirector)}] current={(current != null ? current.name : "belum ada")}\n" +
            $"  {views}\n" +
            $"  brain={(brain != null ? brain.name + " blend=" + brain.DefaultBlend.Style + " " + brain.DefaultBlend.Time + "s" : "TIDAK ADA")}\n" +
            $"  points={points?.Length ?? 0}  connectors={connectors?.Length ?? 0}",
            this);
    }
#endif
}
