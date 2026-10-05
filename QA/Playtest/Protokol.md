# Playtest Protokolü

**Amaç:** HAREKÂT’ın tim BR döngüsünü gerçek oyuncu oturumunda gözlemlemek; anket + metriklerle karar vermek.  
**Süre:** 90–120 dk (brief + 2 maç + debrief)  
**Oyuncu:** 8–20 (tercihen 10’luk en az bir tim)  
**Gözlemci:** 1–2 (oynamaz; form doldurur)

---

## 1. Oturum öncesi (T−30 dk)

1. Smoke (`QA/Smoke.md`) yeşil veya bilinen sapmalar listelenmiş.
2. Build kimliği, seed (varsa), zorluk, intikal tercihi kaydı.
3. Kayıt: ekran + (izinli) ses; kişisel veri yok.
4. Anket linki / `Anket.html` hazır; çıktı `anket-sonuc-YYYYMMDD.json`.
5. Oyunculara kurgu hatırlatması: Mavi/Kırmızı tatbikat; gerçek örgüt adı yok.
6. Kontroller özeti: WASD, Shift, C/Z, fare, 1–3 silah, R, F, Tab, M, F1–F4, V, Esc.

## 2. Brief (10 dk)

| Madde | İçerik |
|-------|--------|
| Hedef | Eğlence değil; **bulgu** ve **sarı bayrak** |
| Kurallar | Takım sesi serbest; hile yok; yapıcı dil |
| Görev | 1) İntikal+yağma 2) Tim emri 3) Zone yönetimi |
| Gözlemci | Sorun anında “zaman kodu + belirti” notu |

## 3. Maç akışı (2× ≈25–35 dk)

### Maç A — Öğrenme

- 4 tim × bot dolgulu veya kısmi insan; Normal zorluk.
- Gözlem odak: intikal, yağma, HUD okunabilirliği, ilk ölüm deneyimi.

### Maç B — Baskı

- Mümkünse daha dolu lobi / Hard veya yoğun çatışma sektörü.
- Gözlem odak: komuta devri, topçu, zone paniği, performans hissedişi.

Maç arası 5 dk: kısa sözlü tur (“en sinir bozucu an?”) — anketi bozmadan.

## 4. Gözlemci checklist (maç içi)

Her olay satırı: `mm:ss | oyuncu/tim | belirti | şiddet 1–5 | senaryo?`

İzle:

- İniş güvenliği / takılma  
- Silah edinme kafa karışıklığı  
- Dost ateşi / işaret eksikliği  
- Emirlerin anlaşılması (F1–F4)  
- Zone uyarı gecikmesi  
- FPS “mikro takılma” şikâyeti  
- Menü/harita imleç kilidi  
- Bağlantı (online ise)

Detay form: `Gozlem_Formu.md`.

## 5. Debrief (15–20 dk)

1. Sessiz 2 dk bireysel not.
2. Tur: her oyuncu **bir** iyi / **bir** kötü.
3. Anket doldurma (`Anket.html`) — gözlemci yardım eder, cevaplara yönlendirmez.
4. P0 adayı belirtiler Hata_Sablonu’na hemen dökülür.

## 6. Oturum sonrası

| Çıktı | Dosya / yer |
|-------|-------------|
| Anket JSON | `Playtest/results/` (yerel; git’e zorunlu değil) |
| Gözlem formları | taranmış veya MD |
| Metrik özeti | `Metrikler.md` şablonuna işlenmiş |
| Ticket’lar | Hata_Sablonu → triage |

## 7. Başarı tanımı (playtest oturumu)

Oturum **başarılı** sayılır eğer:

- En az 1 tam maç tamamlandı (crash’siz veya crash ticket’lı),
- Anket ≥ %70 dolduruldu,
- Gözlem formunda zaman kodlu ≥ 5 not,
- Smoke regresyonu yoksa veya yeni P0 triage edildi.
