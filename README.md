# FPS Battle Royale — Proje Teslimi

PUBG tarzı **FPP (First Person)** battle royale oyunu için SOLID prensiplerine uygun **katmanlı mimari** ile kurulmuş Unity C# projesi.

## Hızlı Başlangıç

1. **Unity Hub** → **Add** → `/Users/f2gomac/Desktop/FPS-GAME` klasörünü seç
2. Unity **6000 LTS** veya **2022.3 LTS** ile aç (URP önerilir)
3. İlk açılışta paketler indirilecek (Input System, URP)
4. Üst menüden: **Project → Setup → Create Test Arena Scene**
5. Ardından: **Project → Setup → Add Scene To Build Settings**
6. **Play** — FPP oyuncu, zemin, kamera, HUD otomatik oluşur

## Kontroller

| Tuş | Aksiyon |
|-----|---------|
| W/A/S/D | Hareket |
| Mouse | Bakış |
| Space | Zıpla |
| Left Shift | Koş |
| Left Ctrl | Eğil |
| **Left Mouse** | Ateş |
| **R** | Reload |
| **F** | Loot al |
| **Right Mouse** | Nişan (ADS için hazır) |

## Proje Yapısı

```
Assets/_Project/
├── Scripts/
│   ├── Core/              # Domain, Interfaces, Events (Unity'siz)
│   ├── Application/       # İş kuralları, servisler
│   ├── Infrastructure/    # Unity implementasyonları, DI, Input
│   ├── Presentation/      # MonoBehaviour, UI, Bootstrap
│   └── Editor/            # Sahne kurulum araçları
├── Scenes/
├── Settings/
└── Prefabs/               # (ileride)
```

## Mimari Özet

| Katman | Sorumluluk | Unity bağımlılığı |
|--------|------------|-------------------|
| **Core** | Entity, enum, interface, event | Hayır |
| **Application** | Match, Zone, Health, Inventory, Weapon logic | Hayır |
| **Infrastructure** | CharacterController, Input, EventBus, DI | Evet |
| **Presentation** | Presenter'lar, HUD, Bootstrap | Evet |

Detaylı dokümantasyon: [ARCHITECTURE.md](ARCHITECTURE.md)

## Hazır Sistemler

- FPP hareket (yürü, koş, eğil, zıpla)
- **Silah sistemi** — hitscan, mermi, reload, recoil viewmodel
- **Loot** — yerde item, F ile alma, envanter servisi
- **Harita blockout** — Verdant Valley prototip (bina, cover, tepe)
- **Test hedefleri** — vurulabilir dummy'ler (headshot destekli)
- Health + hasar + ölüm event'leri
- Match phase state machine (Lobby → PreMatch → InMatch → Ending)
- Zone daralması + zone dışı hasar
- Event-driven HUD (phase, health, ammo, hit feedback)
- DI container + Composition Root
- Editor menüsü ile tek tık sahne kurulumu

## Sonraki Fazlar (Plan)

1. ~~Silah viewmodel + raycast shooting~~ ✅
2. ~~Loot spawn + temel envanter~~ ✅
3. ~~Harita greybox (Verdant Valley)~~ ✅ (prototip)
4. Multiplayer (Mirror / Netcode)
5. Uçak + paraşüt iniş
6. Dedicated server build

## Gereksinimler

- Unity 2022.3 LTS veya 6000 LTS
- Universal Render Pipeline (önerilir)
- Input System paketi (manifest'te tanımlı)

## Not

`TestArenaBootstrap` Play Mode'da eksik objeleri otomatik oluşturur. Production sahnesinde prefab tabanlı kurulum tercih edilir.
