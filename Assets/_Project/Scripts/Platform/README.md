# HAREKÂT — Steam Platform

Unity istemci katmanı: `SteamPlatformService` (asmdef `Project.Platform`, `defineConstraints: ["STEAMWORKS_NET"]`).

Steamworks.NET **yokken** bu assembly derlenmez; ana proje (Core / Application / Infrastructure / Presentation) etkilenmez.

---

## 1. Steamworks.NET kurulumu

1. OpenUPM veya git ile paket ekleyin:
   ```
   com.rlabrecque.steamworks.net
   ```
   Alternatif: [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) release’ini `Assets/Plugins/Steamworks.NET` altına koyun; asmdef adı `com.rlabrecque.steamworks.net` olmalı.
2. `Project.Platform.asmdef` içindeki `versionDefines` paketi görünce `STEAMWORKS_NET` sembolünü açar; assembly derlenir.
3. Editör testi için proje köküne `steam_appid.txt` yazın (tek satır AppID). Geliştirme: **480** (Spacewar). Üretim: Partner’daki gerçek AppID.
4. Steam client açık ve aynı kullanıcıyla giriş yapılmış olmalı.

---

## 2. AppID alma

1. [Steamworks Partner](https://partner.steamgames.com/) hesabı ve şirket/sözleşme onayı.
2. **Create new app** → AppID atanır (ör. `3xxxxxx`).
3. Store / Technical / Packaging sekmelerini doldurun.
4. Üretim build’lerinde `steam_appid.txt` **dağıtılmamalı**; Steam overlay AppID’yi launch options’tan bilir. Editör / standalone debug için dosya yeterlidir.
5. Web API Key: Partner → Users & Permissions → Web API. Backend `Steam:WebApiKey` olarak ayarlanır (`appsettings` / ortam değişkeni). **Repoya koymayın.**

---

## 3. Windows depot’ları

Önerilen yapı (yalnızca Windows; Linux yok):

| Depot | İçerik | Not |
|-------|--------|-----|
| Depot 1 — Content | `HAREKAT.exe` + `HAREKAT_Data/` + Unity Player DLL’leri | Ana oyun |
| Depot 2 — Redistributables (isteğe bağlı) | VC++ runtime installer | Steam “install scripts” ile |
| DLC / dil depot’ları | Sonra eklenebilir | Şimdilik tek dil paketi oyun içinde |

Steamworks Partner → App Admin → SteamPipe → Depots:

1. “Windows” depot oluşturun (`Launch Options` → Executable: `HAREKAT.exe`).
2. OS: Windows 64-bit.
3. Preview / Live branch’leri ayırın (`beta` preview, `default` live).

---

## 4. Build yükleme (SteamPipe)

1. Partner’dan **SteamPipe GUI** veya `steamcmd` + ContentBuilder indirin.
2. Unity’den `Builds/Windows/` altına player build alın (x86_64, IL2CPP veya Mono).
3. `app_build_XXXXX.vdf` örneği:

```vdf
"AppBuild"
{
  "AppID" "YOUR_APP_ID"
  "Desc" "HAREKAT Windows build"
  "BuildOutput" "D:\\SteamPipe\\output"
  "ContentRoot" "D:\\FPS-GAME\\Builds\\Windows\\"
  "Depots"
  {
    "YOUR_DEPOT_ID"
    {
      "FileMapping"
      {
        "LocalPath" "*"
        "DepotPath" "."
        "recursive" "1"
      }
    }
  }
}
```

4. Yükleme:
   ```
   steamcmd +login <builder_account> +run_app_build app_build_XXXXX.vdf +quit
   ```
5. Partner → Builds → preview branch’e set → dahili test → `default`’a promote.

---

## 5. Mağaza onay süreci (özet)

1. **Store presence:** capsule, library hero, trailer, ekran görüntüleri (`Marketing/steam/`).
2. **Store page** TR/EN metinleri Partner’a yapıştırılır; fiyat ve bölgeler.
3. **Review:** Steam Store review + build readiness (depot, Achievements, Cloud isteğe bağlı).
4. **Achievements:** `SteamAchievementMap` API adlarıyla Partner’da 30+ başarım tanımlayın; ikonlar 64×64 / 256×256.
5. **Rich Presence:** Localization token `#Status_Match` / `#Status_Menu` (veya yalnızca `status` anahtarı).
6. Coming Soon → Playtest / Wishlist → Release checklist → Launch.

---

## 6. Kod kullanımı

```csharp
var steam = SteamPlatformService.Instance;
if (steam != null && steam.IsInitialized)
{
    steam.SetMatchRichPresence("Kuzgun Vadisi", 23);
    steam.UnlockAchievement("first_victory");

    // Backend JWT
    var json = await steam.AuthenticateWithBackendAsync("https://api.harekat.example");
}
```

Backend uç: `POST /auth/steam`  
Gövde: `{ "ticket": "<hex>", "personaName": "İsim" }`  
Yanıt: standart `AuthResponse` (access + refresh JWT).

Geliştirme (Steam Web API yokken): ticket `DEV:<steamid64>:<persona>` — yalnızca `Steam:AllowDevTickets=true`.

---

## 7. Kanca (Claude / F2-5)

Rich presence ve başarım senkronu için `MatchBootstrap` / maç bitiş akışına çağrı eklenir — bkz. `Docs/FAZ3_KANCALAR.md`.
