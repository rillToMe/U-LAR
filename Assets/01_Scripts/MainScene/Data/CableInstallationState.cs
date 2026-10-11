using System;

/// <summary>
/// State satu ujung kabel dalam potret instalasi. Murni data hasil pembacaan —
/// tidak ada referensi scene, tidak ada logika.
/// </summary>
public class CableEndpointInstallation
{
    /// <summary>
    /// State kabel hasil membaca <see cref="CablePoint.State"/> saat snapshot.
    /// Dibaca apa adanya ; mesin statenya tetap milik CablePoint.
    /// </summary>
    public CableState CableState;

    /// <summary>
    /// Tahap penggarapan inner wire (Messy/Flattening/Flattened/Sorting/Completed).
    /// Null = ujung ini belum punya <see cref="InnerWireStageController"/>
    /// (mis. endpoint B pada milestone ini).
    /// </summary>
    public InnerWireStage? Stage;

    /// <summary>
    /// Urutan wire per pin : indeks 0 = pin 1, indeks 7 = pin 8. Diambil dari
    /// sort lateral posisi world wire saat snapshot — bukan dari urutan
    /// registrasi, bukan dari posisi slot. Null = urutan ujung ini belum bisa
    /// dibaca (wire kurang dari 2, posisi berimpit, atau ada nama wire yang
    /// tidak dikenal <see cref="InnerWireColor"/>).
    /// </summary>
    public CableColor[] Order;
}

/// <summary>
/// Data runtime SATU instalasi kabel UTP — potret kondisi saat ini, bukan
/// penilaian. Semua field diisi <see cref="CableInstallationTracker"/> dari
/// sistem yang sudah ada ; tidak ada logika koreksi, tidak ada validasi, dan
/// susunan salah tetap tersimpan apa adanya sebagai susunan yang sedang berlaku.
///
/// Urutan wire disimpan sebagai warna per pin (OrderA/OrderB) supaya siap
/// dibandingkan LAN Tester nanti dengan <see cref="CablePatternData.ColorAt"/>.
/// Definisi T568A/T568B TIDAK ada di sini — pola target milik asset
/// CablePatternData, keputusan benar/salah milik LAN Tester.
///
/// Murni C# : tidak menyentuh UnityEngine, tidak diserialisasi Unity,
/// tidak tahu soal save/load.
/// </summary>
public class CableInstallationState
{
    /// <summary>Identitas instalasi. Diisi tracker dari Inspector, belum ada sistem ID global.</summary>
    public string CableId;

    /// <summary>Mode koneksi yang dipilih untuk instalasi ini (Straight / Cross). Enum existing dipakai apa adanya.</summary>
    public ConnectionType Type;

    /// <summary>Potret ujung A.</summary>
    public CableEndpointInstallation EndpointA;

    /// <summary>Potret ujung B. Tetap ada walau ujung B belum punya Ordering/Stage — Order-nya dibaca dari posisi wire.</summary>
    public CableEndpointInstallation EndpointB;

    /// <summary>
    /// Progres pemasangan murni dari mesin state yang sudah ada : kedua ujung
    /// mencapai <see cref="CableState.Connected"/>. INI BUKAN penilaian
    /// kelistrikan maupun urutan warna — validasi kebenaran instalasi milik
    /// LAN Tester nanti. Susunan salah tetap bisa "complete" di sini.
    /// </summary>
    public bool IsComplete =>
        EndpointA != null && EndpointB != null &&
        EndpointA.CableState == CableState.Connected &&
        EndpointB.CableState == CableState.Connected;

    /// <summary>
    /// Perbandingan nilai (bukan referensi) : true = dua potret identik semua
    /// field, termasuk isi array urutan pin per pin. Dipakai tracker untuk
    /// memutuskan apakah <c>OnStateChanged</c> perlu dinyalakan.
    /// </summary>
    public bool SameAs(CableInstallationState other)
    {
        if (other == null)
            return false;

        if (Type != other.Type || CableId != other.CableId)
            return false;

        return SameEndpoint(EndpointA, other.EndpointA) &&
               SameEndpoint(EndpointB, other.EndpointB);
    }

    private static bool SameEndpoint(
        CableEndpointInstallation a,
        CableEndpointInstallation b)
    {
        if (a == null || b == null)
            return a == b;

        if (a.CableState != b.CableState || a.Stage != b.Stage)
            return false;

        return SameOrder(a.Order, b.Order);
    }

    private static bool SameOrder(CableColor[] a, CableColor[] b)
    {
        if (a == null || b == null)
            return a == b;

        if (a.Length != b.Length)
            return false;

        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i])
                return false;

        return true;
    }
}
