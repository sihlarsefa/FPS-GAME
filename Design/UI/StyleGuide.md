# HAREKÂT — Arayüz Stil Rehberi

Unity HUD / menü ajanları için görsel referans. Kaynak doğruluk: `Assets/.../UiTheme.cs`, `Design/GDD/20-ui-ux-akislari.md`, `Marketing/brand/BRAND.md`.

Mockup’lar: `Design/UI/screens.html` · CSS token’lar: `css/tokens.css`.

## 1. Renk tokenları

| Token | Hex / RGBA | Kullanım |
|-------|------------|----------|
| Background | `#12150E` | Tam ekran menü zemini (koyu zeytin) |
| PanelDark | `rgba(22,26,17,0.90)` | HUD kutuları |
| Panel | `rgba(35,41,27,0.92)` | Standart panel |
| PanelLight | `rgba(52,61,40,0.94)` | Liste satırı / iç kutu |
| PanelBorder | `#5A6345` | Kenar / ayraç |
| **Accent (Türk kırmızısı)** | **`#E30A17`** | CTA, vurgu şeridi, birincil düğme |
| AccentDark | `#9E0710` | Basılı vurgu |
| AccentLight | `#FF3B47` | Hover vurgu |
| Amber / Kehribar | `#F2A900` | Uyarı, rütbe, seçili, başlık |
| Khaki | `#C3B07A` | İkincil vurgu |
| Ally / Dost mavisi | `#3D9BFF` | Tim, dost işaret |
| Enemy / Düşman kırmızısı | `#FF4A3D` | Düşman (accent’ten ayırt edilir) |
| Success | `#5FC84E` | İyileşme / dost kill feed |
| Armor | `#8EB8D8` | Zırh çubuğu |
| Boost | `#F2C12E` | Takviye çubuğu |
| Zone | `rgba(46,123,255,0.70)` | Harekât alanı halkası |
| Text | `#ECEBE0` | Ana metin |
| TextDim | `#B4B6A4` | İkincil |
| TextMuted | `#7C816E` | İpucu / pasif |

**Kural:** Dost/düşman yalnızca renkle değil; şekil + etiket (Mavi/Kırmızı kuvvet, ikon silueti). Accent `#E30A17` UI markasıdır; düşman rengi `#FF4A3D` ile karıştırılmaz.

## 2. Tipografi

Referans çözünürlük: **1920×1080**. CanvasScaler ile ölçeklenir; QHD’de `frame--qhd` token’ları ~%28 büyütür.

| Basamak | px (FHD) | Kullanım |
|---------|----------|----------|
| Tiny | 14 | Dipnot, harita etiketi |
| Small | 18 | İpucu, kill feed, ayar satırı |
| Normal | 22 | Gövde |
| Medium | 26 | Düğme / vurgulu satır |
| Large | 34 | Bölüm başlığı |
| Title | 52 | Ekran başlığı |
| Huge | 88 | Logo, “KAZANAN TİM” |
| HudNumber | 44 | Cephane sayacı |

Yazı tipi: sistem sans (Unity’de LegacyRuntime / Arial yedek). Ağır başlıklar `letter-spacing: 0.06–0.12em`, çoğu UI metni **Türkçe**.

## 3. Boşluk ölçeği

| Adım | px |
|------|-----|
| space-1 | 4 |
| space-2 | 8 (= UiTheme.Spacing) |
| space-3 | 12 |
| space-4 | 16 (= Padding) |
| space-5 | 24 |
| space-6 | 32 |
| space-7 | 48 |

- Düğme: 300×56 (FHD), köşe yarıçapı **6px**
- Vurgu şeridi: **4px** sol kenar (`#E30A17`)
- Satır yüksekliği (ayar): 40px

## 4. İkon dili

- Format: **SVG**, 64×64 viewBox, düz dolgu / ince stroke.
- Palet: metin/khaki/amber/ally/enemy/accent — gradyan yok.
- Kategoriler: `icons/weapons/`, `items/`, `orders/`, `transport/`, `ranks/`
- Tim emirleri: **Takip, Mevzi, Taarruz, Toplan** + Topçu.
- İntikal: T-70 helikopter, Kirpi.
- Rütbe nişanları: **stilize geometri**; resmi armaların kopyası değil (GDD §3 / brand kuralı).

Galeri: [`icons/index.html`](icons/index.html).

## 5. Animasyon süreleri

| Ad | Süre | Not |
|----|------|-----|
| Fast | 120 ms | Hover / basış |
| Fade (UiTheme) | **180 ms** | Panel aç/kapa, solma |
| Medium | 280 ms | Isabet işareti |
| Damage dir | 650 ms | Hasar yön halkası |
| Command banner | 2.4 s | Komuta devri bildirimi |
| Ease | `cubic-bezier(0.22, 0.61, 0.36, 1)` | |

`prefers-reduced-motion: reduce` → animasyonlar kapatılır (prototip: `prototypes/hud-events.html`).

## 6. Erişilebilirlik / kontrast

Hedef: WCAG AA benzeri okunurluk (oyun HUD’unda tam AA her pikselde zorunlu değil; metin panellerinde hedefle).

| Çift | Oran (yaklaşık) | Sonuç |
|------|-----------------|--------|
| `#ECEBE0` on `#12150E` | ~12:1 | Geçer |
| `#ECEBE0` on `#23291B` | ~10:1 | Geçer |
| `#E30A17` on `#12150E` | ~5.2:1 | Büyük metin / CTA OK; küçük gövde metninde kullanma |
| `#F2A900` on `#12150E` | ~8.5:1 | Geçer |
| `#3D9BFF` on `#12150E` | ~6.5:1 | Geçer |
| `#7C816E` on `#12150E` | ~3.8:1 | Yalnızca ikincil ipucu |

- Odak: klavye ile düğme sırası; ölüm ekranında birincil CTA odak.
- Zone: renk + süre metni + ses (GDD 20.7).
- Emir tekerleği: fare + sayı tuşları.
- Renk körlüğü: şekil kodu zorunlu — bkz. [`ColorBlindness.md`](ColorBlindness.md).

## 7. Ekran envanteri

| Ekran | Dosya |
|-------|--------|
| Ana menü | `screens/main-menu.html` |
| Harekât kurulumu | `screens/match-setup.html` |
| Kariyer / rütbe | `screens/career.html` |
| Ayarlar | `screens/settings.html` |
| HUD | `screens/hud.html` |
| Tam harita | `screens/full-map.html` |
| Envanter | `screens/inventory.html` |
| Dürbün | `screens/scope.html` |
| Ölüm | `screens/death.html` |
| Zafer | `screens/victory.html` |
| Yükleme | `screens/loading.html` |

Ölçekler: **1920×1080** (varsayılan), **2560×1440** (`?scale=qhd` veya `.frame--qhd`). Galeri: [`screens.html`](screens.html).

## 8. Unity eşleme notu

| Mockup | Unity hedefi (Claude) |
|--------|------------------------|
| tokens / StyleGuide | `UiTheme.cs` |
| HUD yerleşimi | `Hud*View`, `CompassView`, `KillFeedView`, `AllyMarkersView` |
| Menüler | `MainMenuBootstrap`, menü sunumu |
| İkon SVG | Procedural / sprite atlas üretimine referans |
