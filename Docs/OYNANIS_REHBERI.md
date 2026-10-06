# HAREKÂT Oyuncu Rehberi

## Modlar
- **Harekât (battle royale):** Timin 10 kişidir; sen Tim Komutanısın, 9 astın yapay zekâ ile gelir. Rakip timler de 10 kişidir. Son ayakta kalan tim kazanır.
- **Atış Poligonu:** Silahları ve kontrolleri güvenle dene (hedefler, hareketli hedefler).
- **Online:** Giriş yaptıktan sonra profil, tim ve eşleştirme panelleri; sunucular Windows Dedicated Server üzerinde çalışır.

## Maçın akışı
1. **İntikal:** Timin T-70 helikopteri ya da Kirpi ile bölgeye gider; koltuğundan harita ve alan bilgisini incele.
2. **İniş:** Araç iniş noktasında durur, tim iner. İyi bir inişte hemen silah ve zırh bul.
3. **Harekât:** Mavi alan zamanla daralır; dışında hasar alırsın. Her aşamada yeni merkez ve yarıçap gösterilir.
4. **Son:** Son tim kalınca maç biter, sonuç ekranında istatistikler ve XP görünür.

## Rütbeler ve komuta zinciri
Er, Onbaşı, Çavuş, Sözleşmeli Er, Uzman Onbaşı, Uzman Çavuş, Astsubay rütbeleri (Astsubay Çavuş'tan Kıdemli Başçavuş'a), Asteğmen, Teğmen, Üsteğmen, Yüzbaşı, Binbaşı ve üstü. Kariyer rütben maç XP'si ile yükselir.
- Timde komutan en kıdemli askerdir; ast sırası rütbeye göredir.
- Komutan düşerse komuta bir sonraki kıdemliye geçer ve yapay zekâ timi o kişiyi izler. Sen öldüysen tim savaşı sürdürür.

## Tim rolleri (10 kişi)
Komutan, Nişancı, Makineli Tüfekçi, Sıhhiyeci (Medik), Telsizci, Bombacı ve 4 Piyade. Telsizci ve Komutan topçu çağırabilir; Sıhhiyeci iyileştirmede avantajlıdır.

## Tim emirleri
| Tuş | Emir | Anlamı |
|-----|------|--------|
| F1 | Takip | Seni takip etsinler |
| F2 | Mevzi | Bulunduğu yerde bekle ve mevzilen |
| F3 | Taarruz | Nişan aldığın/işaretlediğin noktaya saldır |
| F4 | Toplan | Yanına toplansınlar |
| V | Topçu | İşaretli noktaya atış (bekleme süresi vardır) |

Tam haritada (M) bir nokta işaretleyip topçuyu oraya çağırabilirsin.

## Silahlar
SAR 9 ve Canik TP9 (tabanca), SAR 109T (SMG), MPT-55 (5.56), MPT-76 ve G3A7 (7.62 piyade tüfeği), KNT-76 (3x DMR), JNG-90 Bora-12 (6x dürbünlü keskin nişancı), PMT-76 (makineli, 100 mermi), Escort (pompalı). Ayrıca parçalı bomba (G) ve sis bombası (T).

## Kontroller (özet)
WASD hareket, Shift koşu, Space zıpla, Ctrl/C çömel, Z yüzüstü, Q/E yana eğil, sol tık ateş, sağ tık nişan, R şarjör, F etkileşim, 1-4 silah, B ateş modu, H iyileş, J takviye, X silahı indir, Tab/I envanter, M harita, Esc duraklat, ` geliştirici konsolu.

## İpuçları
- İlk dakikada zırh (yelek, kask) ve iyileştirme malzemesi topla; bina içlerinde loot noktaları kat başına vardır.
- Alanı sık kontrol et; daralmadan önce hareket et, yüksek zemin ve siper kullan.
- Dost ateşi vardır; yapay zekâ timi seninle birlikte düşmanı hedefler, onları ateş hattına sokma.
- Sis bombası ile açık araziyi geç; keskin nişancıya karşı sis ve siper kullan.
- Kirpi zırhlıdır; haritada sürülebilir Kirpi'yi (F ile bin) hızlı yer değiştirmek için kullan.
- Düşme hasarı vardır; yüksekten inerken dikkat.
- Topçu çok yıkıcıdır ama ıslık sesi uyarır; kendi pozisyonuna çağırma.
- Önce poligonda silahların geri tepmesini ve menzilini öğren.

## Hareket hissi (stamina, yük, eğim) — P3

Ayarlar `PlayerMovementConfig` ("Stamina & Load") ve `MovementRules` sabitlerindedir.

- **Stamina** (100): koşu 11/sn harcar; harcamadan sonra 1,1 sn bekleme, sonra 15/sn (durarak x1,35, çömelik x1,15, yüzüstü x1,3 ek; yürürken x0,55). Sıfıra inince "tükenmiş": bekleme +0,9 sn, 25'e çıkana dek koşu/zıplama/tırmanma yok, yürüme %12 yavaş. Koşuya yeniden başlamak için en az 12.
- **Tek seferlik maliyetler**: zıplama 8, tırmanma 12, kayma 10; sert iniş (>4 m/s) (v-4)*2,6 (en çok 35) ve yatay hız kaybı (12 m/s'de %55).
- **Yük/zırh**: envanter doluluğu (%25 üstü, karesel) + zırh (yelek seviyesi + kask/2, seviye başı ~%4) -> "yük etkisi" 0..1; en çok %16 yavaş, %35 yavaş ivme/yavaşlama (atalet), %18 düşük zıplama, %70 fazla stamina harcaması, tırmanma %35 uzun. `loadEffectScale` ile ölçeklenir/kapatılır.
- **Eğim**: yokuş yukarı derece başına ~%1,1 yavaş (alt sınır %60), aşağı en çok +%6; 3 derece altı etkisiz (`slopeSpeedModifier`).
- **Duruş**: ease-in-out boy eğrisi (çömelme ~0,30 sn, yüzüstü ~0,48 sn; `stanceTransitionSpeed` ters orantılı), kamera göz yüksekliği kapsül boyunu izler; geçiş sürerken hız en çok %35 düşer.
- **Koşu ataleti**: koşuya hızlanma ivmenin %72'si, koşudan duruş %80'i.
- **Kayma sınırlı**: stamina >= 10, 2,2 sn bekleme, başlangıç hızı en çok koşu x1,08.
- **Eğilme (Q/E)**: yan ışınla duvar boşluğu kontrolü (motor + kamera), kayarken kapalı, ağır yükte %25 yavaş.
- **ENTEGRASYON (ses/HUD)**: `motor.BreathingIntensity` (0..1) nefes katmanı; `StaminaExhausted/StaminaRecovered` olayları; `FootstepTaken(şiddet)` ve `StepsPerSecond` adım ritmi; `StaminaNormalized` HUD çubuğu.

## Maç akışı ayarları (P4)

Ayrıntı ve zaman çizelgesi: `Docs/MAC_AKISI.md`.

- Bölge: 7 faz, ~11,7 dk (ilk bekleme 120 sn, 740 → 400 → 270 → 170 → 100 → 55 → 22 → 0 m; hasar 1 → 18 hp/sn).
- İkmal sandığı (`MatchConfig.Airdrop*`): 90 sn'de ilk duyuru, 110 sn arayla toplam 4; 40 sn paraşüt + 8 sn kilit; içinde 3. seviye zırh/kask + yüksek kademe silah.
- Topçu bekleme: 180 sn (tim başına).
- Yağma dolu olma: Low %38, Medium %58, High %74, Military %90; her timin inişinde garantili başlangıç kiti.
- Final çemberi arazi çıpalarına (tepe/kale/köy) kayar (`ZoneService.SetAnchors`).

## Silah dengesi ayarları (P2)

Tüm denge sayıları ve nihai tablo: `Docs/SILAH_DENGESI.md`. Ayar yerleri:

- `WeaponCatalog.cs`: hasar, atış hızı, namlu hızı, `FalloffStart/End`, `MinDamageFactor`, kafa/uzuv çarpanı, ağırlık, ADS süresi (formül: `WeaponHandling.AdsTimeForWeight`; testle ±0.03 kilitli).
- `PenetrationRules.ArmorEffectiveness` / `ArmorClassRating` / `MinArmorEffectiveness (0.4)`: zırh sınıfı vs kalibre (Sv.3 yelek 9 mm'yi durdurur, 7.62'yi %65 etkin tutar).
- `BallisticsMath.DragPerMeter`: kalibre başına sürükleme (uçuş süresi/düşüş).
- `WeaponHandling`: `AdsBase 0.12`, `AdsPerKg 0.032`, `ScopeAdsPenalty 0.04`, `SprintToFireBase 0.12`, `SprintToFirePerKg 0.03`.
- `RecoilPattern`: `PatternLength 20`, `RandomVerticalFraction 0.06`, `RandomHorizontalFraction 0.18`, `BaseRecoveryDegPerSecond 3`, `RecoveryPerAccumulatedDegree 0.6`.
- `AttachmentCatalog.cs`: eklenti ağırlığı (`WeightKg`), `AdsTimeMultiplier`, `ReloadMultiplier`, `VelocityMultiplier` ödünleşimleri.
- Hedef: MPT-76 gövdeye zırhsız <100 m'de 3 atış; tabanca 4–5; DMR 2–3; JNG-90 gövde 2, kafa 1.

## Bot taktiği — disiplinli asker (P1)

Botlar artık nişangâh değil asker gibi savaşır. Tüm sayılar Infrastructure/AI içinde (BotSkill, BotCombatRules, BotSquadTactics, BotTactics) saf yardımcılardır ve EditMode testlidir.

**Beceri kademeleri** (beceri 0..1 = zorluk tabanı Kolay 0.20 / Normal 0.50 / Zor 0.78 + rütbe en çok +0.15 + bireysel ±0.06): Acemi < 0.30, Muvazzaf < 0.60, Kıdemli < 0.85, Seçkin ≥ 0.85.

| Ayar | Değer | Not |
|---|---|---|
| Tepki süresi | 0.45 sn (acemi) → 0.20 sn (seçkin), sınır 0.18–0.45 | hedefi ilk gördükten sonra ateşe başlama |
| İlk atış hatası | başlangıç çarpanı 0.65 + 1.55 (acemi) / 0.7 (seçkin), τ 1.15 → 0.5 sn | hedef değişince yeniden açılır |
| Hedef değiştirme gecikmesi | 0.55 → 0.2 sn + açıya göre en çok 0.25 sn | ölü hedeften sonra ×0.6 |
| Geri tepme telafisi | 0.8 (acemi) → 0.3 (seçkin) × rastgele 0.8–1.3 × seri yorgunluğu (+%3.5/atış) × baskı | düşük = iyi |
| Baskı (suppression) | hasar +0.22, yakın geçen mermi en çok +0.14, 35 m içi düşman ateşi en çok +0.05/atış, sönüm 0.28/sn | kıdemli askerde etkisi ×0.6'ya kadar azalır |
| Baskı etkisi | nişan hatası ×(1 + 1.3 × baskı), seri uzunluğu ×0.55'e kadar, siperden çıkma olasılığı düşer | ağır baskıda (>0.6) ≤ 6 m siper için sürünerek gider |
| Siperden çık-ateş et-saklan | saklanma 0.6–1.7 sn (baskıyla ×2.4'e kadar), çıkış 1.3–2.6 sn (baskıyla yarıya kadar kısalır) | saklanırken şarjör doldurur; yüksek sipere köşe çıkışı (yaslanma), alçakta ayağa kalkma |
| Siper skoru | yürüme + tehdide yaklaşma ×1.5 + menzil sapması + ikinci tehdit açığı 14 + tim kopukluğu (>18 m) − yüksek siper 2.5 | baskıda yürüme mesafesi +%80 pahalı |
| Sıçramalı ilerleme | 4 sn evre; 20–75 m, can > %60, baskı < 0.4, en az 1 yakın dost | hareket eden ekip 9–14 m sıçrar, diğeri çömelip korur |
| Geri çekilme | düşman ≥ dost + 2 (ya da 2× üstünlük) ve can < %80 ya da baskı > 0.5; tek başına can < %35 | sis + "fall_back" çağrısı, iyileştirme varsa orada iyileşir |
| Baskıda kanat | ≥ 3.5 sn sıkışma, can ≥ %50, ≤ 2 düşman, 14–90 m; açı 45–75° | sis arkasında hat dışına çıkar |
| Bombayla çıkarma | olasılık 0.10 (bombacı 0.30) + 0.15×saldırganlık + gizli süre (2 sn sonrası +0.05/sn) + sıkışma (+0.04/sn) | dost patlama yarıçapındaysa atılmaz |
| Sis altında kaldırma | sis tehdit hattını keser, can ≥ %45, baskı ≤ 0.55, yaralı ≤ 28 m, ≤ 1 görünen düşman | hat açıksa önce sis atar (≤ 30 m) |
| İşitme | silah sesi: işitme mesafesi × şiddet × 0.65^engel (en az ×0.25); ayak sesi: depar 28, koşu 22, yürüyüş 12, çömelik 5, yatan 2 m × (işitme/85) | duyulan konum mesafe ve engelle belirsizleşir → araştırma |

Telsiz çağrıları (DialogueDirector): temas (yön+mesafe), flank, cover_me / covering, suppress, smoke, grenade_throw, fall_back, reviving, taking_fire. Bot başına 2.2 sn, tim başına 1 sn bekleme (temas 3 sn).

Bütçe: yeni iş yalnızca algı/karar zamanlayıcılarında (LOD ölçekli) çalışır; siper araması ≤ 14 ışın ve bot başına 2 sn'de bir; ayak sesi taraması 0.45 sn'de bir, ≤ 1 ışın; engel ışını silah sesi taramasında ≤ 2.
