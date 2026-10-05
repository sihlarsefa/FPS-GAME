# HAREKÂT — Ajan Görev Planı

**Oyun:** Türk askeri sistemine dayalı, FPP (birinci şahıs) **Tim Battle Royale**.
- 10 kişilik timler T-70 helikopteri ya da Kirpi zırhlı araçla harekât bölgesine intikal eder.
- Harekât alanı daralır, son ayakta kalan tim kazanır.
- Komuta zinciri TSK rütbelerine göre işler.
- Silahlar Türk yapımıdır, harita dağlık "Kuzgun Vadisi"dir.

**Çalışma düzeni:**
- Claude tarafında 25 ajan **iki hatta** çalışır: Hat A ve Hat B, her biri 8 paralel, yani **aynı anda 16 ajan**. Biri bitince kuyruktaki sıradaki başlar.
- Paralelde **Cursor (Opus)** backend'i, **ChatGPT/Codex** web portalını yazar. Her biri yalnızca kendi klasörüne dokunur.
- Her Claude ajanı yalnızca kendi dosyalarına dokunur ve kodunu Unity 6000.6 DLL'lerine karşı derleyerek doğrular.
- Ortak teknik sözleşme: [Docs/CONTRACTS.md](Docs/CONTRACTS.md)

## Hat A — 13 ajan
| Ajan | Alan | Yapacağı iş |
|------|------|-------------|
| `app-weapons` | Silah ve hasar kuralları | 10 Türk silahının balistik değerleri, şarjör/ateş modu/sekme, zırh ve vücut bölgesi hasarı, dost ateşi, testler |
| `app-items` | Envanter ve teçhizat | Silah yuvaları, yelek/kask/çanta, ağırlık, tıbbi malzeme, görev bazlı başlangıç teçhizatı, testler |
| `app-match` | Maç akışı | Tim bazlı maç durumu, kazanan tim, sıralamalar, daralan harekât alanı, istatistik, öldürme akışı, testler |
| `app-sim` | Simülasyon ve komuta | TSK rütbeleri, komuta zinciri, tim emirleri, topçu desteği, intikal planı, bot kararları, ayarlar/kariyer |
| `infra-rendering` | Görsel altyapı | Malzemeler, dijital kamuflaj ve Türk bayrağı dokuları, kamera düzeneği, post-processing |
| `infra-audio` | Ses | Silah, helikopter rotoru, Kirpi motoru, topçu ıslığı, telsiz, ortam ve menü müziği |
| `infra-vfx` | Efektler | Namlu alevi, mermi izi, isabet, kan, mermi deliği, patlama, sis, toz |
| `infra-combat` | Çatışma | Savaşan bileşeni, vuruş kutuları, balistik, patlama, el bombası/sis, yumruk, bölge hasarı, topçu atışları |
| `infra-player` | Oyuncu hareketi | Yürüme/koşma/eğilme/yüzüstü/yana eğilme, düşme hasarı, FPP kamera, sekme, kamera sarsıntısı |
| `infra-weapon-visuals` | Silah modelleri | MPT-76, JNG-90 ve diğer silahların modelleri; FPP eller ve animasyonlar |
| `infra-loot` | Yerdeki eşyalar | Eşya modelleri, alma ve bırakma, ölenlerin eşyalarını düşürme |
| `pres-map-inventory` | Harita ve envanter | Mini harita, tam harita (bölge, timler, iniş noktası, topçu işareti), envanter ekranı |
| `pres-menus` | Menüler | Ana menü (bayrak, askerler, Kirpi dekoru), harekât kurulumu, kariyer ve rütbe ekranı, ayarlar, duraklatma, maç sonu |

## Hat B — 12 ajan
| Ajan | Alan | Yapacağı iş |
|------|------|-------------|
| `pres-ui-kit` | Arayüz kiti | Ortak UI bileşenleri (buton, slider, panel, tema, sprite'lar) |
| `infra-world-terrain` | Arazi | Kuzgun Vadisi: sırtlar, vadi, dere, yollar, ağaçlar, mini harita dokusu, NavMesh |
| `infra-world-buildings` | Binalar | Köy evi, cami, karakol, kule, hangar, sığınak, baraj binası; kapılar, merdivenler |
| `infra-ai` | Yapay zekâ | Tim yapay zekâsı: kama düzeni, emirler, algı, çatışma, ilerleme, iyileşme, bölgeye hareket |
| `infra-characters` | Asker modelleri | Dijital kamuflajlı askerler, tim kolluğu, animasyonlar, eğitim hedefleri |
| `infra-transport` | İntikal araçları | T-70 helikopteri ve Kirpi; koltuklar ve araçtan iniş |
| `pres-player` | Oyuncu kontrolü | Silah kullanımı, etkileşim, iyileşme, bomba, tim emirleri (F1–F4), topçu (V), araçla intikal |
| `pres-bootstrap` | Oyun akışı | Servis kurulumu, timler, intikal, maç döngüsü, sahne geçişleri, atış poligonu |
| `pres-hud` | Oyun ekranı | Can, cephane, pusula, nişangâh, hasar yönü, öldürme akışı, tim paneli, dost işaretleri, dürbün görünümü |
| `editor-setup` | Unity kurulumu | Katmanlar, URP, kalite ayarları, varlıklar, 3 sahne, NavMesh, build (macOS ve Linux sunucu) |
| `infra-world-locations` | Lokasyonlar | Köyler, karakol, ileri üs (FOB), taş ocağı, baraj, röle tepesi, ağıl, harabe, atış poligonu |
| `infra-vehicle-drive` | Sürülebilir araç | Haritada sürülebilir Kirpi |

## Paralel dış ajanlar (her biri 6 bağımsız uzun görev + uzatma hedefleri)
| Araç | Görev dosyası | Görevler ve klasörler |
|------|---------------|-----------------------|
| Cursor (Opus) | [Docs/CURSOR_GOREVI.md](Docs/CURSOR_GOREVI.md) | 1) Backend API `Backend/`<br>2) Sunucu filosu ve dağıtım `Deploy/`<br>3) Telemetri ve hile tespiti `Backend/Harekat.Telemetry/`<br>4) Yük testi `Tools/LoadTest/`<br>5) CI/CD `.github/`<br>6) Discord botu `Tools/DiscordBot/` |
| ChatGPT/Codex | [Docs/CODEX_GOREVI.md](Docs/CODEX_GOREVI.md) | 1) Web portalı `Web/`<br>2) GDD `Design/GDD/`<br>3) Denge hesaplayıcı `Tools/BalanceCalc/`<br>4) Harita paftaları `Design/Maps/`<br>5) Yerelleştirme `Localization/`<br>6) Mağaza sayfası ve basın kiti `Marketing/` |

Her görevin ana kısmı bitince "UZATMA HEDEFLERİ" listesine geçilir.
Görev başlatma cümlesi: *"Docs/CURSOR_GOREVI.md içindeki GÖREV X'i baştan sona uygula."* (Codex için `CODEX_GOREVI.md`)

## Sonraki aşamalar (iki hat bitince)
1. **Entegrasyon:** Tüm Unity projesi hatasız derlenene kadar modüller arası uyumsuzluklar düzeltilir.
2. **İnceleme:** Ayrı inceleme ajanları oyun akışını, çatışmayı, yapay zekâyı, dünya ve editör kurulumunu, arayüzü inceleyip hataları düzeltir.
3. **Son doğrulama:** Son derleme ve testler.
4. **Online:** Unity istemcisi Netcode/Transport adaptörüyle backend'e bağlanır, ardından dedicated server build'i alınır.
