# U4. Ses Tasarım Dokümanı

Kaynak kimlikler: `SoundId` (prosedürel üretim).

## 19.1 Bus / karışım öncelikleri

Öncelik yüksek → düşük (aynı anda çakışmada kıs):

| Öncelik | Bus | Örnekler |
|---------|-----|----------|
| 10 | Kritik oyuncu geri bildirimi | HitMarker, KillConfirm, Headshot, Death, Heartbeat |
| 9 | Tehlike uyarı | ZoneWarning, ZoneDamage, ArtilleryWhistle |
| 8 | Yerel silah | Shot*, Reload*, DryFire, FireModeSwitch |
| 7 | Yakın muharebe 3D | BulletWhiz, Explosion, HitFlesh/Armor/Helmet |
| 6 | Hareket / araç | Footstep, Jump, Land, VehicleEngine, HelicopterRotor |
| 5 | Etkileşim | Pickup, Bandage, Drink, GrenadePin/Bounce, SmokeHiss |
| 4 | Telsiz / UI | RadioBeep, RadioChatter, UiClick/Hover/Confirm |
| 3 | Ambiyans | Wind, Ambience, DistantBattle |
| 2 | Müzik | MenuMusic |

**Duck kuralları:** ArtilleryWhistle sırasında Ambiyans −6 dB; kendi Shot* iken DistantBattle −3 dB.

## 19.2 Silah ateşleri

| SoundId | Kullanım |
|---------|----------|
| ShotPistol | SAR 9, TP9 |
| ShotSmg | SAR 109T |
| ShotRifle556 | MPT-55 |
| ShotRifle762 | MPT-76, G3A7 |
| ShotDmr | KNT-76 |
| ShotSniper | JNG-90 (daha uzun kuyruk) |
| ShotShotgun | Escort |
| ShotMachineGun | PMT-76 (daha dolgun gövde) |

`PlayGunshot`: yerel nişancı için daha dolgun + stereo merkez; uzak için low-pass + mesafe atenuasyonu (maxDistance silaha göre 80–200 m).

## 19.3 Olay kartları

| Olay | SoundId | Tarif | 2D/3D | Max mesafe |
|------|---------|-------|-------|------------|
| Kuru ateş | DryFire | Metalik tık | 2D yerel / 3D uzak | 20 |
| Şarjör çık | ReloadMagOut | Plastik-metal | 3D | 25 |
| Şarjör gir | ReloadMagIn | Kilit tıkı | 3D | 25 |
| Sürgü | ReloadBolt | JNG/pompada vurgulu | 3D | 30 |
| Fişek | ShellInsert | Escort tek tek | 3D | 20 |
| Mod değiştir | FireModeSwitch | Küçük klik | 2D | — |
| Kuşan | WeaponEquip | Kayış/metal | 2D | — |
| Adım | Footstep | Zemin türüne göre perde | 3D | 35 |
| İsabet UI | HitMarker | Kısa “tak” | 2D | — |
| Kill | KillConfirm | Çift tık + alçak ton | 2D | — |
| Kafa | Headshot | Daha tiz kill | 2D | — |
| Et | HitFlesh | Islak kısa | 3D | 40 |
| Kask/zırh | HitHelmet/Armor | Metalik | 3D | 45 |
| Islık | ArtilleryWhistle | 6 sn inen glissando | 3D hedef | 250 |
| Patlama | Explosion | Düşük frekans gövde | 3D | 120 |
| Zone uyarı | ZoneWarning | Siren benzeri nabız | 2D | — |
| Zone hasar | ZoneDamage | Elektriksel cızırtı | 2D | — |
| Rotor | HelicopterRotor | Loop | 3D follow | 200 |
| Motor | VehicleEngine | Loop RPM’e pitch | 3D | 150 |
| Telsiz | RadioBeep/Chatter | Komuta geri bildirimi | 2D | — |
| Ölüm | Death | Kısa nefes kesilmesi | 2D | — |
| Kalp | Heartbeat | Can düşükken loop | 2D | — |

## 19.4 Dinamik durumlar

- **Can ≤ %30:** Heartbeat fade-in; müzik yok.  
- **Sis içinde:** dış Shot* high-cut.  
- **İzleyici modu:** HitMarker yok; DistantBattle + Ambiyans öne.

## 19.5 Uygulama notları

- Havuz: `GameAudio` DontDestroyOnLoad.  
- Per-frame alloc yok; loop’lar `StartLoop`/`StopLoop`.  
- Ayarlar: Master / Ambient ayrı (`GameAudio` API).
