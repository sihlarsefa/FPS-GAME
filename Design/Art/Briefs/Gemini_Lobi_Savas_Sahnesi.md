# HAREKÂT — Gemini Lobi Savaş Sahnesi Brifi (AAA)

**Amaç:** Ana menü / savaş lobisi için fotogerçekçi, yüksek kaliteli **video döngüleri + hâlâ kareler + karakter kesimleri**.  
**Motor bağlama:** Unity URP · `StreamingAssets/Lobby/` · sol tarafta UI, sağ/orta 3B/video yüzey.  
**Ton:** Disiplinli Anadolu askerî operasyon lobisi — Hollywood trailer abartısı yok; PUBG lobi dioraması + modern CoD menü keskinliği.

---

## 0) Marka ve yasal (zorunlu)

| Kural | Detay |
|-------|--------|
| Oyun adı | **HAREKÂT** (Â zorunlu) |
| Kuvvetler | **Mavi kuvvet / Kırmızı kuvvet** (egzersiz senaryosu; gerçek birlik adı yok) |
| Yasak | Resmi TSK arması, gerçek birlik bayrağı, hilal-yıldızın **resmi** kopyası, gerçek yüz/ünlü, Nazi/aşırı sembol |
| İzinli | Stilize oyun amblemi; bordo bere; dijital kamuflaj (oyun ADD); mavi kol bandı (dost) |
| Silahlar | Türk yapımı / oyun içi: **MPT-76, MPT-55, PMT-76, JNG-90, KNT-76, SAR 9, MG3** vb. — “Airsoft / film prop / unloaded” görünümü |
| Dil | UI Türkçe; Gemini prompt’ları İngilizce (aşağıda hazır) |

**Renkler**

| Rol | Hex |
|-----|-----|
| Gece / clear | `#060912` … `#0b0b0b` |
| Vurgu kırmızı | `#E30A17` |
| Bordo bere | `#4A1C28` … `#6B1F2A` |
| Khaki / kum | `#C3B07A` … `#D2BF8A` |
| Dost mavi bant | `#3D9BFF` |
| Ateş | `#FF8F3D` → `#FF3B1A` |
| Kemik metin | `#F5F5F5` / `#ECEBE0` |

---

## 1) Sahne konsepti (tek cümle)

Dağlık Anadolu üssünde, mavi saat (blue hour) altında, **kamp ateşi + kum torbası siper + kamuflaj file + Kirpi MRAP** önünde, **10 kişilik timin brifing öncesi lobisi**: komutan önde MPT-76 low-ready, arka planda silahlar / teçhizat / araç; keskin, karanlık, sinematik ama sakin — “harekâta hazır mısın?” hissi.

**Referans duygusu:** PUBG menü dioraması (yakın, keskin) × Escape from Tarkov hideout ışığı × Türk dağ üssü gerçekçiliği.  
**Değil:** Neon cyberpunk, tropikal orman, çöl Hollywood, aşırı kan, patlama festivali.

---

## 2) Mekân — en ince detay

### 2.1 Zemin ve çevre
- Sıkıştırılmış **çamurlu toprak** + lastik izi; ateş çevresinde **kuru çamur halkası**
- Arka: **metal levha duvar** (paslı cıvata, kaynak izi) + sağda **beton bariyer** + solda **HESCO** tipi kum torbası satırı
- Üstte yatay **kamuflaj file** şeritleri (7 adet hissi; yırtık kenar, gölge)
- Ön-orta: **kum torbası duvarı** 5 sıra, köşelerde dökülmüş torba
- Prop’lar: tahta **sandıklar**, yeşil **variller**, **jerrycan**, çimento/çuval torbaları, mühimmat kutusu (kapalı), telsiz anten parçası
- Uzakta (flu): **Kirpi** (4×4 zırhlı) sol-arka, yaw ~250°; isteğe bağlı uzak **T-70** silueti (helipad ışığı, rotor bulanık)
- Sis: hafif mavi-gri yer sisi; toz partikülü az
- Gökyüzü: düz koyu lacivert-siyah, yıldız yok veya çok az; ay yok (UI okunaklı kalsın)

### 2.2 Işık
- **Key:** mavi saat güneş, yumuşak gölge, renk soğuk mavi-beyaz
- **Ateş:** sıcak turuncu nokta ışık (menzil kısa, yüzleri ısıtır)
- **Rim:** soğuk mavi kenar ışığı (siluet ayırır)
- **Spot:** komutan gövdesi, kum torbası hattı, Kirpi burnu
- **Post:** hafif vignette, düşük film grain, kontrollü bloom (ateş/ışık); aşırı HDR yok
- **FOG:** exponential, çok hafif; arka planı eritir, ön planı keskin bırakır

### 2.3 Kamera dilı (Unity shot’larıyla uyumlu)
Tüm videolar **aynı mekân / aynı ışık**; sadece kamera kayar.

| Shot ID | Amaç | Süre (öneri) | Döngü | FOV hissi | Bakış |
|---------|------|--------------|-------|-----------|--------|
| `lobby_main` | Ana menü idle | **12–16 sn** | Seamless loop | ~44° | Ateş + komutan göğüs-üst |
| `lobby_play` | OYNA paneli | **10–14 sn** | Loop | ~50° | Biraz geniş; ateş + siper |
| `lobby_team` | TİM | **10–14 sn** | Loop | ~38° | Tim üyeleri / omuz omza |
| `lobby_loadout` | DONANIM | **10–14 sn** | Loop | ~42° | Silah masası / sandık / low-ready |
| `lobby_career` | Kariyer | **8–12 sn** | Loop | ~32° | Komutan yüz/omuz yakın |
| `lobby_setup` | Harekât kurulum | **10–14 sn** | Loop | ~40° | Kirpi / araç vurgusu |
| `lobby_settings` | Ayarlar | **8–12 sn** | Loop | ~46° | Geniş üs, az hareket |

**Kamera hareketi (her shot):**
- Başlangıç ↔ bitiş **aynı kare** (seamless loop zorunlu)
- Hareket: ≤ **1–2 cm nefes** + çok yavaş dolly/pan (≤ 3° toplam)
- Shake yok; zoom pop yok; kesme yok
- Focal: ön plan keskin (f/2.8–4 hissi); arka plan hafif yumuşak ama okunur

---

## 3) Karakterler — tim (10 slot) detay

Hepsi aynı kamuflaj ailesi (oyun dijital kamuflajı), **gerçekçi oran**, kir / ter / yıpranma orta seviye.  
**Dost işareti:** sol üst kolda **mavi kumaş bant** (`#3D9BFF`).  
**Resmi nişan yok** — omuz pad boş veya stilize HAREKÂT.

| # | Rol | Görünüm | Silah | Pose / eylem (loop) |
|---|-----|---------|-------|---------------------|
| 1 | **Tim Komutanı** (kahraman) | Bordo bere `#4A1C28`, yelek dolu, sırt çantası, telsiz mik. | **MPT-76** low-ready, namlu aşağı ~20–25° | Nefes; göz kırpma; parmak tetik muhafazasında; ara sıra başı ateşe çevirir |
| 2 | Keskin Nişancı | Kask + örtü, uzun namlu | **JNG-90** | Diz çökmüş / tüfeği diz üstünde; dürbün kontrolü |
| 3 | Makineli | Ağır yelek, kemer | **PMT-76** veya **MG3** | Oturur / bipod hazır; kovan kemeri |
| 4 | Sıhhiyeci | Med çanta kırmızı haç **stilize** (küçük) | **MPT-55** veya tabanca | Çanta tokasını kontrol |
| 5 | Telsizci | Sırt telsiz, kulaklık | **MPT-55** | El telsizde; kısa el işareti |
| 6 | Bombacı | Granat torbası | **MPT-76** / shotgun | Torba ayarı |
| 7–10 | Piyade | Standart kask/yelek | **MPT-55 / MPT-76** | Ayakta / sandığa yaslanmış; idle nefes |

**Komutan yüz (ayrı teslim):**
- `commander_face` — omuz üstü portre, blue hour, bere, ciddi bakış, UI avatar için kare kırpılabilir
- `commander_body` — tam boy PNG kesim (şeffaf zemin), ~1.81 m oran, billboard için ön cephe veya ¾

**Yasak karakter:** gülümseyen influencer pozu, kadın/erkek “fashion military”, anime, aşırı kas, kanlı yüz.

---

## 4) Silah ve teçhizat masası (loadout shot)

Yakın plan masa / sandık üstü düzeni (film prop):
- **MPT-76** (ana), şarjör yanında, emniyet görünür
- **MPT-55**, **JNG-90**, **PMT-76** siluetleri rafta
- **SAR 9** / Canik tarzı tabanca kılıfta
- Optik, fener, susturucu (ayrı parçalar), eldiven, dizlik
- Harita (Kuzgun Vadisi hissi — dağ konturu, metin okunaksız/blur), pusula, el feneri
- Metal jerrycan, mühimmat kutusu damgalı ama **okunaksız** yazı

Metal: hafif yağ lekesi, tırtık; plastik mat; ahşap sandık aşınmış.

---

## 5) Araçlar

| Araç | Lobide | Detay |
|------|--------|-------|
| **Kirpi** | Sol-arka, zorunlu | 4×4 MRAP, mat yeşil-gri, çamur lastik, anten; motor sesi yok (görsel) |
| **T-70** | Uzak opsiyonel | Helipad ışığı, rotor blur; ana odağı çalmasın |

---

## 6) Video teknik şartname (Gemini / Veo uyumlu)

| Parametre | Değer |
|-----------|--------|
| Çözünürlük | **1920×1080** (tercih) veya 1280×720 min; 16:9 |
| FPS | **24 veya 30** (tutarlı tut) |
| Süre / clip | Shot tablosuna göre **8–16 sn**; loop için baş-son eşleşmeli |
| Tekrar | **Seamless loop** — son kare ≈ ilk kare; kesme yok |
| Ses | **Sessiz export** (oyun menü müziği ayrı: 72–84 BPM, ney/saz; savaş distant beden sesi kodda) |
| Codec hedef | H.264 mp4, yuv420, ~8–15 Mbps; Unity VideoPlayer uyumlu |
| Renk | Rec.709, biraz cool shadow / warm fire; LUT abartısı yok |
| Metin / logo | **Çerçevede yazı yok**, watermark yok, UI yok |

### Dosya adları (StreamingAssets/Lobby/)

```
lobby_main_loop.mp4          # 12–16s seamless
lobby_play_loop.mp4
lobby_team_loop.mp4
lobby_loadout_loop.mp4
lobby_career_loop.mp4
lobby_setup_loop.mp4
lobby_settings_loop.mp4
commander_turnaround.mp4     # 6–10s; komutan 360° veya omuz dönüş (eski clip yenile)
commander_gear.mp4           # 6–10s; yelek/dizlik/mavi bant kontrolü
commander_face.jpg           # 1024–2048 px kare veya 3:4
commander_body.png           # şeffaf zemin, tam boy, 2K yükseklik
lobby_hero_still_01.jpg      # ana poster karesi (market/Steam)
lobby_hero_still_02.jpg      # loadout masa
```

### Döngü kontrol listesi
- [ ] İlk ve son kare farkı gözle zor seçiliyor
- [ ] Ateş flicker’ı döngüde “zıplamıyor” (mümkünse ateşi yavaş / stabilize)
- [ ] Karakter ani teleport yok
- [ ] Kamera path kapalı eğri

---

## 7) Hâlâ / kesim teslimleri

1. **Hero still** — `lobby_main` ile aynı ışık, poster kalitesi  
2. **Komutan yüz** — UI profil  
3. **Komutan body PNG** — şeffaf, dikey  
4. **Silah hero** — MPT-76 ürün shot (siyah zemin veya lobi masası)  
5. **Tim wide still** — 4–6 asker + ateş (Team paneli)

---

## 8) Gemini’ye yapıştır — master prompt (İngilizce)

Aşağıyı olduğu gibi kullan; shot değiştirirken yalnızca **CAMERA / DURATION** satırını değiştir.

```text
Photoreal cinematic military staging area for a Turkish first-person squad battle-royale game titled HAREKAT (fictional Blue Force exercise — NOT real unit insignia).

SETTING: Anatolian mountain forward operating base at blue hour. Hard-packed muddy dirt ground with tire tracks, mud ring around a small campfire. Behind: rusty corrugated metal wall with bolts, concrete barriers right, HESCO sandbag line left. Overhead torn camouflage netting strips. Sandbag fighting position 5 rows deep. Props: wooden crates, green fuel barrels, metal jerrycan, sealed ammo boxes, canvas bags. Left-rear: Turkish Kirpi-style 4x4 MRAP, matte green-grey, muddy tires, antennas, 3/4 view. Optional distant blurred helicopter silhouette with soft pad lights — must not steal focus.

HERO: Squad commander center-front, burgundy beret (#4A1C28), digital camouflage uniform, plate carrier with pouches, backpack radio handset, blue cloth armband on left upper arm (friendly ID). Holds MPT-76 battle rifle low-ready, muzzle down ~25 degrees, finger outside trigger guard. Serious adult male face, stubble, tired focused eyes, no smile. Subtle breathing and blink only.

SUPPORT (background, readable but secondary): sniper with JNG-90 kneeling, machine gunner with belt-fed MG, medic with compact med bag, radio operator with backpack radio, 2–3 riflemen with MPT-55/MPT-76. Same camo family, blue armbands, no official military emblems, no readable text on patches.

LIGHTING: cool blue-hour key light, warm campfire key on faces, cool rim light separating silhouettes, soft volumetric haze, controlled bloom on fire only, mild film grain, gentle vignette. Sharp foreground, slightly softer background. Dark clear color ~#060912. No neon, no lens flare spam, no blood, no explosions.

CAMERA: locked-off cinematic move with tiny breathing (±1cm), FOV ~44mm-equivalent, eye level ~1.6m, looking toward commander torso and campfire. Start frame MUST match end frame for seamless loop.

STYLE: PUBG-style close diorama sharpness + modern military realism. Photoreal, 4K detail, natural materials (wet mud, fabric weave, metal wear). No UI, no logos, no subtitles, no watermark.

NEGATIVE: official Turkish Armed Forces insignia, real unit flags, celebrity faces, cartoon, anime, cyberpunk neon, tropical jungle, desert Hollywood, excessive gore, smiling influencer pose, shaky cam, jump cuts, text overlays.
```

### Shot varyasyon ekleri (master’ın altına ekle)

**lobby_main (12–16s loop)**  
`CAMERA: FOV ~44°, look at commander chest + fire. Duration 14 seconds, seamless loop.`

**lobby_play (10–14s)**  
`CAMERA: pull slightly wider FOV ~50°, include more sandbags and fire. Duration 12s seamless loop.`

**lobby_team (10–14s)**  
`CAMERA: FOV ~38°, frame 3–4 squad members shoulder-to-shoulder near fire; commander still readable. Duration 12s seamless loop.`

**lobby_loadout (10–14s)**  
`CAMERA: FOV ~42°, slow push toward weapons laid on crates: MPT-76, magazines, gloves, map (unreadable text). Soldiers soft background. Duration 12s seamless loop.`

**lobby_career (8–12s)**  
`CAMERA: FOV ~32° portrait of commander face and beret, fire bokeh behind. Duration 10s seamless loop.`

**lobby_setup (10–14s)**  
`CAMERA: FOV ~40°, favor Kirpi MRAP 3/4 with commander mid-ground. Duration 12s seamless loop.`

**commander_turnaround (6–10s)**  
`Same lighting/set. Commander slowly turns 90–180° showing gear, MPT-76, blue armband, return to start pose for loop OR end on clean 3/4 for cut. Duration 8s.`

**commander_gear (6–10s)**  
`Close medium shot: hands adjust plate carrier straps, knee pads, blue armband; rifle slung or low-ready. Duration 8s seamless-friendly.`

**Still — commander_face**  
`Ultra detailed portrait photo, burgundy beret, blue hour rim, serious expression, square crop friendly, no text.`

**Still — commander_body PNG**  
`Full-body cutout of same commander, transparent background, standing, MPT-76 low-ready, studio-neutral edge lighting matching blue hour, game billboard ready.`

---

## 9) Kalite kabul (Gemini çıktısı)

| Ölçüt | Geçiş |
|-------|--------|
| Loop | Baş-son farkı < fark edilir kesme |
| Marka | Resmi arma yok; mavi bant + bordo bere var |
| Silah | MPT-76 okunur siluet; “oyuncak” plastik görünüm yok |
| Lobı okunurluğu | Sol %35 koyu kalabilir (UI için); sağ/orta aksiyon |
| Hareket | Abartılı koşu/atış yok — **idle brifing** |
| Teknik | 1080p, sessiz mp4, 8–16 sn |
| Tutarlılık | Tüm shot’larda aynı kostüm / aynı ateş / aynı Kirpi |

---

## 10) Unity entegrasyon notu (Cursor tarafı)

| Asset | Hedef |
|-------|--------|
| `lobby_*_loop.mp4` | Sayfa shot’una göre VideoPlayer / RawImage veya skybox değil — **sağ panel / diorama kartı** |
| `commander_face.jpg` | `MainMenuController` profil |
| `commander_body.png` | `LobbyCommanderStand` billboard |
| Süre | Player `isLooping=true`; clip süresi shot tablosuyla uyumlu |
| Ses | Videoda sessiz; `DistantBattle` + menü müziği kodda |

Mevcut diorama kodu: `MenuBackdrop` + `MenuDioramaBuilder` (ateş, Kirpi, tek komutan). Gemini videoları **bu sahneyi zenginleştirir / yerine geçer**; ışık ve kompozisyon bu brifle hizalı kalsın.

---

## 11) Üretim sırası (Gemini’ye talimat)

1. Önce **hero still** + `commander_face` (tutarlı yüz kilidi)  
2. Aynı seed/yüz ile `lobby_main_loop`  
3. Aynı set’ten diğer shot loop’lar  
4. `commander_turnaround` + `commander_gear`  
5. `commander_body.png` kesim  
6. Loop QA: baş-son kare yan yana

**Toplam hedef paket:** 7 lobi loop + 2 komutan clip + 2 hâlâ + 1 body PNG ≈ bir “savaş lobisi seti”.
