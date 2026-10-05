# Ses Haritası — SoundId

Kaynak: `Assets/_Project/Scripts/Infrastructure/Audio/SoundId.cs`  
Karışım önceliği: GDD [19-ses-tasarim.md](../GDD/19-ses-tasarim.md) (10 = en kritik).  
Katman: **yakın** (yerel / &lt;25 m), **orta** (25–80 m), **uzak** (80 m+ / low-pass).  
Yankı: **iç** (kısa early reflection), **dış** (açık alan / dağ yayılımı), **geçiş** (kapı/pencere low-pass).

> Prosedürel yer tutucudan Sonniss / özel kayıt geçişinde bu tablo eşleme anahtarıdır.  
> `None` oynatılmaz.

## Özet bus öncelikleri

| Öncelik | Bus | SoundId grubu |
|--------:|-----|---------------|
| 10 | Oyuncu geri bildirimi | HitMarker, KillConfirm, Headshot, Death, Heartbeat |
| 9 | Tehlike | ZoneWarning, ZoneDamage, ArtilleryWhistle |
| 8 | Yerel silah | Shot*, DryFire, Reload*, FireModeSwitch, WeaponEquip |
| 7 | Yakın muharebe 3D | BulletWhiz, Explosion, HitFlesh/Helmet/Armor, BulletImpact* |
| 6 | Hareket / araç | Footstep, Jump, Land, VehicleEngine, HelicopterRotor, VehicleDoor |
| 5 | Etkileşim | Pickup, Bandage, Drink, GrenadePin/Bounce, SmokeHiss, Punch |
| 4 | Telsiz / UI | RadioBeep, RadioChatter, UiClick/Hover/Confirm |
| 3 | Ambiyans | Wind, Ambience, DistantBattle |
| 2 | Müzik | MenuMusic |

**Duck:** ArtilleryWhistle → Ambiyans −6 dB; kendi Shot* → DistantBattle −3 dB.

---

## Silah ateşi

| SoundId | Tanım | Katmanlar | Menzil (m) | Yankı | Öncelik | Varyasyon |
|---------|-------|-----------|------------|-------|--------:|----------:|
| ShotPistol | 9 mm kısa gövde, kuru tık | yakın stereo dolgun; orta LP; uzak tiz kuyruk yok | 80 | iç kısa / dış kuru | 8 | 4 |
| ShotSmg | SMG hızlı, hafif gövde | yakın; orta; uzak (sık ateşte pool rotasyon) | 100 | iç / dış | 8 | 6 |
| ShotRifle556 | 5.56 orta gövde (MPT-55) | yakın; orta; uzak | 140 | iç metalik / dış yönlü | 8 | 6 |
| ShotRifle762 | 7.62 dolgun (MPT-76, G3A7) | yakın; orta; uzak + düşük frekans | 160 | iç / dış dağ | 8 | 6 |
| ShotDmr | DMR keskin ön vuruş (KNT-76) | yakın; orta; uzak uzun kuyruk | 180 | dış öncelikli | 8 | 4 |
| ShotSniper | SR ağır gövde + uzun kuyruk (JNG-90) | yakın; orta; uzak (max) | 200 | dış / geçit yankısı | 8 | 4 |
| ShotShotgun | Pompa gövde (Escort) | yakın baskın; orta; uzak kısa | 90 | iç güçlü | 8 | 4 |
| ShotMachineGun | MG sürekli baskı (PMT-76) | yakın dolgun; orta; uzak rumble | 170 | dış | 8 | 8 (burst loop uyumlu) |

## Silah mekaniği

| SoundId | Tanım | Katmanlar | Menzil | Yankı | Öncelik | Varyasyon |
|---------|-------|-----------|--------|-------|--------:|----------:|
| DryFire | Boş şarjör metalik tık | 2D yerel; 3D yakın | 20 | iç hafif | 8 | 2 |
| ReloadMagOut | Şarjör çıkarma | 3D | 25 | iç / dış | 8 | 3 |
| ReloadMagIn | Şarjör kilit tıkı | 3D | 25 | iç / dış | 8 | 3 |
| ReloadBolt | Sürgü / bolt (SR, pompa vurgulu) | 3D | 30 | iç | 8 | 3 |
| ShellInsert | Tek fişek (Escort) | 3D | 20 | iç | 8 | 3 |
| FireModeSwitch | Ateş modu klik | 2D | — | — | 8 | 2 |
| WeaponEquip | Kuşanma kayış/metal | 2D | — | — | 8 | 3 |

## Hareket

| SoundId | Tanım | Katmanlar | Menzil | Yankı | Öncelik | Varyasyon |
|---------|-------|-----------|--------|-------|--------:|----------:|
| Footstep | Yüzey tag: toprak/taş/metal/ahşap/kar/su | 3D; sprint perde↑; crouch −6 dB | 35 | yüzey + mekân | 6 | 8×yüzey (L/R) |
| Jump | Ayak kalkış + ekipman | 3D | 25 | — | 6 | 2 |
| Land | İniş ağırlığı | 3D | 30 | iç kısa | 6 | 3 |

## İsabet / ölüm geri bildirimi

| SoundId | Tanım | Katmanlar | Menzil | Yankı | Öncelik | Varyasyon |
|---------|-------|-----------|--------|-------|--------:|----------:|
| HitMarker | Kısa “tak” (yerel isabet UI) | 2D | — | — | 10 | 2 |
| KillConfirm | Çift tık + alçak ton | 2D | — | — | 10 | 2 |
| Headshot | KillConfirm tiz varyant | 2D | — | — | 10 | 2 |
| HitFlesh | Islak kısa vuruş | 3D | 40 | iç / dış | 7 | 4 |
| HitHelmet | Kask metal | 3D | 45 | iç | 7 | 3 |
| HitArmor | Yelek sert darbe | 3D | 45 | iç | 7 | 3 |
| Death | Nefes kesilmesi (yerel) | 2D | — | — | 10 | 3 |
| Heartbeat | Can ≤%30 loop | 2D fade-in | — | — | 10 | 1 loop |

## Mermi / yakın dövüş / patlayıcı

| SoundId | Tanım | Katmanlar | Menzil | Yankı | Öncelik | Varyasyon |
|---------|-------|-----------|--------|-------|--------:|----------:|
| BulletImpact | Genel yüzey isabeti | 3D | 50 | yüzey | 7 | 6 |
| BulletImpactMetal | Metal sıçrama | 3D | 55 | dış | 7 | 4 |
| BulletWhiz | Yakın geçen mermi ıslığı | 3D stereo pan | 40 | — | 7 | 5 |
| Punch | Yakın dövüş / dipçik | 3D | 15 | — | 5 | 3 |
| Explosion | Patlama gövde + debris | yakın/orta/uzak | 120 | iç güçlü / dış uzun | 7 | 4 |
| ArtilleryWhistle | ~6 sn inen glissando | 3D hedef yönü | 250 | dış | 9 | 2 |
| GrenadePin | Pim çekme | 3D / 2D yerel | 15 | — | 5 | 2 |
| GrenadeBounce | Zemin sekmesi | 3D | 30 | yüzey | 5 | 4 |
| SmokeHiss | Sis salınımı loop başlangıç | 3D | 25 | — | 5 | 2 |

## Etkileşim / UI

| SoundId | Tanım | Katmanlar | Menzil | Yankı | Öncelik | Varyasyon |
|---------|-------|-----------|--------|-------|--------:|----------:|
| Bandage | Sargı sürtünme | 2D | — | — | 5 | 2 |
| Drink | İçecek yudum | 2D | — | — | 5 | 2 |
| Pickup | Eşya alma tık | 2D | — | — | 5 | 3 |
| UiClick | Menü tık | 2D | — | — | 4 | 1 |
| UiHover | Hover hafif | 2D | — | — | 4 | 1 |
| UiConfirm | Onay | 2D | — | — | 4 | 1 |

## Zone / taşıt / ambiyans / telsiz / müzik

| SoundId | Tanım | Katmanlar | Menzil | Yankı | Öncelik | Varyasyon |
|---------|-------|-----------|--------|-------|--------:|----------:|
| ZoneWarning | Siren nabız (daralma uyarısı) | 2D | — | — | 9 | 1 loop |
| ZoneDamage | Zone hasar cızırtı | 2D | — | — | 9 | 2 |
| HelicopterRotor | T-70 rotor loop | 3D follow | 200 | dış | 6 | 1 loop + RPM pitch |
| VehicleEngine | Kirpi motor loop | 3D | 150 | dış / iç kabin LP | 6 | 1 loop + RPM |
| VehicleDoor | Kapı aç/kapa | 3D | 40 | metal | 6 | 2 (aç/kapa) |
| Wind | Rüzgâr loop (sırt/tepe) | 3D ambient | LOD kes | dış | 3 | 3 bölge |
| Ambience | Harita/bölge beden sesi | 2D/3D bed | LOD | mekân | 3 | harita×bölge |
| DistantBattle | Uzak muharebe yatağı | 2D bed | — | — | 3 | 4 |
| MenuMusic | Ana menü / lobiler | 2D | — | — | 2 | stem’ler (bkz. muzik_brifi) |
| RadioBeep | Telsiz PTT / ton | 2D | — | — | 4 | 2 (aç/kapa) |
| RadioChatter | Replik taşıyıcı / filtre | 2D VO bus | — | telsiz HP | 4 | replik seti |

---

## Uygulama notları

1. Silah Shot*: yerel nişancı stereo merkez + dolgun; uzak low-pass + mesafe atenuasyonu.  
2. Footstep: yüzey `Terrain`/`Tag` → clip set; L/R flip veya ayrı örnek.  
3. Ambience / Wind: `ortam_sesleri.md` bölge tablolarına bağlanır; tek genel loop yetmez.  
4. RadioChatter: `telsiz_replikleri.csv` anahtarları ile VO; RadioBeep öncesi/sonrası.  
5. MenuMusic stem’leri maç içinde kapalı; Heartbeat varken müzik yok.
