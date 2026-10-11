/// <summary>
/// Satu-satunya tempat yang menerjemahkan nama GameObject inner wire menjadi
/// <see cref="CableColor"/>.
///
/// Nama wire datang dari Cable.fbx dan tidak boleh diketik ulang di tempat lain :
/// begitu string yang sama muncul di dua file, rename satu node FBX cukup untuk
/// membuat ordering diam-diam salah. Semua konsumen (slot, validator, creator)
/// memanggil <see cref="TryResolve"/>.
///
/// Gagal itu eksplisit, bukan default : nama yang tidak dikenal mengembalikan
/// false supaya pemanggil bisa melaporkannya, bukan menganggapnya warna pertama.
/// </summary>
public static class InnerWireColor
{
    /// <summary>
    /// Nama node FBX -> warna. Cocokkan setelah Trim, tanpa toleransi lain :
    /// hierarchy wire memang tidak boleh diubah di milestone ini, jadi nama yang
    /// menyimpang berarti ada yang salah dan harus terlihat.
    /// </summary>
    public static bool TryResolve(string wireName, out CableColor color)
    {
        switch (wireName != null ? wireName.Trim() : string.Empty)
        {
            case "Biru":         color = CableColor.Blue;        return true;
            case "Biru-Putih":   color = CableColor.WhiteBlue;   return true;

            case "Hijau":        color = CableColor.Green;       return true;
            case "Hijau-Putih":  color = CableColor.WhiteGreen;  return true;

            case "Oren":         color = CableColor.Orange;      return true;
            case "Oren-Putih":   color = CableColor.WhiteOrange; return true;

            case "Coklat":       color = CableColor.Brown;       return true;
            case "Coklat-Putih": color = CableColor.WhiteBrown;  return true;

            default:             color = default;                return false;
        }
    }
}
