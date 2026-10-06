# UI Tarama (kontrast / boyut / çakışma) — 2026-10-06

Ölçüt: metin ≥ 4,5:1 (WCAG AA), 1080p'de en az 14 px (HUD dünya etiketleri) / 16 px (panel metni), çakışma yok, Türkçe metin "..." ile kesilmiyor.
Denetim aracı: oyunda **F9** (UiQaOverlay.cs) — sarı = güvenli alan, camgöbeği = yazı, kırmızı = <16 px yazı, macenta = çakışan yazı, yeşil = görsel/Image.

## Bulgular ve durum
| # | Dosya:satır | Sorun | Durum |
|---|---|---|---|
| 1 | UiTheme.cs:125 TextMuted #7C816E | Panel üstünde 3,72:1, PanelLight üstünde 2,83:1 (74 kullanım) | DÜZELTİLDİ → #A2A790 (>=4,5 tüm panellerde) |
| 2 | UiKit.cs:37 TextMuted #70757E | 3,23:1 (Surface) | DÜZELTİLDİ → #9DA2AA |
| 3 | UiTheme.cs:173 FontTiny=14 | 1080p'de çok küçük (≈35 kullanım) | DÜZELTİLDİ → 16 (FontTiny-2 hâlâ 14: harita rozetleri) |
| 4 | AllyMarkersView.cs:63 (12), CompassView.cs:169,321 (11), HudVitalsView.cs:103,119 (12/11), MinimapView.cs:250 (11), PingWorldView.cs:45 (13), SquadPanelView.cs:113,123,125 (12) | <14 px | DÜZELTİLDİ → 14 |
| 5 | InventoryView.cs:398 (10 px slot adı) | okunmaz | DÜZELTİLDİ → 14 |
| 6 | DownedOverlayView.cs:124 (15 px ipucu) | küçük | DÜZELTİLDİ → 18 |
| 7 | UiTheme.Accent (#E30A17) metin olarak | koyu panelde 3,08:1 | SOLDA: yalnız şerit/dolgu için kullanın; yazıda AccentLight/EnemyRed. Metin kullanımı UiFactory.Label çağrılarında yok (shadow'lu düğme metni Text rengi) |
| 8 | UiFactory.Label (UiFactory.cs:212) | gölge/kontur yok; açık zeminde okunurluk riski | BIRAKILDI (UiFactory kapsam dışı) → ENTEGRASYON |
| 9 | LoadingScreenView.cs:115, OperationSetupPanel.cs:122 "..." | animasyonlu nokta / cümle sonu; kesme değil | SORUN DEĞİL |
| 10 | FullMapView.cs:445-466,1136-1322 (FontTiny-2 = 14) | alt sınırda, koyu harita üstünde gölgeli | KABUL (>=14) |
| 11 | SquadPanelView.cs:121 verticalOverflow=Truncate, Name 190 px | uzun Türkçe adlar kesilebilir | BIRAKILDI → ENTEGRASYON |
| 12 | FontTiny 16'ya çıkınca dar kutular (HudWeaponView.cs:120-166, HudStatusView.cs:84-87, ScoreboardView.cs:175, InventoryView.cs:615-618 resizeTextMaxSize) | taşma/çakışma olasılığı | F9 ile görsel doğrulama gerekli → ENTEGRASYON |

## Çakışma
Statik taramada kesin çakışan çapa bulunmadı; çalışma zamanı denetimi F9 katmanında (macenta). F9 Tutorial'daki "F9 = hepsini atla" (TutorialPresenter.cs:349) ve AaaBenchmarkVitrin.cs:126 ile aynı tuş.
