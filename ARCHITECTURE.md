# Mimari Dokümantasyon

## Genel Bakış

Bu proje büyük ölçekli bir battle royale oyunu için **Clean Architecture** ve **SOLID** prensiplerine göre tasarlanmıştır. Unity `MonoBehaviour` sınıfları ince tutulur; iş mantığı test edilebilir katmanlarda yaşar.

```
┌─────────────────────────────────────────────────────────┐
│                    Presentation                          │
│  FirstPersonPlayerPresenter, MatchHudPresenter,         │
│  GameBootstrap, TestArenaBootstrap                       │
└──────────────────────────┬──────────────────────────────┘
                           │ depends on
┌──────────────────────────▼──────────────────────────────┐
│                   Infrastructure                         │
│  CharacterControllerMotor, UnityInputReader, EventBus,  │
│  ServiceContainer, PlayerHealthComponent                 │
└──────────────────────────┬──────────────────────────────┘
                           │ depends on
┌──────────────────────────▼──────────────────────────────┐
│                    Application                           │
│  HealthService, MatchService, ZoneService,              │
│  InventoryService, BasicWeaponService                    │
└──────────────────────────┬──────────────────────────────┘
                           │ depends on
┌──────────────────────────▼──────────────────────────────┐
│                       Core                               │
│  Domain models, Enums, Interfaces, Events                │
└─────────────────────────────────────────────────────────┘
```

## SOLID Uygulaması

### Single Responsibility (S)

Her sınıf tek bir değişim nedeni taşır:

| Sınıf | Tek sorumluluk |
|-------|----------------|
| `HealthService` | Can hesaplama ve hasar/iyileşme |
| `MatchService` | Maç fazı geçişleri |
| `ZoneService` | Güvenli alan geometrisi ve daralma |
| `CharacterControllerMotor` | Fiziksel hareket uygulama |
| `FirstPersonCameraController` | Pitch kontrolü |
| `FirstPersonPlayerPresenter` | Input → motor/kamera orchestration |

### Open/Closed (O)

Yeni silah türleri `IWeapon` implementasyonu eklenerek genişletilir; mevcut `BasicWeaponService` değiştirilmez. Loot sistemi `ILootSpawnService` üzerinden genişler.

### Liskov Substitution (L)

`IDamageable` implementasyonları (`HealthService`, ileride `VehicleHealthService`) birbirinin yerine kullanılabilir.

### Interface Segregation (I)

Büyük arayüzler parçalandı:

- `IMovementInputReader` / `ILookInputReader` — input okuma ayrı
- `IHealthReadModel` / `IDamageable` / `IHealable` — sağlık sorgulama vs mutasyon ayrı
- `IGameTickService` — sadece tick

### Dependency Inversion (D)

Presentation ve Infrastructure, somut sınıflara değil interface'lere bağımlıdır. Bağımlılıklar `GameCompositionRoot` ile kayıt edilir.

```csharp
// Composition Root — tek bağlantı noktası
var container = GameCompositionRoot.Build(MatchConfig.Default, 40);
var matchService = container.Resolve<IMatchService>();
```

## Assembly Definitions

| Assembly | Referanslar | Unity Engine |
|----------|-------------|--------------|
| `Project.Core` | — | Hayır |
| `Project.Application` | Core | Hayır |
| `Project.Infrastructure` | Core, Application | Evet |
| `Project.Presentation` | Core, Application, Infrastructure | Evet |
| `Project.Editor` | Tümü (Editor only) | Evet |

Core ve Application katmanları `noEngineReferences: true` ile saf C# kalır.

## Event-Driven İletişim

Sistemler arası gevşek bağlantı `IEventBus` ile sağlanır:

- `PlayerDamagedEvent` — HUD, ses, hit marker
- `PlayerDiedEvent` — kill feed, spectator mode
- `MatchPhaseChangedEvent` — UI, spawn logic

Yayınlayan servisler alıcıları bilmez (pub/sub).

## FPP Oyuncu Akışı

```
UnityInputReader.Read()
        │
        ▼
FirstPersonPlayerPresenter (Update)
        │
        ├──► IPlayerMotor.ApplyMovement()
        ├──► IPlayerMotor.ApplyRotation(yaw)
        └──► IFirstPersonCamera.ApplyLook(pitch)
```

## Match State Machine

```
None → Lobby → PreMatch → InMatch → Ending
         │         │          │
         5s      config    alive <= 1
```

`MatchService.Tick()` faz zamanlayıcılarını yönetir.

## Zone Sistemi

`ZoneService` harita düzleminde dairesel alan tutar. `ZoneDamageController` (Infrastructure) oyuncu pozisyonunu kontrol eder ve zone dışında `IDamageable.ApplyDamage` çağırır.

## Genişletme Rehberi

### Yeni silah eklemek

1. `Core`: gerekirse yeni `WeaponCategory`
2. `Application`: `IWeapon` implementasyonu
3. `Infrastructure`: `WeaponViewModel`, raycast, animasyon
4. `Presentation`: `WeaponPresenter`

### Multiplayer eklemek

1. `Core`: `INetworkSession` genişlet
2. `Infrastructure`: Mirror/Netcode adapter
3. Server-authoritative hasar: `HealthService` sadece server'da çalışsın
4. `FirstPersonPlayerPresenter` → network owner kontrolü

### Yeni harita eklemek

1. Scene + spawn point'ler
2. `IPlayerSpawnService` implementasyonu
3. `ILootSpawnService` zone tabloları (ScriptableObject)

## Test Stratejisi

| Katman | Test türü |
|--------|-----------|
| Core / Application | Unit test (NUnit, Unity'siz) |
| Infrastructure | Integration test |
| Presentation | Play Mode test |

Örnek unit test adayları: `HealthService`, `MatchService`, `ZoneService`, `InventoryService`.

## Klasör Konvansiyonları

- `Domain/Enums` — sabit listeler
- `Domain/Models` — immutable struct'lar
- `Interfaces/` — port tanımları
- `Events/` — domain event'leri
- `Services/` — use case implementasyonları
- `Presentation/*Presenter` — view logic, UI bağlama

## Bilinçli Erken Kararlar

1. **Custom DI** — VContainer/Zenject eklenebilir; MVP için hafif container yeterli
2. **TestArenaBootstrap** — hızlı prototip; production'da prefab workflow
3. **OnGUI HUD** — geçici; TextMeshPro UI ile değiştirilecek
4. **Network stub** — `INetworkSession` interface hazır, implementasyon sonraki faz
