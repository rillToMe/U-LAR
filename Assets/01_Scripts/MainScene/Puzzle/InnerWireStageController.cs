using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Tahap penggarapan 8 inner wire di satu endpoint.</summary>
public enum InnerWireStage
{
    Messy,
    Flattening,
    Flattened,
    Sorting,
    Completed
}

/// <summary>
/// Pemilik TAHAP : Messy -> Flattening -> Flattened -> Sorting -> Completed.
///
/// Bukan pemilik bukti. "Sudah datar" tidak pernah disimpan sebagai bool di
/// sini — satu-satunya sumbernya <see cref="InnerWireFlatten.IsComplete"/>,
/// yang mengukur apakah 8 wire benar-benar berada di dalam FlattenZone. Begitu
/// juga "sudah urut" : yang menjawab <see cref="InnerWireOrdering.IsComplete"/>.
/// Komponen ini hanya menyusun urutan tahapnya dan menyiarkan perubahannya.
///
/// Diletakkan di module Puzzle karena ia memegang ATURAN kapan sorting boleh
/// dimulai. Dependency satu arah : UI -> Puzzle -> Tools/Ordering. Komponen ini
/// tidak membaca input, tidak meraycast, tidak menyentuh CablePoint /
/// CableController / CableJacketTrim, dan tidak tahu warna apa pun.
///
/// Ordering tidak dirombak, hanya DIPAGARI : selama <see cref="CanSort"/> masih
/// false, InnerWireDragTool menolak klik sebelum sempat memanggil
/// <c>InnerWireOrdering.Grab</c> — jadi collider wire boleh tetap seperti desain
/// yang sudah ada tanpa risiko men-trigger sorting saat kabel masih acak.
///
/// PENEMPATAN : pasang pada GameObject endpoint yang sudah memegang
/// <see cref="InnerWireFlatten"/> dan <see cref="InnerWireOrdering"/> (mis. "A"),
/// supaya InnerWireDragTool menemukannya lewat HitResolver tanpa raycast
/// tambahan.
/// </summary>
public class InnerWireStageController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Pemilik bukti 'sudah datar'. Wajib.")]
    [SerializeField] private InnerWireFlatten flatten;
    [Tooltip("Sistem drag/sorting yang sudah ada. Wajib — hanya dibaca dan " +
             "dipagari, logic-nya tidak dipanggil dari sini.")]
    [SerializeField] private InnerWireOrdering ordering;

    [Header("Flatten")]
    [Tooltip("Lama animasi meratakan (detik). Pose wire diinterpolasi selama " +
             "ini; 0 = langsung jadi.")]
    [SerializeField] private float flattenDuration = 1.2f;

    // Runtime saja : tahap tidak diserialisasi supaya scene tidak menyimpan
    // progres, sama seperti status di InnerWireFlatten dan InnerWireOrdering.
    private InnerWireStage stage = InnerWireStage.Messy;

    // Dua field dengan dua tugas berbeda, dan keduanya perlu.
    //
    // `flattening` adalah status yang dibaca BeginFlatten. Ia TIDAK boleh
    // diturunkan dari handle coroutine : dengan flattenDuration = 0 seluruh
    // routine selesai di dalam StartCoroutine (MoveNext pertama berjalan
    // sinkron, dan while-nya tidak pernah masuk), sehingga handle yang
    // dikembalikan setelahnya akan menimpa penyelesaian yang sudah terjadi —
    // tahap tampak "sedang berjalan" selamanya dan button mati permanen.
    //
    // `routine` hanya untuk menghentikannya di OnDisable : Unity tidak
    // menghentikan coroutine saat komponen di-disable, hanya saat GameObject-nya
    // dinonaktifkan atau dihancurkan.
    private bool flattening;
    private Coroutine routine;

    [Header("Finish Sorting")]
    [Tooltip("Pemilik tool aktif. Saat pemain menyatakan selesai sorting, tool " +
             "aktif digeser ke RJ45Tool lewat API ToolManager yang sudah ada. " +
             "Kosong = pergeseran tool dilewati (dengan warning).")]
    [SerializeField] private ToolManager toolManager;

    public event Action<InnerWireStage> OnStageChanged;

    public InnerWireStage Stage => stage;

    public float FlattenDuration => flattenDuration;

    /// <summary>
    /// Apakah drag/sorting boleh dijalankan. False di Messy (kabel masih acak),
    /// Flattening (animasi sedang jalan), dan Completed (sudah selesai).
    /// </summary>
    public bool CanSort => stage == InnerWireStage.Flattened || stage == InnerWireStage.Sorting;

    /// <summary>
    /// Dibaca UI untuk memutuskan button masih berguna atau tidak.
    /// Wajib Stripped : jacket yang masih utuh berarti wire belum bisa
    /// dirapikan — flatten endpoint ber-jacket mustahil dimulai. Refresh
    /// button tetap lewat jalur yang sudah ada (OnEnable dan OnStageChanged
    /// di sisi button).
    /// </summary>
    public bool CanBeginFlatten =>
        flatten != null &&
        !flattening &&
        stage == InnerWireStage.Messy &&
        flatten.WiresView != null &&
        flatten.WiresView.Point != null &&
        flatten.WiresView.Point.State == CableState.Stripped;

    /// <summary>
    /// Apakah pemain boleh MENYATAKAN selesai sorting lewat
    /// <see cref="FinishSorting"/>. Benar sejak wire rapi (Flattened) — boleh
    /// juga tanpa pernah menyeret satu pun wire.
    /// </summary>
    public bool CanFinishSorting =>
        stage == InnerWireStage.Flattened || stage == InnerWireStage.Sorting;

    /// <summary>
    /// Apakah RJ45 boleh dipasang di endpoint ini. Syaratnya hanya satu :
    /// pemain sudah menyatakan selesai sorting (stage Completed). TIDAK ada
    /// pemeriksaan urutan warna — susunan salah pun boleh dilanjutkan ke
    /// pemasangan; penilaian benar/salah milik LAN Tester nanti.
    /// </summary>
    public bool CanInsertConnector => stage == InnerWireStage.Completed;

    /// <summary>
    /// Transisi MANUAL oleh pemain : sorting dinyatakan selesai, apa pun
    /// susunan warnanya. Sorting (atau Flattened) → Completed, lalu tool aktif
    /// digeser ke RJ45Tool supaya alur lanjut ke pemasangan. Setelah Completed,
    /// <see cref="CanSort"/> false sehingga gate tahap InnerWireDragTool
    /// menolak drag wire berikutnya — urutan yang dipasang adalah urutan
    /// terakhir yang dilihat pemain.
    ///
    /// Tidak memakai <see cref="InnerWireOrdering.IsComplete"/> sebagai syarat
    /// — penyelesaian sorting adalah keputusan pemain, bukan hasil pemeriksaan.
    /// </summary>
    public bool FinishSorting()
    {
        if (!CanFinishSorting)
        {
            Debug.Log(
                $"[{nameof(InnerWireStageController)}] FinishSorting diabaikan — " +
                $"stage={stage}, harus {InnerWireStage.Flattened} atau " +
                $"{InnerWireStage.Sorting}.",
                this);
            return false;
        }

        SetStage(InnerWireStage.Completed);

        if (toolManager != null)
        {
            toolManager.SetCurrentTool(ToolState.RJ45Tool);
        }
        else
        {
            Debug.LogWarning(
                $"[{nameof(InnerWireStageController)}] ToolManager belum diassign — " +
                $"tool aktif tidak digeser ke {ToolState.RJ45Tool}.",
                this);
        }

        return true;
    }

    /// <summary>
    /// Apakah tahap ini memang mengurus <paramref name="view"/>.
    ///
    /// Perlu ada karena <see cref="HitResolver"/> sengaja menembus cabang sibling :
    /// dari view endpoint B ia juga menemukan komponen milik endpoint A. Flatten
    /// dan Ordering sudah terlindung <c>Owns()</c> masing-masing, tapi gate tidak
    /// punya penjaga serupa — tanpa uji ini, tahap endpoint A akan ikut memblokir
    /// drag wire endpoint B, yang bukan urusannya.
    ///
    /// Yang dibandingkan view, bukan hierarchy : itulah yang menentukan wire mana
    /// yang benar-benar dipegang flatten dan ordering di sini.
    /// </summary>
    public bool Governs(CableInnerWiresView view)
    {
        if (view == null)
            return false;

        return (flatten != null && flatten.WiresView == view) ||
               (ordering != null && ordering.WiresView == view);
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();

        // Coroutine mati bersama komponen : pose wire dibiarkan di tengah jalan
        // akan membuat stage dan posisi tidak sinkron.
        if (!flattening)
            return;

        if (routine != null)
            StopCoroutine(routine);

        flattening = false;
        routine = null;

        if (flatten != null)
            flatten.RestorePreFlatten();

        SetStage(InnerWireStage.Messy);
    }

    /// <summary>
    /// Menyambung ke Ordering, dan MENCATAT apakah sambungannya jadi.
    ///
    /// Dicatat karena OnEnable bisa berjalan saat <see cref="ordering"/> masih
    /// null — terjadi bila komponen ditambahkan lewat script lalu field-nya diisi
    /// menyusul. Tanpa catatan ini, kegagalan langganan hanya terlihat jauh di
    /// belakang sebagai "sorting tidak pernah selesai", bukan sebagai sebabnya.
    /// Idempoten, jadi aman dipanggil ulang.
    /// </summary>
    private void Subscribe()
    {
        if (subscribed || ordering == null)
            return;

        ordering.OnDropResolved += HandleDropResolved;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || ordering == null)
            return;

        ordering.OnDropResolved -= HandleDropResolved;
        subscribed = false;
    }

    private bool subscribed;

    /// <summary>
    /// Apakah tahap benar-benar mendengar hasil drop Ordering. False = drop
    /// pemain tidak akan pernah memajukan Sorting -> Completed.
    /// </summary>
    public bool ListensToOrdering => subscribed;

    /// <summary>
    /// Memulai proses meratakan. Menolak bila tahap sudah lewat atau proses
    /// sedang berjalan, jadi klik button berulang tidak menumpuk coroutine.
    /// </summary>
    /// <returns>false = tidak dimulai; alasannya di-log.</returns>
    public bool BeginFlatten()
    {
        // Field bisa diisi setelah OnEnable (mis. komponen ditambahkan lewat
        // script). Dicoba lagi di sini, bukan diasumsikan sudah tersambung.
        Subscribe();

        if (flatten == null)
        {
            Debug.LogError(
                $"[{nameof(InnerWireStageController)}] InnerWireFlatten belum diassign — " +
                $"tidak ada yang bisa meratakan wire.", this);
            return false;
        }

        // Wajib Stripped — sama seperti CanBeginFlatten. Dicek juga di sini
        // supaya jalur non-button (ContextMenu, script) tidak bisa memulai
        // flatten pada endpoint yang jacket-nya masih utuh.
        if (flatten.WiresView == null || flatten.WiresView.Point == null ||
            flatten.WiresView.Point.State != CableState.Stripped)
        {
            Debug.Log(
                $"[{nameof(InnerWireStageController)}] BeginFlatten diabaikan — endpoint " +
                $"{(flatten.WiresView != null && flatten.WiresView.Point != null ? flatten.WiresView.Point.Endpoint.ToString() : "?")} " +
                $"belum di-strip (state={(flatten.WiresView != null && flatten.WiresView.Point != null ? flatten.WiresView.Point.State.ToString() : "tanpa CablePoint")}).",
                this);
            return false;
        }

        if (flattening)
        {
            Debug.Log(
                $"[{nameof(InnerWireStageController)}] BeginFlatten diabaikan — " +
                $"proses flatten sedang berjalan.", this);
            return false;
        }

        if (stage != InnerWireStage.Messy)
        {
            Debug.Log(
                $"[{nameof(InnerWireStageController)}] BeginFlatten diabaikan — " +
                $"stage sudah {stage}, bukan {InnerWireStage.Messy}.", this);
            return false;
        }

        if (!isActiveAndEnabled)
        {
            Debug.LogError(
                $"[{nameof(InnerWireStageController)}] Komponen tidak aktif — " +
                $"coroutine flatten tidak bisa dijalankan.", this);
            return false;
        }

        if (!flatten.PrepareFlatten())
        {
            Debug.LogError(
                $"[{nameof(InnerWireStageController)}] PrepareFlatten gagal — " +
                $"stage tetap {stage}.", this);
            return false;
        }

        // Ditandai SEBELUM StartCoroutine : dengan flattenDuration = 0, routine
        // selesai seluruhnya di dalam panggilan itu, jadi apa pun yang ditulis
        // setelahnya akan menimpa hasil yang sudah jadi.
        flattening = true;
        routine = StartCoroutine(FlattenRoutine());

        return true;
    }

    /// <summary>
    /// Menjalankan interpolasi pose, lalu MEMERIKSA hasilnya sebelum memajukan
    /// tahap. Selesainya animasi bukan bukti kabel datar — buktinya
    /// <see cref="InnerWireFlatten.IsComplete"/>. Kalau wire ternyata tidak
    /// sampai di dalam zone, pose dikembalikan dan tahap tetap Messy, sehingga
    /// kegagalan terlihat sebagai "flatten tidak jadi" alih-alih membuka sorting
    /// di atas kabel yang masih acak.
    /// </summary>
    private IEnumerator FlattenRoutine()
    {
        SetStage(InnerWireStage.Flattening);

        float elapsed = 0f;

        while (elapsed < flattenDuration)
        {
            elapsed += Time.deltaTime;

            // ApplyFlatten meng-clamp sendiri, jadi overshoot frame terakhir aman.
            flatten.ApplyFlatten(elapsed / flattenDuration);

            yield return null;
        }

        flatten.ApplyFlatten(1f);

        flattening = false;
        routine = null;

        if (!flatten.IsComplete)
        {
            Debug.LogError(
                $"[{nameof(InnerWireStageController)}] Flatten selesai tapi " +
                $"{nameof(InnerWireFlatten)}.{nameof(InnerWireFlatten.IsComplete)} masih false " +
                $"({flatten.FlattenedCount} wire di dalam zone). Pose dikembalikan, " +
                $"stage tetap {InnerWireStage.Messy}. Periksa Flatten/Log Status : " +
                $"lane harus jatuh di dalam FlattenZone.", this);

            flatten.RestorePreFlatten();
            SetStage(InnerWireStage.Messy);

            yield break;
        }

        // Datar, dan urutan warna TETAP acak : yang dipindah flatten hanya lane,
        // dan lane tidak pernah dihitung dari warna maupun dari posisi slot.
        SetStage(InnerWireStage.Flattened);
    }

    /// <summary>
    /// Satu drop sudah diproses Ordering. Yang dilakukan di sini hanya memajukan
    /// tahap — snap benar, reset salah, dan validasi warna tetap sepenuhnya milik
    /// <see cref="InnerWireOrdering"/>.
    /// </summary>
    private void HandleDropResolved(Transform wire, bool correct)
    {
        if (stage == InnerWireStage.Flattened)
            SetStage(InnerWireStage.Sorting);

        if (stage == InnerWireStage.Sorting && ordering != null && ordering.IsComplete)
            SetStage(InnerWireStage.Completed);
    }

    private void SetStage(InnerWireStage next)
    {
        if (stage == next)
            return;

        InnerWireStage previous = stage;
        stage = next;

        Debug.Log(
            $"[{nameof(InnerWireStageController)}] STAGE {previous} -> {stage}  " +
            $"canSort={CanSort}  " +
            $"flattened={(flatten != null ? flatten.FlattenedCount.ToString() : "?")}  " +
            $"ordered={(ordering != null ? ordering.IsComplete.ToString() : "?")}",
            this);

#if UNITY_EDITOR
        // Self Check menyusuri seluruh tahap. Membangunkan tangan dan button di
        // tengah pemeriksaan hanya menghasilkan animasi hantu.
        if (selfChecking)
            return;
#endif

        OnStageChanged?.Invoke(stage);
    }

    private void OnValidate()
    {
        flattenDuration = Mathf.Max(0f, flattenDuration);
    }

#if UNITY_EDITOR
    private bool selfChecking;

    [ContextMenu("Stage/Log Status")]
    private void LogStatus()
    {
        Debug.Log(
            $"[{nameof(InnerWireStageController)}] stage={stage}  canSort={CanSort}  " +
            $"canBeginFlatten={CanBeginFlatten}  running={flattening}\n" +
            $"  flatten  : {(flatten != null ? $"{flatten.FlattenedCount} rapi, complete={flatten.IsComplete}" : "belum diassign")}\n" +
            $"  ordering : {(ordering != null ? $"complete={ordering.IsComplete}" : "belum diassign")}",
            this);
    }

    /// <summary>
    /// Berjalan tanpa Play Mode, dan lewat jalur yang sesungguhnya dipakai :
    /// <see cref="InnerWireFlatten.PrepareFlatten"/> / <c>ApplyFlatten</c> yang
    /// asli, lalu <c>InnerWireOrdering.Grab</c>/<c>Drop</c> yang asli. Yang
    /// dilewati hanya coroutine-nya, karena coroutine tidak jalan di Edit Mode —
    /// jadi durasi tidak ikut diuji, tapi hasil akhirnya iya.
    ///
    /// Tidak meninggalkan jejak : pose wire, status flatten, status ordering, dan
    /// tahap dipulihkan di akhir — semuanya lewat API yang sudah ada, tanpa
    /// menambah reset API ke Ordering.
    /// </summary>
    [ContextMenu("Stage/Self Check")]
    private void SelfCheck()
    {
        bool pass = true;

        // ---- 1, 2 : reference. ----
        pass &= Check("1. flatten terpasang", flatten != null, string.Empty);
        pass &= Check("2. ordering terpasang", ordering != null, string.Empty);

        if (flatten == null || ordering == null)
        {
            Debug.Log($"[{nameof(InnerWireStageController)}] Self Check : FAIL (reference belum lengkap)", this);
            return;
        }

        // Satu daftar wire, bukan dua : kalau flatten dan ordering menunjuk view
        // berbeda, "sudah datar" dan "sudah urut" berbicara tentang wire yang
        // berlainan dan seluruh gate ini tanpa arti.
        pass &= Check(
            "flatten & ordering memakai CableInnerWiresView yang sama",
            flatten.WiresView != null && flatten.WiresView == ordering.WiresView,
            $"flatten={(flatten.WiresView != null ? flatten.WiresView.name : "null")}  " +
            $"ordering={(ordering.WiresView != null ? ordering.WiresView.name : "null")}");

        if (flatten.WiresView == null || flatten.WiresView != ordering.WiresView)
        {
            Debug.Log($"[{nameof(InnerWireStageController)}] Self Check : FAIL (view tidak sama)", this);
            return;
        }

        // Event Ordering hanya tersambung saat komponen enabled. Tanpa itu,
        // Flattened -> Sorting -> Completed tidak akan pernah terjadi.
        Subscribe();

        pass &= Check(
            "OnDropResolved tersambung",
            ListensToOrdering,
            $"active={gameObject.activeInHierarchy} enabled={enabled}");

        var wires = new List<Transform>();

        foreach (GameObject wire in flatten.WiresView.Wires)
            if (wire != null)
                wires.Add(wire.transform);

        pass &= Check("8 wire terdaftar", wires.Count == 8, $"{wires.Count}/8");

        // Gate hanya boleh mengikat wire milik tahap ini. HitResolver menembus
        // cabang sibling, jadi tanpa Governs() tahap endpoint ini akan memblokir
        // drag wire endpoint lain.
        pass &= Check("Governs(view sendiri)", Governs(flatten.WiresView), string.Empty);
        pass &= Check("Governs(null) = false", !Governs(null), string.Empty);

        bool foreignRejected = true;

        foreach (CableInnerWiresView other in FindObjectsByType<CableInnerWiresView>(FindObjectsSortMode.None))
        {
            if (other == flatten.WiresView || !Governs(other))
                continue;

            foreignRejected = false;

            Debug.LogError(
                $"[{nameof(InnerWireStageController)}] Governs('{other.name}') true padahal view " +
                $"itu bukan milik tahap ini — gate akan memblokir wire endpoint lain.", other);
        }

        pass &= Check("Governs menolak view endpoint lain", foreignRejected, string.Empty);

        var before = new Dictionary<Transform, Vector3>();

        foreach (Transform wire in wires)
            before[wire] = wire.position;

        InnerWireStage restoreStage = stage;
        selfChecking = true;

        // ---- 3, 4, 5 : peta CanSort per tahap. ----
        (InnerWireStage stage, bool canSort)[] gates =
        {
            (InnerWireStage.Messy,      false),
            (InnerWireStage.Flattening, false),
            (InnerWireStage.Flattened,  true),
            (InnerWireStage.Sorting,    true),
            (InnerWireStage.Completed,  false),
        };

        foreach ((InnerWireStage target, bool expected) in gates)
        {
            SetStage(target);

            pass &= Check(
                $"4/5. CanSort saat {target}",
                CanSort == expected,
                $"{CanSort}, harus {expected}");
        }

        SetStage(InnerWireStage.Messy);
        pass &= Check("3. stage awal = Messy", stage == InnerWireStage.Messy, stage.ToString());

        // BeginFlatten tidak boleh memulai dari tahap yang sudah lewat.
        // Jalur suksesnya tidak diuji di sini : StartCoroutine tidak berjalan di
        // Edit Mode, jadi flatten di bawah dijalankan langsung.
        SetStage(InnerWireStage.Flattening);
        pass &= Check("BeginFlatten ditolak saat Flattening", !BeginFlatten(), string.Empty);

        SetStage(InnerWireStage.Completed);
        pass &= Check("BeginFlatten ditolak saat Completed", !BeginFlatten(), string.Empty);

        SetStage(InnerWireStage.Messy);

        // ---- Flatten, lewat jalur asli. ----
        bool prepared = flatten.PrepareFlatten();
        pass &= Check("PrepareFlatten berhasil", prepared, string.Empty);

        if (!prepared)
        {
            selfChecking = false;
            SetStage(restoreStage);

            Debug.Log($"[{nameof(InnerWireStageController)}] Self Check : FAIL (PrepareFlatten gagal)", this);
            return;
        }

        SetStage(InnerWireStage.Flattening);

        flatten.ApplyFlatten(0f);

        bool atZero = true;

        foreach (Transform wire in wires)
            if (wire.position != before[wire])
                atZero = false;

        pass &= Check("ApplyFlatten(0) = pose awal", atZero, string.Empty);

        flatten.ApplyFlatten(1f);

        // ---- 6 : bukti datar datang dari zone, bukan dari selesainya animasi. ----
        pass &= Check(
            "6. flatten.IsComplete setelah flatten",
            flatten.IsComplete,
            $"{flatten.FlattenedCount}/{wires.Count} wire di dalam zone");

        // ---- 7, 8 : urutan warna TIDAK ikut selesai. ----
        pass &= Check("7. ordering.IsComplete masih false", !ordering.IsComplete, string.Empty);

        bool noneInSlot = true;

        foreach (Transform wire in wires)
        {
            InnerWireSlot landed = ordering.SlotAt(wire.position);

            if (landed == null)
                continue;

            noneInSlot = false;

            Debug.LogError(
                $"[{nameof(InnerWireStageController)}] '{wire.name}' mendarat di dalam slot " +
                $"'{landed.name}' setelah flatten — flatten tidak boleh menempatkan wire " +
                $"ke slot mana pun.", wire);
        }

        pass &= Check("8. tidak ada wire yang mendarat di slot", noneInSlot, string.Empty);

        pass &= Check(
            "8. urutan hasil flatten != urutan slot",
            !FlattenMatchesSlotOrder(wires),
            "identitas = pemain hanya perlu menarik lurus, tanpa berpikir");

        // ---- 9, 10 : sorting lewat API Ordering yang sudah ada. ----
        SetStage(InnerWireStage.Flattened);

        // Mulai dari sini event Ordering dibiarkan bekerja : perpindahan
        // Flattened -> Sorting -> Completed memang harus datang dari
        // HandleDropResolved, bukan dari SetStage manual.
        selfChecking = false;

        bool sortPath = true;

        foreach (Transform wire in wires)
        {
            InnerWireSlot slot = ordering.SlotFor(wire);

            if (slot == null)
            {
                sortPath = false;

                Debug.LogError(
                    $"[{nameof(InnerWireStageController)}] Tidak ada slot untuk '{wire.name}' — " +
                    $"nama wire tidak dikenal InnerWireColor, atau slot warnanya belum dibuat.",
                    wire);
                continue;
            }

            if (!ordering.Grab(wire))
            {
                sortPath = false;

                Debug.LogError(
                    $"[{nameof(InnerWireStageController)}] Ordering.Grab('{wire.name}') ditolak.",
                    wire);
                continue;
            }

            wire.position = slot.SnapPosition;

            if (ordering.Drop(wire))
                continue;

            sortPath = false;

            Debug.LogError(
                $"[{nameof(InnerWireStageController)}] Ordering.Drop('{wire.name}') di slot " +
                $"'{slot.name}' dinilai salah.", wire);
        }

        pass &= Check("drop di slot benar diterima Ordering", sortPath, string.Empty);
        pass &= Check("9. ordering.IsComplete setelah semua benar", ordering.IsComplete, string.Empty);
        pass &= Check("10. stage mencapai Completed", stage == InnerWireStage.Completed, stage.ToString());

        // ---- Pulihkan status Ordering lewat API-nya sendiri. ----
        // Drop di luar semua slot = jalur "salah" yang sudah ada : ResetToOrigin
        // mengembalikan posisi dan menandai wire Loose. Jadi tidak perlu reset
        // API baru di Ordering.
        selfChecking = true;

        foreach (Transform wire in wires)
        {
            if (!ordering.Grab(wire))
                continue;

            wire.position += Vector3.up * 1000f;
            ordering.Drop(wire);
        }

        pass &= Check("status Ordering dipulihkan", !ordering.IsComplete, string.Empty);

        flatten.RestorePreFlatten();

        pass &= Check("flatten.IsComplete kembali false", !flatten.IsComplete,
            $"{flatten.FlattenedCount} wire masih tercatat rapi");

        bool restored = true;

        foreach (Transform wire in wires)
            if (wire.position != before[wire])
            {
                restored = false;

                Debug.LogError(
                    $"[{nameof(InnerWireStageController)}] '{wire.name}' tidak kembali ke pose " +
                    $"sebelum Self Check : {before[wire]:F6} -> {wire.position:F6}", wire);
            }

        pass &= Check("pose wire dipulihkan", restored, string.Empty);

        selfChecking = false;

        // Progres flatten dan ordering baru saja dinolkan, jadi tahap yang jujur
        // adalah Messy — bukan tahap sebelum Self Check.
        SetStage(InnerWireStage.Messy);

        Debug.Log(
            $"[{nameof(InnerWireStageController)}] Self Check : {(pass ? "PASS" : "FAIL")}  " +
            $"(stage dikembalikan ke {stage}; sebelum Self Check {restoreStage})",
            this);
    }

    /// <summary>
    /// Apakah wire hasil flatten sudah berbaris persis seperti urutan slot.
    /// Kalau ya, sorting hanya tinggal delapan tarikan lurus — inilah yang
    /// dicegah shuffle di <see cref="InnerWireFlatten.PrepareFlatten"/>.
    ///
    /// Perbandingannya di sini, bukan di InnerWireFlatten : yang tahu warna slot
    /// adalah Puzzle, dan flatten harus tetap buta warna.
    /// </summary>
    private bool FlattenMatchesSlotOrder(List<Transform> wires)
    {
        Vector3 axis = flatten.LaneAxis;

        var slots = new List<InnerWireSlot>();

        foreach (InnerWireSlot slot in ordering.Slots)
            if (slot != null)
                slots.Add(slot);

        if (wires.Count == 0 || wires.Count != slots.Count)
            return false;

        var ranked = new List<Transform>(wires);

        ranked.Sort((a, b) => Vector3.Dot(a.position, axis).CompareTo(Vector3.Dot(b.position, axis)));
        slots.Sort((a, b) => Vector3.Dot(a.SnapPosition, axis).CompareTo(Vector3.Dot(b.SnapPosition, axis)));

        for (int i = 0; i < ranked.Count; i++)
        {
            if (!InnerWireColor.TryResolve(ranked[i].name, out CableColor color))
                return false;

            if (color != slots[i].AcceptedColor)
                return false;
        }

        return true;
    }

    private static bool Check(string label, bool condition, string detail)
    {
        string suffix = string.IsNullOrEmpty(detail) ? string.Empty : $"  ({detail})";

        if (condition)
            Debug.Log($"  PASS  {label}{suffix}");
        else
            Debug.LogWarning($"  FAIL  {label}{suffix}");

        return condition;
    }
#endif
}
