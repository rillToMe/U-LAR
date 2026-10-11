using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Generic pointer dispatcher: Mouse/Touch -> RaycastHit -> ITool.Execute.
/// Tidak mengenal CablePoint, RJ45, Crimp, maupun Tester — resolusi target
/// sepenuhnya urusan masing-masing tool.
///
/// Collider terdekat bukan berarti target yang benar : di ujung kabel,
/// collider endpoint (sphere ~0.15 m) menyelubungi seluruh collider inner wire
/// (~0.003 m), jadi hit terdekat selamanya endpoint dan wire tidak pernah bisa
/// diklik. Karena itu seluruh hit di bawah kursor diurutkan dari yang terdekat
/// dan ditawarkan satu per satu sampai ada tool yang menerimanya.
///
/// Ini memakai kontrak <see cref="ITool.Execute"/> yang sudah ada apa adanya :
/// nilai <c>false</c> memang berarti "target tidak cocok", dan tiap tool sudah
/// menolak hit yang bukan miliknya. Yang berubah hanya : nilai itu sekarang
/// dipakai, bukan dibuang. Konsekuensinya klik bisa menembus collider di depan
/// bila tool tidak mengakuinya — itu memang yang dibutuhkan di sini.
/// </summary>
public class ToolInput : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionReference pointerPosition;
    [SerializeField] private InputActionReference pointerPress;

    [Header("References")]
    [SerializeField] private ToolManager toolManager;
    [SerializeField] private Camera mainCamera;

    [Header("Raycast")]
    [Tooltip("Batas hit per klik. Cukup besar untuk collider yang bertumpuk di " +
             "ujung kabel (endpoint + 8 inner wire), tanpa mengalokasi per klik.")]
    [SerializeField] private int maxHits = 16;

    // Non-alloc : buffer dipakai ulang antar klik.
    private RaycastHit[] hits;

    // Tool yang butuh drag (tekan-tahan-lepas) tidak bisa diungkapkan lewat
    // ITool.Execute yang sekali-jalan. Daripada tiap tool membuat action sendiri
    // — yang berarti binding ganda ke pointer yang sama — action milik komponen
    // ini dipublikasikan apa adanya. Tetap satu sumber input.
    private static ToolInput active;

    public static Vector2 PointerPosition =>
        active != null && active.pointerPosition != null
            ? active.pointerPosition.action.ReadValue<Vector2>()
            : Vector2.zero;

    public static bool PointerHeld =>
        active != null && active.pointerPress != null && active.pointerPress.action.IsPressed();

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        hits = new RaycastHit[Mathf.Max(1, maxHits)];
    }

    private void OnEnable()
    {
        active = this;

        pointerPress.action.Enable();
        pointerPosition.action.Enable();

        pointerPress.action.performed += OnPointerPressed;
    }

    private void OnDisable()
    {
        pointerPress.action.performed -= OnPointerPressed;

        pointerPress.action.Disable();
        pointerPosition.action.Disable();

        if (active == this)
            active = null;
    }

    private void OnPointerPressed(InputAction.CallbackContext context)
    {
        Vector2 screenPosition = pointerPosition.action.ReadValue<Vector2>();
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);

        // Terlihat di Scene view (dan Game view bila Gizmos aktif) selama Play.
        Debug.DrawRay(ray.origin, ray.direction * 10f, Color.yellow, 1f);

        ITool current = toolManager != null ? toolManager.CurrentTool : null;

        Debug.Log(
            $"[ToolInput DEBUG]\n" +
            $"pointer={screenPosition}\n" +
            $"rayOrigin={ray.origin:F4}\n" +
            $"rayDirection={ray.direction:F4}\n" +
            $"currentTool={(current != null ? current.GetType().Name : "null")}\n" +
            $"currentToolState={(toolManager != null ? toolManager.CurrentToolState.ToString() : "no ToolManager")}\n" +
            $"inputMode={InputModeService.Current}",
            this);

        // Arbitrase: klik hanya diproses saat mode UseTool.
        if (!InputModeService.Is(InputMode.UseTool))
        {
            Debug.Log(
                $"[ToolInput DEBUG] BLOCKED — " +
                $"configured={InputModeService.IsConfigured} " +
                $"inputMode={InputModeService.Current}, butuh UseTool",
                this);
            return;
        }

        ITool tool = toolManager.CurrentTool;
        if (tool == null)
        {
            Debug.Log(
                $"[ToolInput DEBUG] NO TOOL DISPATCHED — " +
                $"currentToolState={toolManager.CurrentToolState} tidak ada di registry " +
                $"(ITool dengan Id itu bukan child toolsRoot, atau Id-nya terdaftar dobel)",
                this);
            return;
        }

        // Satu query fisika. Semua log di bawah membaca hasil yang sama.
        int count = Physics.RaycastNonAlloc(ray, hits);

        Debug.Log($"[ToolInput DEBUG]\nraycastHit={count > 0}  hitCount={count}", this);

        if (count == 0)
        {
            Debug.Log("[ToolInput DEBUG] NO TOOL DISPATCHED — raycast tidak kena apa pun", this);
            return;
        }

        if (count > hits.Length - 1)
            Debug.LogWarning(
                $"[ToolInput] Buffer raycast penuh ({hits.Length}). Naikkan Max Hits " +
                $"bila ada collider yang terlewat.", this);

        // Terdekat lebih dulu : RaycastNonAlloc tidak menjamin urutan.
        System.Array.Sort(hits, 0, count, HitDistanceComparer.Instance);

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];

            Debug.Log(
                $"[ToolInput DEBUG] HIT [{i + 1}/{count}]\n" +
                $"collider={hit.collider.name}\n" +
                $"colliderGameObject={hit.collider.gameObject.name}\n" +
                $"transform={hit.transform.name}\n" +
                $"hitPoint={hit.point:F4}\n" +
                $"distance={hit.distance:F4}\n" +
                $"colliderITool={ToolOn(hit.collider)}\n" +
                $"parentChain={ParentChain(hit.collider.transform)}",
                hit.collider);

            // false = "target ini bukan milikku", jadi hit berikutnya ditawarkan.
            // Inilah yang membuat collider besar berhenti menutupi yang kecil.
            if (tool.Execute(hit))
            {
                Debug.Log(
                    $"[ToolInput DEBUG] DISPATCH -> {tool.GetType().Name}  " +
                    $"accepted='{hit.collider.name}' hit[{i + 1}/{count}]",
                    this);
                return;
            }
        }

        Debug.Log(
            $"[ToolInput DEBUG] NO TOOL DISPATCHED — {tool.GetType().Name} menolak " +
            $"seluruh {count} hit di bawah kursor",
            this);
    }

    /// <summary>
    /// Comparer instance tunggal supaya Array.Sort tidak boxing/alloc per klik.
    /// </summary>
    private sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public static readonly HitDistanceComparer Instance = new HitDistanceComparer();

        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }

    private static string ToolOn(Collider collider)
    {
        ITool tool = collider.GetComponent<ITool>();

        return tool != null ? tool.GetType().Name : "none";
    }

    private static string ParentChain(Transform origin)
    {
        string chain = origin.name;

        for (Transform parent = origin.parent; parent != null; parent = parent.parent)
            chain = parent.name + " / " + chain;

        return chain;
    }
}
