using System;
using UnityEngine;

/// <summary>
/// Arbitrase input global. Hanya satu mode yang aktif dalam satu waktu,
/// sehingga CableInput dan ToolInput tidak pernah memproses klik yang sama.
///
/// Mode awal BUKAN milik service ini — pemiliknya <see cref="InputModeSwitcher"/>.
/// Sebelum ada yang menetapkannya, service berada dalam keadaan *belum
/// terkonfigurasi* dan <see cref="Is"/> selalu false. Itu disengaja : dulu field
/// statisnya default ke DragCable, sehingga setiap kondisi "belum/keburu
/// ter-reset" tidak terlihat sebagai masalah — ia menyamar sebagai DragCable yang
/// sah, tanpa log, dan ToolInput memblokir klik tanpa alasan yang bisa dilacak.
/// Sekarang keadaan itu berteriak, bukan berpura-pura.
/// </summary>
public static class InputModeService
{
    private static InputMode current;
    private static bool configured;

    public static event Action<InputMode> OnModeChanged;

    public static bool IsConfigured => configured;

    public static InputMode Current => current;

    /// <summary>
    /// False untuk semua mode selama belum dikonfigurasi. Tidak ada fallback
    /// diam-diam ke DragCable.
    /// </summary>
    public static bool Is(InputMode mode)
    {
        if (!configured)
        {
            WarnUnconfigured();
            return false;
        }

        return current == mode;
    }

    public static void SetMode(InputMode mode)
    {
        // Penulisan pertama harus tetap lewat, walau nilainya sama dengan
        // default enum (DragCable = 0). Tanpa cek `configured` di sini,
        // SetMode(DragCable) pada scene yang baru mulai akan early-return dan
        // service tetap dianggap belum terkonfigurasi.
        if (configured && current == mode)
            return;

        InputMode previous = current;
        bool first = !configured;

        current = mode;
        configured = true;

        Debug.Log(
            $"[InputModeService] SetMode {(first ? "(unconfigured)" : previous.ToString())} -> {mode}\n" +
            $"caller={Caller()}");

        OnModeChanged?.Invoke(mode);
    }

    /// <summary>
    /// Field statis tidak ikut ter-reset saat Domain Reload dimatikan, jadi sisa
    /// mode dari sesi Play sebelumnya (atau dari ContextMenu di Edit Mode) harus
    /// dibuang di sini.
    ///
    /// Yang dikembalikan adalah keadaan *belum terkonfigurasi*, bukan DragCable :
    /// reset ini tidak berhak menentukan mode apa pun. Kalau ia kebetulan
    /// berjalan setelah InputModeSwitcher.Awake(), akibatnya sekarang terlihat
    /// sebagai warning "belum dikonfigurasi", bukan mode DragCable yang salah.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Debug.Log(
            $"[InputModeService] RESET  wasConfigured={configured} current={current}\n" +
            $"caller={Caller()}");

        current = default;
        configured = false;
        OnModeChanged = null;
    }

    private static bool warned;

    private static void WarnUnconfigured()
    {
        if (warned)
            return;

        warned = true;

        Debug.LogError(
            "[InputModeService] Mode dibaca sebelum dikonfigurasi. " +
            "Pasang InputModeSwitcher di scene dan set Startup Mode-nya — " +
            "selama ini belum terjadi, seluruh input diblokir.\n" +
            $"caller={Caller()}");
    }

    /// <summary>
    /// Pemanggil di luar file ini. Dipakai untuk melacak siapa yang mengubah
    /// mode; StackTrace hanya dibangun saat mode benar-benar berubah, bukan per
    /// frame.
    /// </summary>
    private static string Caller()
    {
        var trace = new System.Diagnostics.StackTrace(1, false);
        string chain = string.Empty;
        int shown = 0;

        for (int i = 0; i < trace.FrameCount && shown < 4; i++)
        {
            var method = trace.GetFrame(i).GetMethod();

            if (method == null || method.DeclaringType == typeof(InputModeService))
                continue;

            chain += (chain.Length > 0 ? " <- " : string.Empty) +
                     $"{method.DeclaringType?.Name}.{method.Name}";
            shown++;
        }

        return chain.Length > 0 ? chain : "(tidak diketahui)";
    }
}
