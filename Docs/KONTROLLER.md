# HAREKÂT — Kontroller (nihai tablo)

Kaynak: `Application/Services/ControlScheme.cs` (bağlam tablosu + test), `InputBindingMap.cs` (yeniden atanabilir eylemler),
`Infrastructure/Input/*`, `Presentation/UI/OverlayState.cs` (katman sahipliği). "Atanabilir" tuşlar
**Ayarlar > Tuş Atama** panelinden değiştirilir; sabit tuşlar değiştirilemez ve başka eyleme **atanamaz**.

## 1. Yaya (oynanış)

| Tuş | İşlev | Atanabilir |
|-----|-------|-----------|
| W A S D / Fare | Hareket / bakış | evet (WASD) |
| Shift (basılı) | Koşu | evet |
| Space | Zıpla | evet |
| Ctrl (basılı) / C (aç-kapa) | Çömel | evet |
| Z | Yüzüstü | evet |
| Q / E (basılı) | Sola / sağa eğil | evet |
| Sol tık / Sağ tık (basılı) | Ateş / nişan | hayır |
| R | Şarjör değiştir | evet |
| F | Etkileşim: eşya al, araca bin | evet |
| F (basılı) | Yakındaki yaralı müttefiki kaldır (6 sn, Sıhhiyeci 3 sn) | evet (Etkileşim ile aynı) |
| 1 2 3 4 / fare tekeri | Silah seç / sıradaki silah | evet (1-4) |
| B | Ateş modu | evet |
| H / J | İyileş / güçlendirici | evet |
| G / T | El bombası / sis bombası | evet |
| X | Silahı indir | evet |
| Tab veya I | Envanter | evet |
| M | Tam harita | evet |

## 2. Araç / intikal

| Tuş | İşlev |
|-----|-------|
| W / S, A / D | Gaz-fren, direksiyon (sürücü) |
| Space (Zıpla tuşu) | El freni |
| 1 / 2 (Silah 1 / Silah 2 tuşları) | Kirpi: sürücü koltuğu / taret nişancı koltuğu |
| Sol tık, R (Şarjör tuşu) | Taret ateş, mermi yükle (nişancı koltuğunda) |
| F | Araçtan / helikopterden in |

Araçtayken silah tuşları silah seçmez (silah çalışmaz); 1/2 yalnızca koltuk değiştirir.

## 3. Tim / komuta (yaya, araç ve açık haritada çalışır)

| Tuş | İşlev |
|-----|-------|
| F1 / F2 / F3 / F4 | Emir: Takip / Mevzi / Taarruz / Toplan (Komutan) |
| V | Topçu atışı (Komutan veya Telsizci) |
| U | İHA keşfi (Komutan veya Telsizci; tim başı 120 sn bekleme) |
| L | T-129 ATAK desteği (yalnızca Komutan; yeniden atanabilir "T-129 ATAK desteği"). Tim 8 öldürme yapınca ya da 300 sn dolunca açılır; hedef nişan noktası, yoksa harita işareti. Helikopter 25 sn çevreler, 20 mm top + 2 füze salvosu atar, düşürülebilir (1500 can). YZ komutanlar da kullanır |
| Fare orta tuş (dokun) | Ping |
| Fare orta tuş ya da Y (basılı) | Komut çarkı; bırakınca seç, Esc iptal |
| CapsLock (basılı) | Skor tablosu |

Haritada V / U hedefi harita işaretidir (sağ tık ile işaretlenir).

## 4. Harita ve envanter (oyun girdisi kapalı)

| Tuş | İşlev |
|-----|-------|
| M / Tab, I | Aç-kapa (biri açıkken diğerine basmak ötekini kapatıp açar) |
| Sağ tık | Harita: işaret koy |
| Space | Harita: oyuncuya odakla |
| + / - | Harita yakınlaştır / uzaklaştır |
| Esc | Önce haritayı / envanteri kapatır |

## 5. İzleyici (ölünce)

| Tuş | İşlev |
|-----|-------|
| E / Q | Sonraki / önceki oyuncu |
| Fare tekeri | Yakınlaştır |
| Sağ tık (basılı) + fare | Kamerayı döndür |
| M | Harita |

Q/E burada hedef değiştirir; yaya oynanışta eğilmedir (bağlamlar ayrıktır).

## 6. Eğitim, konsol, geliştirici

| Tuş | İşlev |
|-----|-------|
| N / F9 | Eğitim: adımı atla / tümünü atla (yalnızca eğitim çalışırken, duraklatma/konsol kapalıyken) |
| Enter | Eğitim özet kartını kapat |
| ` (Backquote) | Geliştirici konsolu (Editör / Development / `-dev`) |
| Konsolda Enter / Tab / ↑ ↓ | Çalıştır / tamamla / geçmiş |
| F10 | Performans göstergesi (eskiden F3'tü; F3 = Taarruz emri) |
| Noclip (konsol) | Hareket tuşları + Zıpla (yukarı) / Eğil (aşağı) + Koş (hızlı) — atamaya uyar |

## 7. Gamepad

Sol çubuk hareket, sağ çubuk bakış, A zıpla, B çömel, X şarjör, Y etkileşim, LB iyileş, RB el bombası,
RT ateş, LT nişan, L3 koş, R3 yüzüstü, D-pad silah 1-4, Start duraklat, Select harita.

## 8. Katman sahipliği (Esc, imleç, zaman)

Esc **her zaman en üstteki katmana** gider; bir basış tek katmanı kapatır:

1. Geliştirici konsolu / komut çarkı / kozmetik paneli (geçici katmanlar, `OverlayState`)
2. Duraklatma menüsündeki onay / ayarlar / tuş atama (tuş yakalarken Esc yalnızca yakalamayı iptal eder)
3. Tam harita, ardından envanter
4. Duraklatma menüsü (aç / "Devam Et" ile kapat)
5. Maç sonu ekranı açıkken Esc oyun içi katman açmaz

| Katman | İmleç | Oyun girdisi | `Time.timeScale` |
|--------|-------|--------------|------------------|
| Oynanış | kilitli | açık | 1 |
| Komut çarkı | kilitli (fare çarkı sürer) | kapalı | 0,25 (yalnızca çevrimdışı) |
| Harita / envanter | serbest (`MapOverlayInput`) | kapalı | 1 |
| Konsol | serbest | kapalı (yazı girişi; M/Tab/I/F1-F4/Y/U/V/N/Enter çalışmaz, skor tablosu gizli) | değişmez |
| Duraklatma / ayarlar | serbest | kapalı | 0 |
| Maç sonu | serbest | kapalı | 1 |
| Ana menü (+ kozmetik) | serbest | — | 1 |

Çakışma kuralları:

- **F**: yakında yaralı müttefik varsa F = kaldırma (basılı); eşya/araç için yaralıdan uzaklaşın. Araçta/intikalde F = in.
- **1 / 2**: yaya = silah; Kirpi sürücüsü = koltuk. Araçta silah seçimi yoktur.
- **Q / E**: yaya = eğil, ölü = izleyici hedefi, noclip artık Zıpla/Eğil kullanır.
- **F3** yalnızca Taarruz emri; performans göstergesi **F10**.
- **Tab**: oyunda envanter, konsolda tamamlama (konsol açıkken envanter açılmaz).
- **Esc**: konsol/çark kapanırken duraklatma açılmaz (aynı basışta iki katman olmaz).
- Ayrılmış tuşlar atanamaz: Esc, F1-F4, F9, F10, `, CapsLock, Y, U, V, N, Enter.

## Maç durumu paneli (P)

- **P (basılı)**: maç durumu paneli — hayatta tim/oyuncu, sıralama tahmini, tim tablosu (durum/leş/hasar), leş liderleri ilk 3, bölge fazı + sonraki daralma sayacı. Envanter Tab/I, tam skor tablosu CapsLock olduğundan çakışmaması için ayrı tuş seçildi; imleç serbestken (envanter, harita, konsol, duraklatma) açılmaz.
