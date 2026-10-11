/// <summary>
/// Hasil validasi SATU ujung kabel. Murni data laporan — tidak ada tindakan,
/// tidak menyentuh Transform wire, tidak dipakai mengoreksi apa pun.
/// </summary>
public class LanEndpointResult
{
    /// <summary>Ujung mana yang dilaporkan.</summary>
    public CableEndpoint Endpoint;

    /// <summary>False = Order ujung ini tidak terbaca (null / panjangnya bukan 8) — detail pin kosong.</summary>
    public bool OrderReadable;

    /// <summary>Pola yang PERSIS sama dengan urutan ujung ini. Null = tidak cocok dengan pola mana pun.</summary>
    public CablePatternData MatchedPattern;

    /// <summary>
    /// Pola yang seharusnya dipakai ujung ini menurut mode koneksi dan ujung
    /// satunya. Null hanya bila Order tidak terbaca.
    /// </summary>
    public CablePatternData RequiredPattern;

    /// <summary>Pin pertama yang salah terhadap RequiredPattern. 0 = tidak ada / tidak terbaca.</summary>
    public int FirstWrongPin;

    /// <summary>Warna yang diharapkan pada FirstWrongPin. Null bila FirstWrongPin = 0.</summary>
    public CableColor? ExpectedColor;

    /// <summary>Warna yang benar-benar ada pada FirstWrongPin. Null bila FirstWrongPin = 0.</summary>
    public CableColor? ActualColor;

    /// <summary>Semua pin yang salah (1-based, menaik). Kosong bila valid atau tidak terbaca.</summary>
    public int[] WrongPins = System.Array.Empty<int>();

    /// <summary>True = urutan terbaca dan persis sama dengan pola yang disyaratkan mode.</summary>
    public bool IsValid =>
        OrderReadable && RequiredPattern != null && MatchedPattern == RequiredPattern;
}

/// <summary>
/// Hasil validasi satu instalasi kabel end-to-end : apakah OrderA dan OrderB
/// sesuai pola target dan mode koneksi. Straight = kedua ujung pola yang sama ;
/// Cross = pola berlawanan. Data murni — tampilan/efek/suara milik pemanggil
/// (modul LAN Tester UI nanti), bukan class ini.
/// </summary>
public class LanTestResult
{
    /// <summary>Kedua ujung valid menurut mode koneksi.</summary>
    public bool IsValid;

    /// <summary>Mode yang dipakai saat validasi (dibaca dari CableInstallationState.Type).</summary>
    public ConnectionType ConnectionType;

    public LanEndpointResult EndpointA;
    public LanEndpointResult EndpointB;
}
