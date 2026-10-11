using UnityEngine;

/// <summary>
/// Aturan men-crimp SATU konektor RJ45 yang sudah terpasang. Ditempatkan di
/// ROOT crimper — SATU crimper dipakai bersama semua endpoint, jadi targetnya
/// TIDAK di-hardwire : konektor aktif ditentukan saat runtime, yaitu
/// satu-satunya <see cref="RJ45Point"/> yang ber-state
/// <see cref="RJ45State.Inserted"/>. Tidak ada lookup per frame — pencarian
/// hanya saat tombol/UI bertanya.
///
/// Area crimping diukur dari MULUT tool (collider kecil 'ColliderHit') terhadap
/// konektor terpasang : yang menentukan pemain siap men-crimp adalah konektor
/// benar-benar masuk mulut, bukan sekadar body tool menyentuh konektor. Tool
/// tetap punya collider besar ('ColliderDrag') untuk memudahkan grab — dua
/// collider dengan dua tugas berbeda, keduanya di-resolve ke root yang sama
/// sehingga tidak perlu field konektor-endpoint baru.
///
/// Men-crimp TIDAK terjadi saat drag. <see cref="CrimpTool"/> hanya
/// memindahkan crimper; keputusan diambil tombol UI (<c>CrimpButton</c>) yang
/// membaca <see cref="CanCrimpNow"/> — jadi pemain melihat dulu konektor
/// benar-benar masuk mulut tool, baru menekan CRIMP. Mulut hanya GERBANG
/// TAMPILAN : <see cref="TryCrimp"/> tetap memeriksa sendiri state konektor,
/// jadi tidak ada jalan pintas dari sisi UI.
///
/// Tidak menyentuh inner wire sama sekali : urutan wire hasil sorting pemain
/// (benar maupun salah) dibiarkan apa adanya — penilaian T568A/B milik LAN
/// Tester nanti, bukan crimping ini. Crimper tidak di-snap ke konektor :
/// setelah selesai ia disembunyikan lagi dan dikembalikan ke
/// <see cref="readyPosition"/> supaya siap dipakai endpoint berikutnya.
/// </summary>
public class CrimpController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Semua konektor yang boleh di-crimp oleh crimper ini (A, B, ...). " +
             "Konektor aktif = yang RJ45Point-nya sedang Inserted. Wajib diisi.")]
    [SerializeField] private RJ45Controller[] connectors;
    [SerializeField] private PracticeManager practiceManager;
    [Tooltip("Pemilik tool aktif. Setelah crimp berhasil, tool aktif digeser ke " +
             "WireStripper supaya endpoint berikutnya bisa langsung dikerjakan. " +
             "Kosong = pergeseran tool dilewati (dengan warning).")]
    [SerializeField] private ToolManager toolManager;

    [Header("Crimping")]
    [Tooltip("Collider MULUT tool ('ColliderHit') — bagian yang benar-benar " +
             "menerima konektor. Hanya kotak KECIL ini yang diuji terhadap " +
             "konektor, bukan ColliderDrag yang menutupi seluruh badan tool : " +
             "kalau seluruh badan yang diuji, menempelkan gagang ke konektor " +
             "sudah dihitung 'masuk area' padahal konektornya belum masuk mulut.")]
    [SerializeField] private Collider slotCollider;
    [Tooltip("CADANGAN saja, dipakai bila Slot Collider belum diassign : radius " +
             "jarak root crimper ke konektor. Sekaligus dasar jarak spawn " +
             "(lihat Spawn Margin).")]
    [SerializeField] private float crimpRadius = 0.25f;

    [Header("Tool Presentation")]
    [Tooltip("Jarak spawn crimper dari konektor = Crimp Radius + margin ini " +
             "(meter). Diturunkan dari radius supaya crimper SELALU muncul di " +
             "luar area crimping — pemain melihat tool dulu, baru menggesernya " +
             "masuk — tanpa dua angka yang bisa desync.")]
    [SerializeField] private float spawnMargin = 0.12f;
    [Tooltip("Titik pulang CADANGAN, dipakai hanya bila Reveal dipanggil tanpa " +
             "konektor aktif (normalnya tombol keluar hanya muncul saat ada " +
             "konektor Inserted, jadi jarak spawn yang dipakai).")]
    [SerializeField] private Vector3 readyPosition = new Vector3(0f, 0f, 0.55f);

    // Collider konektor aktif dikumpulkan sekali saat konektornya berganti.
    // Uji mulut dibaca UI tiap frame, jadi pencarian collider tidak boleh
    // jalan tiap frame.
    private RJ45Controller cachedTarget;
    private Collider[] cachedTargetColliders;

    /// <summary>
    /// Dijaga di sini, bukan diserahkan ke status aktif di scene : crimper
    /// adalah tool yang masuk meja karena tombol KELUARKAN, jadi ia tidak boleh
    /// tampil sendiri saat play dimulai. Kalau status aktif di scene ikut
    /// tersimpan sebagai aktif, <see cref="IsRevealed"/> sudah true dari frame
    /// pertama — syarat tombol (<c>HasTarget &amp;&amp; !IsRevealed</c>) tidak
    /// pernah terpenuhi, dan tombolnya diam-diam tidak pernah muncul.
    ///
    /// Amannya begini : saat <see cref="Reveal"/> memanggil SetActive(true),
    /// flag sudah diset lebih dulu, jadi Awake-nya tidak menghidup-matikan ulang
    /// tool yang baru saja dikeluarkan pemain.
    /// </summary>
    private void Awake()
    {
        if (revealed)
            return;

        if (gameObject.activeSelf)
        {
            Debug.Log(
                $"[{nameof(CrimpController)}] Crimper ternyata aktif di scene saat " +
                $"play dimulai — disembunyikan dulu (keluar lewat tombol KELUARKAN).",
                this);
        }

        gameObject.SetActive(false);
    }

    private bool revealed;

    /// <summary>
    /// Konektor yang sedang menunggu di-crimp — satu-satunya yang Inserted.
    /// Null = belum ada yang bisa di-crimp (tombol keluar tool tidak muncul).
    /// </summary>
    public RJ45Controller ActiveConnector
    {
        get
        {
            if (connectors == null)
                return null;

            foreach (RJ45Controller connector in connectors)
            {
                if (connector == null)
                    continue;

                RJ45Point point = connector.Point;

                if (point != null && point.State == RJ45State.Inserted)
                    return connector;
            }

            return null;
        }
    }

    /// <summary>Ada konektor terpasang yang menunggu di-crimp.</summary>
    public bool HasTarget => ActiveConnector != null;

    /// <summary>Crimper sedang keluar (GameObject-nya aktif) — dipakai tombol.</summary>
    public bool IsRevealed => gameObject.activeSelf;

    /// <summary>
    /// Posisi konektor terpasang — satu-satunya acuan area crimping : jarak
    /// crimper diukur ke root konektor itu, bukan ke endpoint kabel.
    /// </summary>
    public Vector3 CrimpTargetPosition =>
        ActiveConnector != null ? ActiveConnector.Point.transform.position : Vector3.zero;

    /// <summary>
    /// Apakah <paramref name="crimperPosition"/> berada di dalam area
    /// crimping konektor target. Murni jarak — tidak ada raycast, tidak ada
    /// collider tambahan. Dipakai sebagai cadangan saat <see cref="slotCollider"/>
    /// belum diassign.
    /// </summary>
    public bool IsInCrimpingArea(Vector3 crimperPosition)
    {
        if (ActiveConnector == null)
            return false;

        return Vector3.Distance(crimperPosition, CrimpTargetPosition) <= crimpRadius;
    }

    /// <summary>
    /// Apakah konektor aktif benar-benar masuk MULUT tool.
    ///
    /// Yang diuji hanya collider mulut (<see cref="slotCollider"/>) terhadap
    /// collider konektor — bukan seluruh badan tool. ColliderDrag sengaja besar
    /// supaya mudah di-grab, jadi kalau ia yang dipakai ukuran, menyentuhkan
    /// gagang ke konektor sudah dianggap siap crimp padahal mulutnya masih jauh.
    ///
    /// Hasilnya diuji dengan bounds collider, BUKAN query fisika. Alasannya
    /// konkret : proyek ini jalan dengan <c>Physics.autoSyncTransforms = false</c>,
    /// jadi <c>Physics.OverlapBox</c> yang dipanggil di frame yang sama dengan
    /// perpindahan crimper masih membaca posisi collider yang lama — tombol CRIMP
    /// jadi muncul satu langkah terlambat (atau tidak muncul saat tes otomatis
    /// yang menggeser lalu langsung bertanya). Bounds collider selalu mengikuti
    /// transform saat itu juga, tanpa menunggu sync.
    ///
    /// Kedua sisi praktis selalu sejajar sumbu (crimper hanya digeser, tidak
    /// pernah diputar), jadi AABB vs AABB ini sama dengan uji kotak sesungguhnya.
    /// </summary>
    public bool IsTargetInSlot()
    {
        RJ45Controller target = ActiveConnector;

        if (target == null)
            return false;

        if (slotCollider == null || !slotCollider.enabled)
            return IsInCrimpingArea(transform.position);

        Bounds slot = slotCollider.bounds;
        Collider[] colliders = TargetColliders(target);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];

            if (collider != null && collider.enabled && collider.bounds.Intersects(slot))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Collider milik konektor target, dikumpulkan sekali per konektor — bukan
    /// per frame. Konektor boleh punya berapa pun collider anak (badan, pin,
    /// slot), jadi penyaringan tetap per komponen pemilik, bukan per nama.
    /// </summary>
    private Collider[] TargetColliders(RJ45Controller target)
    {
        if (target != cachedTarget)
        {
            cachedTarget = target;
            cachedTargetColliders = target.GetComponentsInChildren<Collider>();
        }

        return cachedTargetColliders;
    }

    /// <summary>
    /// Syarat tampilnya tombol CRIMP : crimper keluar, ada konektor menunggu,
    /// dan konektor itu benar-benar masuk mulut tool. Dibaca UI tiap frame
    /// (posisi crimper hanya berubah lewat drag, tidak ada event yang bisa
    /// dilanggan).
    /// </summary>
    public bool CanCrimpNow =>
        gameObject.activeInHierarchy && HasTarget && IsTargetInSlot();

    /// <summary>
    /// Mengeluarkan crimper di dekat konektor yang sedang menunggu (lihat
    /// <see cref="SpawnPosition"/>). Dipanggil tombol KELUARKAN CRIMPING TOOL —
    /// bukan otomatis, supaya pemain yang memutuskan kapan tool tangan ini
    /// masuk ke meja.
    /// </summary>
    public void Reveal()
    {
        transform.position = ActiveConnector != null ? SpawnPosition() : readyPosition;

        // Flag diset SEBELUM SetActive : Awake ikut jalan saat GameObject
        // diaktifkan, dan tidak boleh menyembunyikan tool yang baru diminta.
        revealed = true;

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Titik spawn : di luar ujung kabel yang sedang dikerjakan, sejauh
    /// <c>Crimp Radius + Spawn Margin</c> dari konektor terpasang.
    ///
    /// - Arahnya arah KELUAR kabel (diproyeksikan ke bidang datar), jadi
    ///   endpoint A dan B sama-sama muncul di luar ujungnya sendiri, bukan di
    ///   tengah kabel yang tidak terjangkau frame view crimping.
    /// - Tinggi (Y) disamakan dengan konektor, sehingga bidang drag yang
    ///   menghadap kamera melewati tinggi target : jarak ke area crimping
    ///   hanya ditentukan oleh geseran X/Z, tidak ada selisih tinggi yang
    ///   harus ditebus pemain.
    /// - Jaraknya selalu DI LUAR <see cref="crimpRadius"/> (kelebihan
    ///   <see cref="spawnMargin"/>), jadi tombol CRIMP belum muncul saat
    ///   crimper keluar — pemain sempat melihat posisinya dulu.
    ///
    /// Catatan framing : view crimping membingkai ± 0.6 m dari konektor, dan
    /// sumbu pandang kamera tegak lurus sehingga tebal model crimper di layar
    /// hanya ~0.08 m — jadi selama <c>radius + margin</c> masih di bawah ~0.55
    /// crimper terlihat utuh saat muncul.
    /// </summary>
    private Vector3 SpawnPosition()
    {
        Vector3 target = CrimpTargetPosition;
        CablePoint cable = ActiveConnector.Cable;

        Vector3 outward = cable != null ? cable.AttachmentDirection : Vector3.forward;
        outward.y = 0f;

        if (outward.sqrMagnitude < 1e-6f)
            outward = Vector3.forward;

        outward.Normalize();

        float distance = crimpRadius + spawnMargin;

        return new Vector3(
            target.x + outward.x * distance,
            target.y,
            target.z + outward.z * distance);
    }

    /// <summary>
    /// Men-crimp konektor yang sudah terpasang. Gate diperiksa dulu — harus
    /// ada konektor Inserted. Tidak ada pemeriksaan urutan warna : susunan
    /// salah tetap boleh di-crimp, penilaiannya menunggu LAN Tester.
    ///
    /// SUKSES = state konektor maju ke Crimped, endpoint maju ke Connected,
    /// tool aktif digeser ke WireStripper (endpoint berikutnya), lalu crimper
    /// disembunyikan lagi. GAGAL = tidak ada yang berubah sama sekali.
    /// </summary>
    public bool TryCrimp()
    {
        RJ45Controller target = ActiveConnector;

        if (target == null)
            return false;

        CablePoint cable = target.Cable;

        if (cable == null)
        {
            Debug.LogWarning(
                $"{name} : konektor '{target.name}' tidak punya CablePoint — " +
                $"crimp dibatalkan.", this);
            return false;
        }

        // Harus sesuai step praktikum
        if (practiceManager != null && !practiceManager.CanCrimp(cable))
            return false;

        target.Point.State = RJ45State.Crimped;

        // Endpoint selesai : Connected. CableController memetakan state ini
        // ke trim yang sama seperti Stripped (jacket tidak pulih), dan
        // CableInstallationTracker mencatatnya untuk LAN Tester nanti.
        cable.State = CableState.Connected;

        practiceManager?.CompleteCurrentStep();

        // Alur lanjut : endpoint berikutnya dimulai dari strip. Tool aktif
        // digeser lewat API ToolManager yang sudah ada — bukan sistem pilihan
        // tool baru.
        if (toolManager != null)
        {
            toolManager.SetCurrentTool(ToolState.WireStripper);
        }
        else
        {
            Debug.LogWarning(
                $"{name} : ToolManager belum diassign — tool aktif tidak digeser " +
                $"ke {ToolState.WireStripper}.",
                this);
        }

        Debug.Log($"{name} : RJ45 Crimped, endpoint {cable.Endpoint} Connected");

        Conceal();

        return true;
    }

    /// <summary>
    /// Sembunyikan crimper dan pulangkan ke titik siap pakai, supaya endpoint
    /// berikutnya tinggal menekan tombol keluar lagi. Crimper TIDAK di-snap ke
    /// konektor dan tidak ditinggal di atasnya.
    /// </summary>
    private void Conceal()
    {
        transform.position = readyPosition;
        gameObject.SetActive(false);
    }

#if UNITY_EDITOR

    [ContextMenu("Test/Try Crimp")]
    private void TestTryCrimp()
    {
        RJ45Controller target = ActiveConnector;

        Debug.Log($"Result : {TryCrimp()}");
        Debug.Log($"RJ45   : {(target != null ? target.Point.State.ToString() : "tidak ada konektor Inserted")}");
        Debug.Log($"Cable  : {(target != null && target.Cable != null ? target.Cable.State.ToString() : "-")}");
    }

#endif
}
