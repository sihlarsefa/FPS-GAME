# Third-Party Assets

Hazır paketler (Asset Store, Mixamo, Sonniss, Poly Haven, ambientCG, Megascans) buraya konur.
Kodla üretilen görünüm yedek kalır; eşleme `ContentOverrides` + **HAREKÂT → İçerik → Varlık Eşleyici** ile yapılır.

**Gerçekçilik dalgası:** Araştırma bitince Cursor onay beklemeden indirir ve bağlar —
görev listesi [`Docs/CURSOR_GERCEKCILIK.md`](../../Docs/CURSOR_GERCEKCILIK.md).
HDRP yalnızca araştırma önerirse; aksi halde URP + bu klasör.

## Klasör düzeni

```
Assets/ThirdParty/
  Mixamo/           # FBX + Animator (Humanoid)
  Characters/
  Animations/
  Animators/        # Mixamo helper çıktısı
  Weapons/          # Silah prefab / FBX
  Vehicles/         # Kirpi, T-70
  Buildings/        # Köy evi, cami, karakol, …
  Environment/      # Mesh grass, çalı, eğrelti, kaya prop, foliage atlas
  Materials/        # Poly Haven / ambientCG materyaller
  Audio/            # Sonniss / Asset Store klipler
```

Arazi/çim kalite sırası: [Design/Art/ENVIRONMENT.md](../../Design/Art/ENVIRONMENT.md) (önce splat, sonra mesh foliage).

Unity dışı öneri listesi ve lisans notları: `Design/Assets/*.csv` (Codex C3-1).
İsteğe bağlı özet: aynı lisans satırlarını `ThirdPartyLicenses.md` dosyasına da kopyalayabilirsiniz (Steam denetimi için).

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

## Asker ve araç prefab gereksinimleri

ContentOverrides (`Resources/ContentOverrides`) ile bağlanır; alanlar boşsa prosedürel model kullanılır.

**Asker (humanoid)**: Humanoid rig'li prefab (Animator.isHuman ise sağ el `HumanBodyBones.RightHand` silahı taşır). Ayak tabanı y=0, +Z ileri. Collider'lar silinir (oyun mantığı prosedürel vuruş kutularında). AnimatorController parametreleri (olanlar sürülür, eksikler yok sayılır): `Speed` (float, m/s), `Crouch` (float 0-1 veya bool), `Prone` (float 0-1 veya bool), `AimPitch` (float, derece; + aşağı), `Dead` (bool).

**Helikopter** (`ContentIds.Helicopter`): Orijin gövde merkezi, +Z burun. İsteğe bağlı alt nesneler `MainRotor`, `TailRotor` (Y / X ekseninde döner); biri yoksa prosedürel rotor eklenir. Koltuklar (`Seat_0..9`) ve çarpıştırıcılar prosedürel kalır.

**Kirpi** (`ContentIds.Kirpi`): Orijin zemin ortası, +Z ön. İsteğe bağlı: `Wheel_0..N` (WheelPositions sırası; hepsi yoksa prosedürel teker), `Door` (arka kapı menteşesi), `Turret` (yaw ekseni). `Seat_0..9` ve collider'lar prosedürel kalır. Vehicles/KirpiModelBuilder: prosedürel görseller gizlenir, collider'lar kalır; prefab kule içermelidir.

### V1 — Fotogrametri dokular + HDRI (2026-10-06)

| Ad | Kaynak URL | Lisans | Yazar | Tarih | Boyut |
|----|-----------|--------|-------|-------|-------|
| Asphalt031 (2K-JPG) | https://ambientcg.com/view?id=Asphalt031 | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 17M |
| Concrete034 (2K-JPG) | https://ambientcg.com/view?id=Concrete034 | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 5.1M |
| Concrete036 (2K-JPG) | https://ambientcg.com/view?id=Concrete036 | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 13M |
| CorrugatedSteel007A (2K-JPG) | https://ambientcg.com/view?id=CorrugatedSteel007A | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 8.7M |
| Fabric062 (2K-JPG) | https://ambientcg.com/view?id=Fabric062 | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 18M |
| Grass001 (2K-JPG) | https://ambientcg.com/view?id=Grass001 | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 23M |
| Grass007 (2K-JPG) | https://ambientcg.com/view?id=Grass007 | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 21M |
| Gravel023 (2K-JPG) | https://ambientcg.com/view?id=Gravel023 | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 18M |
| Ground036 (2K-JPG) | https://ambientcg.com/view?id=Ground036 | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 22M |
| Ground037 (2K-JPG) | https://ambientcg.com/view?id=Ground037 | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 23M |
| Ground067 (2K-JPG) | https://ambientcg.com/view?id=Ground067 | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 22M |
| Ground106 (2K-JPG) | https://ambientcg.com/view?id=Ground106 | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 20M |
| Metal021 (2K-JPG) | https://ambientcg.com/view?id=Metal021 | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 13M |
| Planks023A (2K-JPG) | https://ambientcg.com/view?id=Planks023A | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 13M |
| Planks037A (2K-JPG) | https://ambientcg.com/view?id=Planks037A | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 13M |
| Plaster004 (2K-JPG) | https://ambientcg.com/view?id=Plaster004 | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 15M |
| Plaster007 (2K-JPG) | https://ambientcg.com/view?id=Plaster007 | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 17M |
| Rock035 (2K-JPG) | https://ambientcg.com/view?id=Rock035 | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 19M |
| Rock051 (2K-JPG) | https://ambientcg.com/view?id=Rock051 | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 18M |
| Rock058 (2K-JPG) | https://ambientcg.com/view?id=Rock058 | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 16M |
| Rocks011 (2K-JPG) | https://ambientcg.com/view?id=Rocks011 | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 16M |
| RoofingTiles013A (2K-JPG) | https://ambientcg.com/view?id=RoofingTiles013A | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 6.9M |
| ScatteredLeaves009 (2K-JPG) | https://ambientcg.com/view?id=ScatteredLeaves009 | CC0-1.0 | ambientCG (Lennart Demes) | 2026-10-06 | 23M |
| HDRI autumn_hilly_field (4K) | https://polyhaven.com/a/autumn_hilly_field | CC0-1.0 | Poly Haven (Grzegorz Wronkowski) | 2026-10-06 | 23M |
| HDRI cannon (4K) | https://polyhaven.com/a/cannon | CC0-1.0 | Poly Haven (Greg Zaal) | 2026-10-06 | 6.5M |
| HDRI bloem_field_sunrise (4K) | https://polyhaven.com/a/bloem_field_sunrise | CC0-1.0 | Poly Haven (Jenelle van Heerden / Greg Zaal) | 2026-10-06 | 25M |
| HDRI belfast_sunset (4K) | https://polyhaven.com/a/belfast_sunset | CC0-1.0 | Poly Haven (Greg Zaal / Dimitrios Savva) | 2026-10-06 | 24M |

Normal haritalar ambientCG `NormalGL` (OpenGL = Unity) JPG olarak alındı; Displacement dosyaları atıldı. Eşleme tablosu: Docs/VARLIK_BAGLAMA.md.

### Ses (CC0, OpenGameArt) — 2026-10-06
İşlenmiş çıktılar (44.1 kHz mono, kırpılmış, kategori başına ses düzeyi dengelenmiş) `Assets/_Project/Resources/Audio/{Weapons/_class,Ambience,SFX}` altındadır. Ham indirmeler repoda DEĞİL. Tüm kaynaklar OpenGameArt sayfasında CC0 olarak işaretli (atıf gerekmez; yine de kredi veriyoruz).

| Ad | Kaynak URL | Lisans | Yazar | Tarih | Boyut |
|---|---|---|---|---|---|
| The Free Firearm Sound Library (Prepared SFX; 22 silah, yakın+orta mesafe atış) | https://opengameart.org/content/the-free-firearm-sound-library | CC0-1.0 | Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney (yükleyen: bart) | 2026-10-06 | 194M (7z) |
| Gun reload sounds (assaultriflereload1, gunreload1, shotguncock) | https://opengameart.org/content/gun-reload-sounds | CC0-1.0 | SpringySpringo | 2026-10-06 | 0.6M |
| Handgun reload sound effect (reload.wav) | https://opengameart.org/content/handgun-reload-sound-effect | CC0-1.0 | zer0_sol | 2026-10-06 | 0.3M |
| Gun reload sound effects (clipload1/2, singlebullet1) | https://opengameart.org/content/gun-reload-sound-effects | CC0-1.0 | BMacZero | 2026-10-06 | 0.07M |
| Shotgun Reload Sound effects (Rack, First/Subsequent Shell) | https://opengameart.org/content/shotgun-reload-sound-effects | CC0-1.0 | zer0_sol | 2026-10-06 | 0.3M |
| Gun reload lock or click sound | https://opengameart.org/content/gun-reload-lock-or-click-sound | CC0-1.0 | pauliuw | 2026-10-06 | 0.02M |
| Equipment Clicks III (equipment_clicks3.wav) | https://opengameart.org/content/equipment-clicks-iii | CC0-1.0 | LFA | 2026-10-06 | 2M |
| Chunky Explosion | https://opengameart.org/content/chunky-explosion | CC0-1.0 | Joth | 2026-10-06 | 0.1M |
| Mild Wind Background Noise | https://opengameart.org/content/mild-wind-background-noise | CC0-1.0 | Bashar3A | 2026-10-06 | 16M |
| Wind whoosh loop | https://opengameart.org/content/wind-whoosh-loop | CC0-1.0 | SketchMan3 | 2026-10-06 | 0.25M |
| Rain (loopable) | https://opengameart.org/content/rain-loopable | CC0-1.0 | Ylmir | 2026-10-06 | 2.7M |
| Crickets ambient noise (loopable) | https://opengameart.org/content/crickets-ambient-noise-loopable | CC0-1.0 | Wolfgang_ | 2026-10-06 | 0.2M |
| Ambient bird sounds | https://opengameart.org/content/ambient-bird-sounds | CC0-1.0 | isaiah658 | 2026-10-06 | 0.5M |
| Amb Outside 1 | https://opengameart.org/content/amb-outside-1 | CC0-1.0 | Kresiek The Furry | 2026-10-06 | 0.3M |

Not: Mermi vızıltısı/çatlama (whiz/crack), kovan düşme (beton/toprak), kuş/baykuş/köpek/horoz tek seferlikleri, MG kemer sesi ve gerçek sürgü (bolt) bu turda CC0 olarak bulunamadı; prosedürel yedek çalışır. Liste ve adımlar: Docs/SES_KAYNAK_LISTESI.md.

### V2 — Poly Haven 3B modeller (2026-10-06)

Lisans: CC0-1.0 (Poly Haven). FBX + dokular (JPG/PNG; EXR yerine PNG/JPG karşılığı alındı). Bağlama: `Editor/ThirdParty/ThirdPartyModelBinder.cs`, eşleme `Docs/VARLIK_BAGLAMA.md` > Modeller.

| Ad | Kaynak URL | Lisans | Yazar | Tarih | Boyut |
|----|-----------|--------|-------|-------|-------|
| Barrel_01 (Barrel_01, FBX 1K) | https://polyhaven.com/a/Barrel_01 | CC0-1.0 | Poly Haven (Jorge Camacho) | 2026-10-06 | 4.0M |
| ammo_box (Ammo Box, FBX 1K) | https://polyhaven.com/a/ammo_box | CC0-1.0 | Poly Haven (DanKit) | 2026-10-06 | 1.4M |
| barrel_03 (Barrel 03, FBX 1K) | https://polyhaven.com/a/barrel_03 | CC0-1.0 | Poly Haven (Serhii Khromov) | 2026-10-06 | 4.9M |
| boulder_01 (Boulder 01, FBX 2K) | https://polyhaven.com/a/boulder_01 | CC0-1.0 | Poly Haven (Rico Cilliers) | 2026-10-06 | 34.1M |
| cement_bag (Cement Bag, FBX 1K) | https://polyhaven.com/a/cement_bag | CC0-1.0 | Poly Haven (PierreB3D) | 2026-10-06 | 5.2M |
| compost_bags (Compost Bags, FBX 1K) | https://polyhaven.com/a/compost_bags | CC0-1.0 | Poly Haven (James Ray Cock) | 2026-10-06 | 7.3M |
| concrete_road_barrier (Concrete Road Barrier, FBX 1K) | https://polyhaven.com/a/concrete_road_barrier | CC0-1.0 | Poly Haven (Amal Kumar) | 2026-10-06 | 13.1M |
| dead_tree_trunk (Dead Tree Trunk, FBX 1K) | https://polyhaven.com/a/dead_tree_trunk | CC0-1.0 | Poly Haven (Rob Tuytel) | 2026-10-06 | 14.5M |
| dead_tree_trunk_02 (Dead Tree Trunk 02, FBX 1K) | https://polyhaven.com/a/dead_tree_trunk_02 | CC0-1.0 | Poly Haven (Jenelle van Heerden, Rico Cilliers) | 2026-10-06 | 13.8M |
| dry_branches_medium_01 (Dry Branches Medium 01, FBX 1K) | https://polyhaven.com/a/dry_branches_medium_01 | CC0-1.0 | Poly Haven (Rico Cilliers) | 2026-10-06 | 8.4M |
| fern_02 (Fern 02, FBX 1K) | https://polyhaven.com/a/fern_02 | CC0-1.0 | Poly Haven (Rob Tuytel, Rico Cilliers) | 2026-10-06 | 4.1M |
| fir_sapling (Fir Sapling, FBX 1K) | https://polyhaven.com/a/fir_sapling | CC0-1.0 | Poly Haven (Rob Tuytel, Rico Cilliers) | 2026-10-06 | 38.3M |
| grass_medium_01 (Grass Medium 01, FBX 1K) | https://polyhaven.com/a/grass_medium_01 | CC0-1.0 | Poly Haven (Rob Tuytel, Rico Cilliers) | 2026-10-06 | 6.9M |
| grass_medium_02 (Grass Medium 02, FBX 1K) | https://polyhaven.com/a/grass_medium_02 | CC0-1.0 | Poly Haven (Rico Cilliers) | 2026-10-06 | 1.4M |
| hand_truck (Hand Truck, FBX 1K) | https://polyhaven.com/a/hand_truck | CC0-1.0 | Poly Haven (Mutanzom3D) | 2026-10-06 | 8.5M |
| jacaranda_tree (Jacaranda Tree, FBX 1K) | https://polyhaven.com/a/jacaranda_tree | CC0-1.0 | Poly Haven (Rob Tuytel, Rico Cilliers) | 2026-10-06 | 168.0M |
| metal_jerrycan_green (Metal Jerrycan Green, FBX 1K) | https://polyhaven.com/a/metal_jerrycan_green | CC0-1.0 | Poly Haven (Ulan Cabanilla) | 2026-10-06 | 10.7M |
| modular_chainlink_fence (Modular Chainlink Fence, FBX 1K) | https://polyhaven.com/a/modular_chainlink_fence | CC0-1.0 | Poly Haven (James Ray Cock, Amal Kumar) | 2026-10-06 | 16.0M |
| namaqualand_boulder_02 (Namaqualand Boulder 02, FBX 2K) | https://polyhaven.com/a/namaqualand_boulder_02 | CC0-1.0 | Poly Haven (Greg Zaal, Rico Cilliers) | 2026-10-06 | 30.5M |
| namaqualand_boulder_03 (Namaqualand Boulder 03, FBX 2K) | https://polyhaven.com/a/namaqualand_boulder_03 | CC0-1.0 | Poly Haven (Jenelle van Heerden, Dario Barresi) | 2026-10-06 | 23.8M |
| namaqualand_boulder_05 (Namaqualand Boulder 05, FBX 2K) | https://polyhaven.com/a/namaqualand_boulder_05 | CC0-1.0 | Poly Haven (Jenelle van Heerden, Dario Barresi) | 2026-10-06 | 26.3M |
| namaqualand_boulders_01 (Namaqualand Boulders 01, FBX 2K) | https://polyhaven.com/a/namaqualand_boulders_01 | CC0-1.0 | Poly Haven (Greg Zaal, Jenelle van Heerden) | 2026-10-06 | 25.0M |
| namaqualand_cliff_01 (Namaqualand Cliff 01, FBX 2K) | https://polyhaven.com/a/namaqualand_cliff_01 | CC0-1.0 | Poly Haven (Jenelle van Heerden, Rico Cilliers) | 2026-10-06 | 29.1M |
| pine_sapling_small (Pine Sapling Small, FBX 1K) | https://polyhaven.com/a/pine_sapling_small | CC0-1.0 | Poly Haven (Rob Tuytel, Rico Cilliers) | 2026-10-06 | 38.3M |
| rock_07 (Rock 07, FBX 2K) | https://polyhaven.com/a/rock_07 | CC0-1.0 | Poly Haven (Jenelle van Heerden) | 2026-10-06 | 19.0M |
| rock_face_01 (Rock Face 01, FBX 2K) | https://polyhaven.com/a/rock_face_01 | CC0-1.0 | Poly Haven (Dario Barresi) | 2026-10-06 | 23.8M |
| rock_moss_set_01 (Rock Moss Set 01, FBX 2K) | https://polyhaven.com/a/rock_moss_set_01 | CC0-1.0 | Poly Haven (Kless Gyzen) | 2026-10-06 | 28.3M |
| rusted_spade_01 (Rusted Spade 01, FBX 1K) | https://polyhaven.com/a/rusted_spade_01 | CC0-1.0 | Poly Haven (Blemonade) | 2026-10-06 | 7.2M |
| shrub_01 (Shrub 01, FBX 1K) | https://polyhaven.com/a/shrub_01 | CC0-1.0 | Poly Haven (Rico Cilliers) | 2026-10-06 | 10.0M |
| shrub_02 (Shrub 02, FBX 1K) | https://polyhaven.com/a/shrub_02 | CC0-1.0 | Poly Haven (Rico Cilliers) | 2026-10-06 | 3.3M |
| shrub_03 (Shrub 03, FBX 1K) | https://polyhaven.com/a/shrub_03 | CC0-1.0 | Poly Haven (Rico Cilliers) | 2026-10-06 | 4.8M |
| shrub_04 (Shrub 04, FBX 1K) | https://polyhaven.com/a/shrub_04 | CC0-1.0 | Poly Haven (Rico Cilliers) | 2026-10-06 | 3.1M |
| stone_01 (Stone 01, FBX 2K) | https://polyhaven.com/a/stone_01 | CC0-1.0 | Poly Haven (Dario Barresi, Rico Cilliers) | 2026-10-06 | 34.9M |
| tree_stump_01 (Tree Stump 01, FBX 1K) | https://polyhaven.com/a/tree_stump_01 | CC0-1.0 | Poly Haven (Rob Tuytel) | 2026-10-06 | 10.6M |
| tree_stump_02 (Tree Stump 02, FBX 1K) | https://polyhaven.com/a/tree_stump_02 | CC0-1.0 | Poly Haven (Rob Tuytel) | 2026-10-06 | 12.6M |
| wicker_basket_01 (Wicker Basket 01, FBX 1K) | https://polyhaven.com/a/wicker_basket_01 | CC0-1.0 | Poly Haven (Kuutti Siitonen) | 2026-10-06 | 6.9M |
| wine_barrel_01 (Wine Barrel 01, FBX 1K) | https://polyhaven.com/a/wine_barrel_01 | CC0-1.0 | Poly Haven (James Ray Cock) | 2026-10-06 | 2.1M |
| wooden_barrels_01 (Wooden Barrels 01, FBX 1K) | https://polyhaven.com/a/wooden_barrels_01 | CC0-1.0 | Poly Haven (James Ray Cock) | 2026-10-06 | 34.7M |
| wooden_bucket_01 (Wooden Bucket 01, FBX 1K) | https://polyhaven.com/a/wooden_bucket_01 | CC0-1.0 | Poly Haven (James Ray Cock) | 2026-10-06 | 6.9M |
| wooden_bucket_02 (Wooden Bucket 02, FBX 1K) | https://polyhaven.com/a/wooden_bucket_02 | CC0-1.0 | Poly Haven (James Ray Cock) | 2026-10-06 | 7.7M |
| wooden_crate_01 (Wooden Crate 01, FBX 1K) | https://polyhaven.com/a/wooden_crate_01 | CC0-1.0 | Poly Haven (James Ray Cock) | 2026-10-06 | 7.4M |
| wooden_crate_02 (Wooden Crate 02, FBX 1K) | https://polyhaven.com/a/wooden_crate_02 | CC0-1.0 | Poly Haven (James Ray Cock, Jurita Burger) | 2026-10-06 | 6.7M |
| wooden_ladder (Wooden Ladder, FBX 1K) | https://polyhaven.com/a/wooden_ladder | CC0-1.0 | Poly Haven (Miroslav Turura) | 2026-10-06 | 7.1M |
| wooden_ladder_02 (Wooden Ladder 02, FBX 1K) | https://polyhaven.com/a/wooden_ladder_02 | CC0-1.0 | Poly Haven (JN_3DPRINTINGNERD) | 2026-10-06 | 7.0M |
| wooden_military_crate (Wooden Military Crate, FBX 1K) | https://polyhaven.com/a/wooden_military_crate | CC0-1.0 | Poly Haven (Prabhjinder Singh) | 2026-10-06 | 7.5M |

Toplam model indirmesi: 785 MB.

## Silah modelleri (2026-10-06)

| name | url | license | author | date | size |
|---|---|---|---|---|---|
| Ultimate Gun Pack (FBX: AssaultRifle_1/2/3, AssaultRifle2_1/2, SniperRifle_2/4/5, SubmachineGun_1, Pistol_1/2/3, Shotgun_1/2; Weapons/<model>/) | https://opengameart.org/content/low-poly-guns-pack (zip: https://opengameart.org/sites/default/files/ultimate_gun_pack_by_quaternius.zip) | CC0 (sayfada "License(s): CC0") | Quaternius | 2026-10-06 | 7.4M zip (14 FBX ~0.5M) |

Not: OGA M4A1 (m4a1.zip) ve Lamoot AK-47 sayfasında CC0 doğrulanamadı (m4a1 sayfası 404, AK-47 sayfasında lisans satırı yok) - indirilip kullanılmadı. Eşleme: Editor/ThirdParty/ThirdPartyWeaponBinder.cs.
