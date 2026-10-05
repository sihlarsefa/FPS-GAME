# Ortam Sesleri — Harita Bölgeleri

Atmosfer kanalı ayrı ses ayarı (`Ambience` / `Wind`). Kritik olaylar (intikal, alan daralması, emir) her zaman metin/ikonla da duyurulur.  
Kurgu: harekât tatbikatı; gerçek örgüt adı yok.

---

## 1. Kuzgun Vadisi (gündüz + gece)

Kaynak yerleşim: GDD 09 / `MapLayout.CreateKuzgunVadisi`. Gece varyantı: `Design/Maps/v2/KuzgunVadisiGece/`.

### Bölge tablosu

| Bölge | Gündüz katmanlar | Gece farkı | Maskeleme / taktik | Önerilen SoundId |
|-------|------------------|------------|--------------------|------------------|
| **Köy** (Kuzgun / Yamaç) | Köpek uzak havlama (seyrek), rüzgâr sokak koridoru, ahşap kapı gıcırtısı, uzak horoz **yok** (tatbikat sessizliği), kuş sürüsü sabah kısa | Sessiz zemin; kapı/ahşap net; köpek daha seyrek | CQB’de ayak metal/ahşap ifşa | Ambience, Wind, Footstep(ahşap) |
| **Orman** (Çam Sırtı) | Yaprak sürtünme, böcek düşük, dal kırılma one-shot | Rüzgâr yaprak baskın; koşu uzak taşır | Sprint cezası yüksek | Wind, Ambience, Footstep(toprak) |
| **Baraj** / dere | Su akış loop, köprü tahta/metal ayak, rüzgâr su yüzeyi | Su daha baskın → ayak maskelemesi artar | Su maskesi ile yaklaşım | Ambience(su), Footstep(metal/tahta) |
| **Taş ocağı** | Açık kazı rüzgâr, taş düşme seyrek, makine uğultusu **kapalı** (terk edilmiş) | Rüzgâr + yankı taş duvar | Uzun görüş; ayak taş net | Wind, Ambience, BulletImpact |
| **FOB / karakol** | Jeneratör sabit uğultu, bayrak ip, uzak araç | Jeneratör maske; yaklaşınca ani kesilme uyarı | Maske kesilince tehdit | Ambience, VehicleEngine uzak |
| **Açık tarla / ana yol** | Rüzgâr düz, DistantBattle düşük | Ayak + ekipman metal; fısıltı şart | En az maske | Wind, Footstep, DistantBattle |
| **Röle tepesi / gözetleme** | Tepe rüzgârı güçlü, anten uğultu hafif | Rüzgâr VO’yu kısmen örter (duck hafif) | KN pozisyonu | Wind |
| **Köprüler** | Su + tahta/metal | Gece daha okunaklı | Baskı noktası | Footstep, Ambience(su) |
| **Harabe / ağıl** | Metal sac, rüzgâr delik, düşük fauna | Daha sessiz | Sessiz yağma | Ambience, Wind |

### Kuzgun kuralları

- **Ezan / dini çağrı sesi yok** (tasarım kararı; köy ambienste yer almaz).  
- Gece: ambiyans −4…−8 dB gündüze göre; one-shot aralıkları uzar.  
- DistantBattle: maç ortası +2 dB; zone daralınca +1 dB daha.

---

## 2. Ayaz Geçidi

Kaynak: `Design/Maps/v2/AyazGecidi/SesOrtami.md`.

| Bölge | Katman | Not | SoundId |
|-------|--------|-----|---------|
| Üst sırt | Rüzgâr yönlü, seyrek | VO’yu maskelemeyecek dinamik aralık | Wind |
| Kayak evi | Ahşap gıcırtı | İç/dış geçişte low-pass | Ambience, Footstep(ahşap) |
| Radar üssü | Mekanik düşük uğultu | Yapı dışında hızla söner | Ambience |
| Kar yüzeyi | Ayak kuru/sert kar | Sprint/crouch ayrı clip | Footstep |
| Geçit | Kısa kaya yankısı | Uzaktan atış yönü korunur | Shot* + reverb preset |
| Buz göleti | Su yalnızca kıyı | Güvenli buz üstü yürüyüşü yok | Ambience(su) |

### Teknik

- Kar ayak: yüzey tag; 2 örnek set (kuru / sert).  
- Rüzgâr: 3D emitter sırt hattı; LOD uzak kes.  
- Radar: loop + distance attenuation; iç mekân low-pass.

---

## 3. Mavi Liman

Kaynak: `Design/Maps/v2/MaviLiman/SesOrtami.md`.

| Bölge | Katman | Not | SoundId |
|-------|--------|-----|---------|
| Liman | Halat / gövde çarpma | Aralıklı one-shot 8–20 sn; sürekli metal yok | Ambience |
| Deniz feneri | Rüzgâr + taş | Tepe rüzgârı; taş iç kısa yankı | Wind, Ambience |
| Zeytinlik | Yaprak sürtünme | Az yoğunlukta böcek | Ambience, Wind |
| Kıyı kasabası | Sokak yansıması | Dar sokak early reflection; ev içi geçiş net | Ambience, Footstep |
| Sahil | Dalga | Kıyıya yaklaştıkça artar; ~60 m sonra azalır | Ambience(deniz) |
| Sahil karakolu | Jeneratör | Sakin uğultu; iki giriş eşit | Ambience |
| Dere / kanal | Su | Köprüde ayak + su | Ambience, Footstep |

### Teknik

- Deniz: göl diski sınırında tek ambient bus; stereo pan harita X.  
- Liman: loop’tan kaçın (adım maskeleme).  
- Sokak: kısa ER; açık sahilde kapat.

---

## Ortak uygulama

| Parametre | Değer |
|-----------|-------|
| Ambiyans bus | Öncelik 3; Master altında ayrı slider |
| Wind vs Ambience | Wind yönlü 3D; Ambience bed/2D+yerel emitter |
| İç mekân | Ambiyans high-cut ~2–3 kHz; dış Wind kıs |
| Zone içinde | Ambiyans −3 dB; ZoneWarning öne |
| İzleyici modu | Ambiyans + DistantBattle öne; HitMarker yok |
