# Arazi, çim ve çevre — görsel kalite planı

> Şimdiki sistem çöpe gitmez. Prosedürel terrain + splat + `ContentOverrides` iskeleti kalır;
> kalite aşamasında katmanlar eklenir.

## Şimdi (stabil — bozma)

| Katman | Teknoloji | Durum |
|--------|-----------|--------|
| Yükseklik / vadi / dere | Unity **Terrain** + prosedürel gürültü (`TerrainNoise` / `TerrainModel`) | Kodda |
| Zemin rengi | **Splat / alphamap** (`TerrainPainter`): Grass, DryGrass, Dirt, Rock, Mud, Snow, Gravel, Asphalt | Kodda |
| Ağaç / büyük dekor | Terrain trees + `PropFactory` / `LocationBuilder` | Kodda |
| Silah / asker / bina görünümü | Prosedürel mesh **veya** `ContentOverrides` | F3-4 |
| Render | **URP** (şimdilik sabit) | Proje |

Splat, zeminin *materyalini* çözer; diz boyu gerçek çim / rüzgâr / yoğun bitki *değildir* — bu bilinçli.

## Sonra (kalite aşaması — entegrasyon + oynanabilir build sonrası)

```
Unity Terrain
├── Splat / Alphamap        ← mevcut (koru)
├── Foliage Layer (yeni)    ← mesh grass / çalı / eğrelti / çiçek / küçük taş
└── Tree Layer              ← mevcut + kaliteli prefab override
```

### Foliage gereksinimleri

- GPU **Instancing** (veya Unity Terrain detail / özel instancer)
- **LOD** + mesafe **culling**
- Wind shader (hafif; performans bütçesine bağlı)
- Terrain normal hizalama
- Rastgele scale / rotation / density (biome / DryGrass vs Grass maskesine göre)

Kaynak önerisi: Poly Haven / Asset Store foliage; eşleme `ContentOverrides` veya ayrı `FoliageCatalog` (ileride). Klasör: `Assets/ThirdParty/Environment/`.

### Render notu (URP vs HDRP)

- **Şimdi ve yakın vadede URP.** Pipeline değişimi tüm materyalleri kırar.
- Tarkov / AAA fotoreal hedefi için **ileride** HDRP değerlendirmesi yapılabilir; zorunlu değil.
- Önce: iyi asset + lighting + foliage + URP Lit/Shader Graph ile tavanı zorla.

## Modelleme yolu (değişmez)

```
WeaponModelFactory / SoldierModel / …
        ↓
ContentOverrides.TryGet*(id) ?
   evet → gerçek FBX/prefab
   hayır → prosedürel yedek
```

Artist asseti gecikse bile oyun çalışır.

## Lisans

Her paket: `Assets/ThirdParty/README.md` tablosu + `Design/Assets/*.csv`.  
Steam: ticari kullanım + yeniden dağıtım onayı şart.

İlgili: [ArtBible.md](ArtBible.md) · [Docs/CURSOR_FAZ3.md](../../Docs/CURSOR_FAZ3.md) F3-4 · C3-1 CSV.
