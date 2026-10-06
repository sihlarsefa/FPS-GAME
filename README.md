# HAREKÂT

Türk askeri sistemine dayalı, birinci şahıs (FPP) **tim battle royale** oyunu. Unity 6000.6 (URP 17.6, yeni Input System), sunucu tarafı **Windows Server + MSSQL**, web **düz HTML/CSS/JS**. Linux hedeflenmez.

> Kurgu: Mavi/Kırmızı kuvvetler arasında bir "harekât tatbikatı". Gerçek dünyadaki hiçbir örgüt adlandırılmaz. Arayüz dili Türkçedir.

## Oyun nedir?

- **10 kişilik timler.** Oyuncu Tim Komutanı olarak başlar, 9 yapay zekâ astı komuta eder; rakip timler de 10 kişidir.
- **TSK komuta zinciri.** Er'den Binbaşı'ya rütbeler; komutan düşünce komuta rütbe sırasındaki bir sonraki askere geçer, tim savaşa devam eder. Rütbe, kariyer XP'si ile yükselir.
- **İntikal.** Her tim harekât bölgesine **T-70 helikopteri** ya da **Kirpi zırhlı aracı** ile gelir. Haritada sürülebilir Kirpi de bulunur.
- **Türk yapımı silahlar:** SAR 9, Canik TP9, SAR 109T, MPT-55, MPT-76, G3A7, KNT-76, JNG-90 Bora-12, PMT-76, Escort.
- **Harita: Kuzgun Vadisi.** 1024 x 1024 m dağlık arazi; köyler, karakol, FOB, baraj, taş ocağı, röle tepesi.
- **Daralan harekât alanı.** Alan dışı hasar verir; son ayakta kalan tim kazanır.
- **Topçu desteği.** Telsizci ve komutan işaretli noktaya topçu atışı çağırabilir.
- **Modlar:** Harekât (battle royale) ve Atış Poligonu (eğitim). Online (Netcode + dedicated server) altyapısı hazırdır.

Ayrıntılı oyuncu kılavuzu: [Docs/OYNANIS_REHBERI.md](Docs/OYNANIS_REHBERI.md).

## Depo haritası

| Klasör | İçerik |
|--------|--------|
| `Assets/_Project/Scripts/Core` | Saf C# alan modeli, olaylar, arayüzler (Unity yok) |
| `Assets/_Project/Scripts/Application` | Saf C# kurallar ve servisler (silah, hasar, maç, bölge, rütbe, bot kararı) |
| `Assets/_Project/Scripts/Infrastructure` | Unity uygulamaları: savaş, yapay zekâ, dünya üretimi, ses, görsel, araçlar, DI, içerik eşleme |
| `Assets/_Project/Scripts/Presentation` | Oyun akışı, oyuncu, HUD, menüler, harita/envanter, geliştirici konsolu |
| `Assets/_Project/Scripts/Online` | Backend istemcisi, profil, eşleştirme arayüzleri; `Netcode/` alt derlemesi |
| `Assets/_Project/Scripts/Platform` | Steam entegrasyonu |
| `Assets/_Project/Scripts/Editor` | Kurulum menüsü, sahne üretimi, build araçları, varlık eşleyici |
| `Assets/_Project/Tests` | EditMode ve PlayMode testleri |
| `Backend/` | ASP.NET Core API, MSSQL, telemetri, ServerManager (oyun sunucusu yöneticisi) |
| `Deploy/windows` | IIS, SQL, güvenlik duvarı ve dağıtım betikleri (Windows Server) |
| `Web/` | Oyuncu portalı (düz HTML/CSS/JS) |
| `Wiki/` | Oyuncu kılavuzu sitesi |
| `Tools/` | UnityVerify, BalanceCalc, LocTool, LoadTest, Installer, Launcher, SqlReports, DiscordBot, AgentWorkflows |
| `Design/` | GDD, harita, UI, sanat, ses, varlık CSV'leri |
| `QA/` | Test planı, 368 senaryo, playtest protokolü |
| `Docs/` | Sözleşmeler ([CONTRACTS.md](Docs/CONTRACTS.md)), durum ([DURUM.md](Docs/DURUM.md)), faz görevleri |
| `Localization/`, `Marketing/` | Çeviri hattı, canlı operasyon planı |

## Unity'de açma

1. Unity Hub ile **Unity 6.6 (6000.6.x)** kurun. Modüller: **Windows Build Support** ve **Windows Dedicated Server Build Support**.
2. Hub'da **Projects > Add** ile bu klasörü ekleyin ve açın (ilk açılışta paketler çözülür).
3. Menüden **HAREKÂT > Kurulum > 1) Her Şeyi Kur** komutunu çalıştırın (katmanlar, Player Settings, URP, sanat kütüphanesi, 3 sahne, NavMesh).
4. `MainMenu` sahnesini açıp **Play**'e basın. Sahneler: `MainMenu`, `KuzgunVadisi`, `TrainingRange`.

Hazır sanat varlıkları isteğe bağlıdır: **HAREKÂT > İçerik > Varlık Eşleyici**. Eşleme yoksa her şey prosedürel üretilir.

## Kontroller

Tam tablo (bağlamlar, araç, izleyici, katman/Esc kuralları): [Docs/KONTROLLER.md](Docs/KONTROLLER.md).

| Tuş | İşlev |
|-----|-------|
| W A S D / Fare | Hareket / bakış |
| Shift / Space | Koşu / zıpla |
| Ctrl (basılı) veya C | Çömel |
| Z | Yüzüstü |
| Q / E (basılı) | Yana eğil (ölünce: izleyici hedef değiştir) |
| Sol tık / Sağ tık | Ateş / nişan |
| R | Şarjör değiştir (Kirpi taretinde mermi) |
| F | Etkileşim: eşya al, araca bin / in; yaralı müttefikin yanında basılı = kaldır |
| 1-4, fare tekeri | Silah seç (Kirpi'de 1/2 = sürücü/nişancı koltuğu) |
| B | Ateş modu |
| H / J | İyileş / takviye |
| G / T | Parçalı bomba / sis bombası |
| X | Silahı indir |
| Tab veya I | Envanter |
| M | Tam harita (sağ tık: işaret) |
| Esc | En üstteki katmanı kapat / duraklat |
| F1 / F2 / F3 / F4 | Tim emri: Takip / Mevzi / Taarruz / Toplan |
| V / U | Topçu atışı / İHA keşfi (Telsizci, Komutan) |
| Fare orta tuş / Y | Dokun: ping; basılı: komut çarkı |
| CapsLock (basılı) | Skor tablosu |
| N / F9 | Eğitim: adımı / tümünü atla |
| ` (backquote) / F10 | Geliştirici konsolu / performans göstergesi |

Kaynak: `Application/Services/ControlScheme.cs`, `Infrastructure/Input/UnityInputReader.cs`, `Presentation/Player/SquadCommandInput.cs`.

## Build

Menü **HAREKÂT > Build**:
- **Windows İstemci (x64)**
- **Windows Dedicated Server**
- **macOS**

Windows kurulum paketi: `Tools/Installer/build.ps1` (Inno Setup). Sunucu kurulumu: [Deploy/windows/README.md](Deploy/windows/README.md).

## Doğrulama (Unity açılamayan makinede)

`Tools/UnityVerify` Unity DLL'lerine karşı `csc` ile derler ve EditMode testlerini koşturur:

```sh
zsh Tools/UnityVerify/setup_deps.sh                       # bir kez
zsh Tools/UnityVerify/verify.sh Tools/UnityVerify/out_ben --player --tests
```

Çıktı: her derleme için `== <Assembly>: N errors`. `--player` UnityEditor'suz çalışma zamanı derlemesini, `--tests` testleri çalıştırmayı ekler.

## Durum ve bilinen sınırlar

- 25/25 Unity modülü yazıldı; derleme ve EditMode testleri `verify.sh` ile 0 hata (399 test).
- **Gerçek Unity editöründe henüz tam doğrulama yapılmadı** (kurulum yenileniyor); ilk gerçek oynatma testi bekliyor. Sahne üretimi, NavMesh ve PlayMode testleri o zaman sınanacak.
- Online (Netcode, dedicated server, backend) kodu hazır; canlı eşleştirme uçtan uca denenmedi.
- Görseller prosedürel low-poly; hazır varlık hattı ([Design/Art/ENVIRONMENT.md](Design/Art/ENVIRONMENT.md)) sonraki adım.
- Anlık durum: [Docs/DURUM.md](Docs/DURUM.md), plan: [AJAN_PLANI.md](AJAN_PLANI.md).
