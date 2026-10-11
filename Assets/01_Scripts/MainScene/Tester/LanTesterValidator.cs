using System;
using System.Collections.Generic;

/// <summary>
/// Validasi kelistrikan instalasi UTP terhadap pola target (mis. T568A/B).
/// Murni fungsi : membaca <see cref="CableInstallationState"/>, membandingkan
/// OrderA/OrderB dengan dua <see cref="CablePatternData"/>, dan mengembalikan
/// <see cref="LanTestResult"/>. Tidak menyentuh Transform, tidak mengubah
/// state, tidak mengoreksi apa pun.
///
/// Mode koneksi DIBACA dari state, tidak ditebak dari nama pola : T568A bisa
/// menjadi ujung kabel Straight (A-A) maupun Cross (A-B). Straight valid bila
/// kedua ujung cocok pola yang SAMA ; Cross bila keduanya cocok pola yang
/// BERLAWANAN. Kedua pola dibandingkan ISINYA — urutan parameter bebas,
/// asalkan berbeda dan masing-masing valid.
///
/// Diagnostik pin : ujung yang tidak cocok pola mana pun dilaporkan terhadap
/// pola yang paling mirip (salah pin tersedikit ; seri → parameter pertama) —
/// ini dugaan maksud pemain demi laporan yang berguna, bukan bagian dari
/// keputusan valid (ujung tetap tidak valid).
/// </summary>
public static class LanTesterValidator
{
    /// <summary>
    /// Membandingkan instalasi dengan dua pola target menurut mode koneksi
    /// yang tercatat di state. Aman dipanggil kapan pun — read-only total.
    /// </summary>
    /// <exception cref="ArgumentNullException">state atau salah satu pola null.</exception>
    /// <exception cref="ArgumentException">pola tidak valid (warna kembar) atau keduanya sama isi.</exception>
    public static LanTestResult Validate(
        CableInstallationState state,
        CablePatternData pattern1,
        CablePatternData pattern2)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        if (pattern1 == null)
            throw new ArgumentNullException(nameof(pattern1));

        if (pattern2 == null)
            throw new ArgumentNullException(nameof(pattern2));

        if (!pattern1.IsValid() || !pattern2.IsValid())
            throw new ArgumentException("Kedua pola harus valid (8 warna unik tiap pola).");

        if (SameContent(pattern1, pattern2))
            throw new ArgumentException("Kedua pola tidak boleh sama isi — mode Cross butuh dua pola berbeda.");

        CableColor[] orderA = state.EndpointA?.Order;
        CableColor[] orderB = state.EndpointB?.Order;
        bool readableA = IsReadable(orderA);
        bool readableB = IsReadable(orderB);

        CablePatternData matchedA = readableA ? MatchPattern(orderA, pattern1, pattern2) : null;
        CablePatternData matchedB = readableB ? MatchPattern(orderB, pattern1, pattern2) : null;

        // Acuan mode : ujung A kalau ia cocok suatu pola, kalau tidak pola
        // termirip, kalau tetap tidak bisa (tak terbaca) parameter pertama.
        CablePatternData basePattern =
            matchedA ??
            (readableA ? ClosestPattern(orderA, pattern1, pattern2) : null) ??
            pattern1;

        CablePatternData requiredA = readableA ? basePattern : null;
        CablePatternData requiredB = !readableB
            ? null
            : state.Type == ConnectionType.Cross
                ? Other(basePattern, pattern1, pattern2)
                : basePattern;

        var result = new LanTestResult
        {
            ConnectionType = state.Type,
            EndpointA = BuildEndpoint(CableEndpoint.A, orderA, matchedA, requiredA),
            EndpointB = BuildEndpoint(CableEndpoint.B, orderB, matchedB, requiredB)
        };

        result.IsValid = result.EndpointA.IsValid && result.EndpointB.IsValid;

        return result;
    }

    private static LanEndpointResult BuildEndpoint(
        CableEndpoint endpoint,
        CableColor[] order,
        CablePatternData matched,
        CablePatternData required)
    {
        var result = new LanEndpointResult
        {
            Endpoint = endpoint,
            OrderReadable = IsReadable(order),
            MatchedPattern = matched,
            RequiredPattern = required
        };

        if (!result.OrderReadable || required == null)
            return result;

        var wrong = new List<int>();

        for (int pin = 1; pin <= CablePatternData.PinCount; pin++)
        {
            CableColor expected = required.ColorAt(pin);
            CableColor actual = order[pin - 1];

            if (expected == actual)
                continue;

            wrong.Add(pin);

            if (result.FirstWrongPin == 0)
            {
                result.FirstWrongPin = pin;
                result.ExpectedColor = expected;
                result.ActualColor = actual;
            }
        }

        result.WrongPins = wrong.ToArray();

        return result;
    }

    /// <summary>Pola (dari dua parameter) yang persis sama dengan order. Null bila tidak ada.</summary>
    private static CablePatternData MatchPattern(
        CableColor[] order,
        CablePatternData pattern1,
        CablePatternData pattern2)
    {
        if (!IsReadable(order))
            return null;

        for (int pin = 1; pin <= CablePatternData.PinCount; pin++)
            if (order[pin - 1] != pattern1.ColorAt(pin))
                return EqualsEveryPin(order, pattern2) ? pattern2 : null;

        return pattern1;
    }

    /// <summary>Pola dengan jumlah pin salah paling sedikit. Seri → pattern1 (deterministik).</summary>
    private static CablePatternData ClosestPattern(
        CableColor[] order,
        CablePatternData pattern1,
        CablePatternData pattern2)
    {
        if (!IsReadable(order))
            return null;

        int wrong1 = CountMismatch(order, pattern1);
        int wrong2 = CountMismatch(order, pattern2);

        return wrong1 <= wrong2 ? pattern1 : pattern2;
    }

    private static CablePatternData Other(
        CablePatternData pattern,
        CablePatternData pattern1,
        CablePatternData pattern2) =>
        pattern == pattern1 ? pattern2 : pattern1;

    private static bool EqualsEveryPin(CableColor[] order, CablePatternData pattern)
    {
        for (int pin = 1; pin <= CablePatternData.PinCount; pin++)
            if (order[pin - 1] != pattern.ColorAt(pin))
                return false;

        return true;
    }

    private static int CountMismatch(CableColor[] order, CablePatternData pattern)
    {
        int wrong = 0;

        for (int pin = 1; pin <= CablePatternData.PinCount; pin++)
            if (order[pin - 1] != pattern.ColorAt(pin))
                wrong++;

        return wrong;
    }

    private static bool SameContent(CablePatternData a, CablePatternData b)
    {
        for (int pin = 1; pin <= CablePatternData.PinCount; pin++)
            if (a.ColorAt(pin) != b.ColorAt(pin))
                return false;

        return true;
    }

    private static bool IsReadable(CableColor[] order) =>
        order != null && order.Length == CablePatternData.PinCount;
}
