# FAZ 3 — Claude kancaları (henüz uygulanmadı)

> Cursor yeni modülleri yazarken Claude’un dosyalarına dokunmaz.
> Aşağıdaki değişiklikler Claude "ENTEGRASYON/İNCELEME TAMAMLANDI" yazınca veya F2-5 penceresinde uygulanır.

| Dosya | Eklenecek | Neden |
|-------|-----------|-------|
| `Presentation/Bootstrap/GameSession.cs` | `public static Func<INetworkSession> NetworkSessionFactory;` + Online kayıt | Online assembly kendini kaydeder |
| Ana menü (MainMenuController / benzeri) | "ONLİNE" butonu → OnlineLoginPanel | F3-1 menü girişi |
| `MatchBootstrap` / bootstrap | DevTools konsol oluştur (`DevConsole.Ensure`) | F3-5 ` tuşu |
| `MatchBootstrap` / maç HUD | `SteamPlatformService.Instance?.SetMatchRichPresence(map, alive)` | F3-7 rich presence |
| Maç bitiş / başarım unlock | `SteamPlatformService.Instance?.UnlockAchievements(newIds)` | F3-7 Steam başarımları |
| `WeaponModelFactory.Build` | önce `ContentOverrides.TryGetWeapon` | F3-4 hazır varlık |
| `SoldierModel.Build` | ContentOverrides asker | F3-4 |
| `GameAudio.GetClip` / Play | ContentOverrides ses | F3-4 |
| `MaterialLibrary.Get` | ContentOverrides malzeme | F3-4 |
| Helicopter / ArmoredCarrier builder | ContentOverrides araç | F3-4 |
| `Packages/manifest.json` | `com.unity.netcode.gameobjects` (2.13.2) + `com.unity.transport` (yalnızca F2-4) | F3-2 — asmdef `HAREKAT_NETCODE` / versionDefines |
| Oyuncu prefab / spawn | `NetworkObject` + `NetworkPlayer` + `NetworkTransform` + NetworkManager PlayerPrefab | F3-2 ServerRpc / vitals |
| `PlayerController` / komut gönderimi | Online’da `IPlayerCommandSink` → `NetcodeNetworkSession.Submit` (+ `SubmitShot`) | F3-2 tick’li komut / atış |
| `BotController.Create` / AI Update | `if (!ServerBotGate.ShouldRunBots) return;` — botlar yalnızca sunucu | F3-2 server-only botlar |
| `MatchBootstrap` maç sonu | `ServerBootstrap.Instance?.SubmitMatchResultAndIdleAsync(matchId, json)` | F3-2 backend sonuç |
| `MatchBootstrap` maç başı (dedicated) | `ServerBootstrap.Instance?.NotifyMatchStarted(matchId)` | F3-2 heartbeat status |
| NetworkManager sahne/prefab | ConnectionApproval + max 60; InterestManager zaten runtime | F3-2 60 oyuncu |

## Günlük
- 2026-10-05 20:28 — Dosya açıldı; F3 ajanları satır ekleyecek.
- 2026-10-05 20:35 — F3-7: Steam rich presence + başarım kancaları eklendi.
- 2026-10-05 20:31 — F3-2: Netcode paket / prefab / bot / maç sonucu kancaları eklendi.
