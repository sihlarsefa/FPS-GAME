# Third-Party Assets

Hazır paketler (Asset Store, Mixamo, Sonniss, Poly Haven, ambientCG) buraya konur.
Kodla üretilen görünüm yedek kalır; eşleme `ContentOverrides` + **HAREKÂT → İçerik → Varlık Eşleyici** ile yapılır.

## Klasör düzeni

```
Assets/ThirdParty/
  Mixamo/           # FBX + Animator (Humanoid)
    Characters/
    Animations/
    Animators/      # Mixamo helper çıktısı
  Weapons/          # Silah prefab / FBX
  Vehicles/         # Kirpi, T-70
  Buildings/        # Köy evi, cami, karakol, …
  Materials/        # Poly Haven / ambientCG materyaller
  Audio/            # Sonniss / Asset Store klipler
  Characters/       # Asker modelleri (Mixamo dışı)
```

Unity dışı öneri listesi ve lisans notları: `Design/Assets/*.csv` (Codex C3-1).

## Lisans kayıt tablosu

Her eklenen paket için bir satır doldurun. Steam dağıtımı için **ticari kullanım** ve **yeniden dağıtım** iznini doğrulayın.

| Tarih | Paket / varlık | Kaynak | Lisans | Ticari | Steam OK | Klasör | Not |
|-------|----------------|--------|--------|--------|----------|--------|-----|
| | | | | | | | |

## İçe aktarma kuralları

| Tür | Ayar |
|-----|------|
| Mixamo karakter / anim | Rig = **Humanoid**, Animation Type = Humanoid |
| Silah FBX | Ölçek metre; Transform adları: `Muzzle`, `Grip_R`, `Grip_L`, `Magazine`, `Bolt`, `Sight` |
| Araç / bina | Pivot zemin / tekerlek hizası; birim = metre |
| Dokular | URP Lit; mümkünse 2K (arazi 1K atlas) |
| Ses | Mono (3B) / Stereo (UI/müzik); varyasyonlar aynı `SoundId` altına |

## Menüler

- **HAREKÂT / İçerik / Varlık Eşleyici** — kimlik ↔ prefab/clip/material
- **HAREKÂT / İçerik / Mixamo İçe Aktarma** — Humanoid + Animator Controller
- **HAREKÂT / İçerik / ContentOverrides Oluştur** — `Resources/ContentOverrides.asset`
