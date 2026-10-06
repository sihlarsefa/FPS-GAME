# VFX Graph Spec (HAREKÂT)

Cursor bu grafikleri Unity editöründe kurar. Paket: `com.unity.visualeffectgraph` (URP 17 ile uyumlu). Paket kurulunca `HAREKAT_VFXGRAPH` define'ı otomatik gelir (asmdef `Project.Infrastructure.VfxGraph`). Paket veya `.vfx` yoksa oyun CPU parçacık yoluna (GameVfx) düşer; hiçbir şey kırılmaz.

## Genel kurallar
- Dosya yolu: `Assets/_Project/Resources/VFX/VFX_<Efekt>.vfx` (ad `GpuVfxBudget.ResourceName`: `VFX_MuzzleRifle`, `VFX_MuzzlePistol`, `VFX_MuzzleShotgun`, `VFX_MuzzleSniper`, `VFX_MuzzleSuppressed`, `VFX_MuzzleMachineGun`, `VFX_Impact`, `VFX_Explosion`, `VFX_RotorDust`, `VFX_Rain`, `VFX_Snow`, `VFX_SmokeGrenade`).
- Her grafikte **Spawn Event**: `OnPlay` (tek atım burst). Rain/Snow: sürekli spawn (Constant Rate), `Intensity` ile çarpılır.
- Dışa açık (exposed) özellik adları (yoksa kod sessizce atlar, `HasX` kontrolü var):
  - `Scale` (float, 1) — boyut/çarpan
  - `Direction` (Vector3) — namlu/yüzey normali yönü (dünya)
  - `Surface` (int) — `SurfaceKind`: 0 Default,1 Dirt,2 Concrete,3 Metal,4 Wood,5 Flesh,6 Water,7 Foliage,8 Snow
  - `Tier` (int 0..3), `Capacity` (int; bilgi amaçlı, asıl Capacity grafikte kademeye göre ayarlanır)
  - `Duration` (float sn; SmokeGrenade/Explosion duman ömrü)
  - `Radius` (float m; Explosion/SmokeGrenade/RotorDust)
  - `Intensity` (float 0..1; Rain/Snow)
- Sistem: World space simülasyon, `Bounds` elle (Initialize'da sabit kutu: silah 3 m, patlama 40 m, yağmur 60 m). Culling: `Instancing` açık, `Prewarm` kapalı (Rain/Snow hariç 2 sn).
- Malzeme: URP Particle Unlit/Lit Shader Graph çıktıları; flipbook dokular `VfxTextures` ile aynı atlaslardan. Soft particle (depth fade) açık, ışık tepkisi Lit duman için açık.
- Yarıçap/yaşam ölçeği `Scale` ile çarpılır; ışık (noktasal) GameVfx'te CPU'da kalır.

## Grafikler
1. **Muzzle (6 sınıf)**: Burst: 3-6 alev yaprağı (additive flipbook 4x4, ömür 0.04-0.07 s), 8-24 kıvılcım (stretched billboard, hız 15-40 m/s), 6-14 duman (ömür 0.6-1.5 s, yukarı sürüklenme). Sınıf farkları: Pistol Scale 0.6; Rifle 1.0; MachineGun 1.2 + daha uzun duman; Shotgun geniş koni (35°) + yoğun duman; Sniper uzun parlak alev + yer tozu (ground check ışını yok, `Surface` kullanılabilir); Suppressed alev yok, yalnız ince duman. Capacity: 128 (Orta) / 256 (Yüksek) / 512 (Ultra).
2. **Impact (SurfaceKind)**: `Surface` ile dallanma. Dirt: toz bulutu + 6-10 toprak parçası. Concrete: gri toz + 8-14 kıvılcım + moloz kırıntıları. Metal: 12-24 sıcak kıvılcım (stretched, yerçekimli) + duman yok. Wood: kıymık (mesh küçük) + açık renk toz. Snow: beyaz toz. Water/Flesh/Foliage GameVfx CPU yolunda kalır (Flesh = kan ayrı). Parçalar çarpışmalı (Depth Buffer collision) zeminde sekebilir. Capacity 64/128/256.
3. **Explosion**: (a) ateş topu: 20-40 additive flipbook, ömür 0.5-1.2 s, renk gradyanı beyaz-sarı→turuncu→koyu; (b) şok dalgası: tek halka quad, ölçek 0→Radius*2 in 0.25 s, distortion (opsiyonel, kademe 3); (c) duman sütunu: 40-100 parçacık, yukarı hız 6-12 m/s, ömür 4-8 s, `Duration` ile uzar; (d) moloz: 30-80 küçük mesh/quad, balistik, yer çarpışması; (e) kıvılcım: 60 adet. Capacity 750/1500/3000.
4. **RotorDust**: `Radius` etrafında halka şeklinde emitter (yerden 0.2 m), dışa doğru 8-14 m/s, toz/kum rengi `Surface` ile (Dirt kahve, Snow beyaz). Sürekli (heli yakınken Reinit ile tekrar tetiklenir). Capacity 500/1000/2000.
5. **Rain**: kamerayı izleyen 40x40x30 m kutu, çizgi (stretched billboard) düşüş 12-18 m/s, rüzgârla eğim (`Wind` global vektör opsiyonel). Yere çarpınca küçük sıçrama (ikinci sistem, yalnız kademe 3). `Intensity` spawn hızını ölçekler. Capacity 3000/5000/8000.
6. **Snow**: aynı kutu, yavaş (1-2 m/s) sürüklenen yuvarlak flipbook, türbülans gürültüsü. Capacity 2500/4000/6000.
7. **SmokeGrenade**: ilk patlama 0.5 s'de 60 parça, sonra `Duration` (varsayılan 25 s) boyunca hacim: 150-400 büyük yumuşak sprite (boyut 3-6 m, `Radius` yarıçapına sığar), yavaş dönen, sönüm son 4 s. Lit (ışık alır), soft particle, sıralama için `Sort` açık. Capacity 500/1000/2000. Kademe 3: 3D SDF kürenin içinde gürültü ile hacim görünümü.

## Kademe bütçesi (kod: `GpuVfxBudget`)
| Kademe | Durum | Aynı anda örnek (Impact / Muzzle / Patlama / Duman) | Parçacık çarpanı |
|---|---|---|---|
| 0 Düşük | KAPALI (CPU yolu) | 0 | 0 |
| 1 Orta | Açık | 8 / 3 / 1 / 1 | x1 |
| 2 Yüksek | Açık | 16 / 6 / 2 / 3 | x2 |
| 3 Ultra | Açık | 24 / 9 / 3 / 4 | x4 |
Toplam GPU parçacık hedefi: Orta ≤ 15k, Yüksek ≤ 40k, Ultra ≤ 100k canlı. VFX GPU süresi ≤ 1.2 ms (Orta, GTX1660S 1080p).

## Cursor doğrulama listesi
- Paketi kur, `HAREKAT_VFXGRAPH` define'ının Project.Infrastructure.VfxGraph'ta göründüğünü doğrula (Player Settings > Scripting Define Symbols'ta değil, asmdef versionDefines ile).
- Her .vfx'i yukarıdaki ad ve exposed adlarla kaydet; Play modunda `GpuVfx.Available` true olmalı.
- Kapat/aç: .vfx'i sil → tek uyarı logu, CPU efektleri çalışmaya devam eder.
- Entegrasyon: GameVfx çağrılarına (aşağıdaki kancalar) eklenince CPU spawn'ı atlanır.
- Profiler: GPU VFX ms ve parçacık sayısı kademe tablosunu aşmıyor mu.
