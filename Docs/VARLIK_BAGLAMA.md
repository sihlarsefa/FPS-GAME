# Varlık Bağlama (V1 — fotogrametri doku + HDRI)

Çalıştırma: Unity batch `-executeMethod Project.EditorTools.BatchEntry.BindThirdParty`
(`ThirdPartyBinder.BindAll()` + varsa `ThirdPartyModelBinder.BindAll()` + ContentOverrides doğrulayıcı).

Maske kuralı (MaterialLibrary/ProceduralPbr ile aynı): `<Set>_Mask.png` = R metalik, G AO, B 0.5, A = 1 - roughness (sRGB kapalı).
Üretilen varlıklar: `Assets/ThirdParty/Materials/HK_<Set>.mat` (URP/Lit), `HK_TL_<Set>.terrainlayer`. Normal haritalar ambientCG NormalGL (Unity ile uyumlu).

## MaterialId -> doku seti (MaterialOverride)

| MaterialId | Set | Not |
|---|---|---|
| Mud | Ground036 | ıslak çamur |
| Dirt | Ground106 | kuru toprak |
| Grass | Grass001 | |
| DryGrass | Grass007 | |
| PineNeedles | ScatteredLeaves009 | orman zemini |
| Rock | Rock058 | granit/kayalık |
| RockDark | Rock035 | |
| Gravel | Gravel023 | çakıl yol |
| Asphalt | Asphalt031 | |
| Concrete / ConcreteDark | Concrete034 / Concrete036 | |
| Plaster / PlasterWarm | Plaster004 / Plaster007 | eski sıva |
| Stone | Rock051 | moloz taş duvar |
| StoneDark | Rocks011 | |
| RoofTile | RoofingTiles013A | kil kiremit |
| RoofMetal | CorrugatedSteel007A | |
| Wood / WoodDark | Planks037A / Planks023A | |
| Rust | Metal021 | paslı sac |
| TentCanvas | Fabric062 | askeri kumaş |
| Sandbag | Fabric045 | kaba çuval dokuması, haki ton (0.74, 0.64, 0.47) ile çarpılır |

## Arazi katmanları (TerrainLayerOverride)

Grass=Grass001, DryGrass=Grass007, Dirt=Ground106, Mud=Ground036, Rock=Rock058, RockDark=Rock035, Gravel=Gravel023, Asphalt=Asphalt031, PineNeedles=ScatteredLeaves009.
TerrainTextureFactory yalnız Grass/DryGrass/Dirt/Rock/Gravel/Mud/Snow/Asphalt okur. Ground037 (nemli orman) ve Ground067 (kayalık orman toprağı) indirildi, henüz slota bağlı değil.

## Gökyüzü (SkyOverride, Cubemap)

| skyId | HDRI |
|---|---|
| day_clear | autumn_hilly_field_4k.hdr |
| cloudy | cannon_4k.exr |
| sunset | belfast_sunset_4k.hdr |
| sunrise | bloem_field_sunrise_4k.hdr (AtmosphereMath.SkyId şafakta henüz "sunset" döndürüyor) |

## Modeller (Vegetation / Rock / Prop)

`Editor/ThirdParty/ThirdPartyModelBinder.BindAll()` (BatchEntry.BindThirdParty içinden) Poly Haven CC0 FBX'lerini işler:
ModelImporter (globalScale 1, collider/okuma/kamera/ışık/animasyon kapalı) → URP/Lit materyal (`Assets/ThirdParty/Materials/PH/`; normal map,
metal haritası varsa metalik, alpha varsa diff+alpha RGBA PNG + alfa kırpma + çift yüz) → `Assets/ThirdParty/Prefabs/<varlık>.prefab`
(kök + LODGroup + collider; taban merkezde, 1 birim = 1 m) → `ContentOverrides.asset` vegetation/rocks/props. Modelde `_LOD0/1/2` varsa o kademeler;
yoksa opak ve >3000 üçgen ise köşe-kümeleme ile ~1/4 üçgenli LOD1 (`Prefabs/Meshes/`), yaprakta tek kademe + kesme. Bozuk/eksik model slotu boş bırakır → prosedürel yedek geçerli.

| Slot | Varlıklar (Models/<id>) | Ölçek |
|---|---|---|
| veg `pine` | fir_sapling, pine_sapling_small | 6.5-7.5 m yükseklik, gövde kapsül collider |
| veg `oak` | *(boş — jacaranda ~3.8M üçgen; prosedürel)* | - |
| veg `bush` | shrub_01..04, fern_02 | doğal |
| veg `grass` | grass_medium_01/02 | doğal (çağrı noktası yok; ileride) |
| veg `dead` | *(boş: ayakta ölü ağaç modeli yok; prosedürel)* | - |
| rock `small` | stone_01 (0.9 m), rock_07 (1.0 m) | en büyük boyut |
| rock `medium` | boulder_01, namaqualand_boulder_02/03/05 (1.5-1.7 m) | en büyük boyut |
| rock `large` | namaqualand_cliff_01 (5.5 m), namaqualand_boulders_01, rock_face_01 (3.5 m) | en büyük boyut |
| prop `ammo_crate` | wooden_military_crate | doğal |
| prop `barrel` | barrel_03 | doğal |
| prop `barrier` | concrete_road_barrier | doğal |
| prop `stump` | tree_stump_01 | doğal |
| prop (çağrı noktası yok, ileride) | crate, crate_02, ammo_box, barrel_explosive, barrel_wood, barrel_wine, fence, jerrycan, bucket(_02), ladder(_02), spade, basket, bags, cement_bag, handtruck, stump_02, log(_02), branches | doğal |

Çağrı noktaları (boşsa/yoksa prosedürel): `TreeFactory.CreateDefaultPrototypes` → `TryGetVegetation`; `RockScatter.TryPlaceOverride` → `TryGetRock`
(prefab doğal boyutla konur, boyut sınıfı hedefleri yukarıda); `PropFactory.TryOverride` (yeni, null-güvenli) → AmmoCrate/Barrel/ConcreteBarrier/TreeStump → `TryGetProp`.
Not: meşe/ayakta ölü ağaç/köy evi için CC0 düşük-poli model yok; lisans kaydı Credits.json `polyhaven-models` + `thirdparty-prefabs-generated`, satırlar Assets/ThirdParty/README.md.
