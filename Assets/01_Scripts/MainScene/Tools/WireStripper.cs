using UnityEngine;

/// <summary>
/// Memotong (strip) jacket satu ujung kabel — selama UJUNG ITU sudah dipilih.
///
/// Pemilihan ujung adalah KLIK PERTAMA, dan yang menandainya adalah kamera:
/// kali pertama sebuah ujung diklik, tool ini hanya memindahkan kamera ke view
/// ujung tersebut (lewat <see cref="CameraDirector"/>) dan belum menyentuh
/// state. Klik berikutnya, saat kamera memang sudah di ujung itu, baru
/// memotong. Jadi kabel tidak pernah terpotong oleh klik yang cuma dimaksudkan
/// untuk melihat ujungnya.
///
/// Sumber kebenaran "ujung mana yang sedang dipilih" adalah view yang hidup di
/// CameraDirector — bukan bool di tool ini. Kalau disimpan sendiri, ia bisa
/// desync dengan kamera (mis. setelah crimp mengembalikan kamera ke overview).
///
/// Camera Director diisi lewat Inspector. Kalau KOSONG (mis. scene test tanpa
/// rig kamera), tool kembali ke perilaku lama : satu klik langsung memotong.
/// </summary>
public class WireStripper : MonoBehaviour, ITool
{
    [Header("References")]
    [Tooltip("Penunjuk kamera per ujung. Diisi = klik pertama hanya memilih " +
             "(fokus kamera) dan klik kedua memotong. Kosong = klik langsung " +
             "memotong (perilaku lama).")]
    [SerializeField] private CameraDirector cameraDirector;

    public ToolState Id => ToolState.WireStripper;

    public bool Execute(RaycastHit hit)
    {
        // Collider ada di "Handle", CablePoint ada di parent-nya.
        CablePoint point = HitResolver.Resolve<CablePoint>(hit);

        if (point == null)
            return false;

        if (point.State != CableState.Intact)
            return false;

        // Langkah 1 : pilih ujungnya. Belum ada yang berubah selain kamera.
        if (cameraDirector != null && !cameraDirector.IsFocusedOn(point.Endpoint))
        {
            cameraDirector.FocusEndpoint(point.Endpoint);

            Debug.Log(
                $"{name} : ujung {point.Endpoint} dipilih — klik lagi untuk " +
                $"memotong jacket.");

            return true;
        }

        // Langkah 2 : ujung ini sudah dipilih, sekarang potong. Trim jacket
        // mengikuti dari sini : CableController mendengar OnStateChanged dan
        // meneruskannya ke CableJacketTrim — ujung mana yang dipotong ditentukan
        // oleh CablePoint.Endpoint, bukan oleh tool ini. Tool tidak menyentuh
        // mesh, posisi endpoint, maupun kurva.
        point.State = CableState.Stripped;

        // ALUR TOOL. Stripping membuka inner-wire gameplay : tool aktif
        // langsung digeser ke CopperTool (yang di-registry adalah
        // InnerWireDragTool), sehingga drag wire bisa langsung dipakai tanpa
        // switch manual. Dilakukan di sini — bukan di event CablePoint — karena
        // ini satu-satunya titik yang tahu stripping benar-benar selesai.
        //
        // Endpoint-independent : alur praktikum sekarang selalu berakhir di
        // WireStripper (crimp menggeser tool ke sini), jadi strip ujung mana
        // pun — A dulu, B dulu, maupun bolak-balik — selalu melanjutkan ke
        // alur wire ujung itu sendiri. Tanpa gate endpoint, strip B lebih
        // dulu tidak lagi bisa mengunci drag A.
        //
        // ToolManager dicari dari parent, bukan dari Inspector : WireStripperTool
        // sudah berada di bawah ToolsManager, jadi tidak ada wiring baru. Kalau
        // tidak ketemu (mis. tool dipakai di scene test tanpa manager), strip
        // tetap berhasil — hanya saja tool tidak berpindah.
        ToolManager manager = GetComponentInParent<ToolManager>();

        if (manager != null)
            manager.SetCurrentTool(ToolState.CopperTool);
        else
            Debug.LogWarning(
                $"{name}: ToolManager tidak ketemu di parent — tool tetap " +
                $"WireStripper setelah strip.",
                this);

        Debug.Log($"{point.name} ({point.Endpoint}) stripped.");

        return true;
    }
}
