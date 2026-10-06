# Design/Assets — Hazır varlık eşleme CSV'leri

Kolon adları **sabit** (F3-4 Varlık Eşleyici bunları okur). İçerik satırlarını Codex **C3-1** doldurur.

| Dosya | Kolonlar |
|-------|----------|
| `materials.csv` | `MaterialId,kaynak_site,varlik_adi,url,lisans,ucretsiz_mi,notlar` |
| `sounds.csv` | `SoundId,kaynak_paket,dosya_onerisi,katman(yakın/orta/uzak),lisans,notlar` |
| `animations.csv` | `durum,mixamo_animasyon_adi,in_place,notlar` |
| `models.csv` | `kimlik(WeaponId/arac/asker/bina),gecici_hazir_model_onerisi,kaynak,fiyat_araligi,benzerlik_notu,lisans` |

Kimlik örnekleri: `WeaponIds.*`, `asker`, `kirpi`, `t70`, `BuildingStyle` adları.

Unity eşleme: **HAREKÂT → İçerik → Varlık Eşleyici → CSV Ön Doldur**.

Arazi / çim / foliage kalite sırası: [Design/Art/ENVIRONMENT.md](../Art/ENVIRONMENT.md).
`materials.csv` satırlarında zemin katmanları (Grass, DryGrass, Dirt, Rock, Mud) için Poly Haven / ambientCG önerileri öncelikli kalsın; mesh foliage paketleri kalite aşamasında `Environment/` altına not edilir.
