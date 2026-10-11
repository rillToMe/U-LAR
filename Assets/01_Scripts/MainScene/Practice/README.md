# Practice Module

Pengatur alur praktikum (practice flow). Modul ini **hanya** menjawab dua
pertanyaan: "sekarang langkah ke berapa" dan "aksi ini boleh atau tidak".

## Batasan

Modul ini tidak membaca input, tidak menyentuh `ToolManager` / `ToolState` /
`InputMode`, tidak menyentuh UI, dan tidak pernah menulis state `CablePoint`
maupun `RJ45Point`. Mutasi state tetap milik `*Controller` dan tool.

Arah dependency satu arah: **tool yang bertanya ke `PracticeManager`**, bukan
sebaliknya. `PracticeManager` tidak boleh menyimpan referensi ke tool.

## File

| File | Isi |
|---|---|
| `PracticeStep.cs` | `enum PracticeStep` (urutan langkah) + `enum PracticeSide` (ujung A/B) |
| `PracticeValidator.cs` | `enum PracticeAction` + tabel aturan aksi↔step. `static`, stateless |
| `PracticeState.cs` | Mesin state urutan langkah. Plain C#, tanpa `UnityEngine` |
| `PracticeManager.cs` | MonoBehaviour service: menerjemahkan `CablePoint` → `PracticeSide`, mem-publish `OnStepChanged` |

Pembagiannya begitu supaya aturan alur bisa di-test tanpa scene: `PracticeState`
dan `PracticeValidator` tidak butuh GameObject sama sekali.

## Alur langkah

```
StripPointA → InsertRJ45PointA → CrimpPointA
            → StripPointB → InsertRJ45PointB → CrimpPointB
            → LANTest → Finished
```

Satu `PracticeAction` tepat memetakan ke satu `PracticeStep`
(lihat `PracticeValidator.RequiredStep`), jadi validasi hanya satu
perbandingan dan tidak ada tabel ganda yang bisa desync dengan enum.

## Pemakaian dari tool

`PracticeManager` diminta lewat `[SerializeField]` seperti dependency lain di
project ini — jangan `FindObjectOfType`, jangan singleton statis.

```csharp
public class WireStripper : MonoBehaviour, ITool
{
    [SerializeField] private PracticeManager practiceManager;

    public bool Execute(RaycastHit hit)
    {
        CablePoint point = HitResolver.Resolve<CablePoint>(hit);
        if (point == null) return false;

        if (practiceManager != null && !practiceManager.CanStrip(point))
            return false;

        // ... mutasi state di sini (atau via controller) ...

        practiceManager?.CompleteCurrentStep();
        return true;
    }
}
```

Null-check-nya sengaja: selama field belum di-wire di scene, tool tetap
berjalan seperti sebelum modul ini ada. Sprint ini **belum** menyentuh file
tool mana pun, jadi tidak ada perubahan behaviour pada flow yang sudah jalan.

## Testing

`PracticeState` dan `PracticeValidator` bisa dipakai langsung:

```csharp
var state = new PracticeState();
Assert.AreEqual(PracticeStep.StripPointA, state.CurrentStep);
state.Advance();
Assert.AreEqual(PracticeStep.InsertRJ45PointA, state.CurrentStep);
```

`PracticeManager` tidak butuh Inspector untuk di-test — pakai
`Configure(pointA, pointB, startStep)`.

Belum ada test asmdef di project ini; menambahkan test butuh membuatnya dulu.

## Belum termasuk (sengaja)

Save system, score, hint, UI, animation, validator urutan warna kabel, dan
logic LAN tester. Sprint ini hanya state machine flow.
