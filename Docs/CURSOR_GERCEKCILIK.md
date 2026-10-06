# Cursor — Gerçekçilik (URP, kesin plan)

Kaynak: [`GERCEKCILIK_PLANI.md`](GERCEKCILIK_PLANI.md) · **HDRP yok**.

## M0 (hemen)

| ID | İş |
|----|-----|
| C7 | ContentOverrides v2 slot iskeleti + Editor doğrulayıcı |
| U2 | Faz A ambientCG/Poly Haven arazi+yapı dokuları → ThirdParty + ContentOverrides |
| U1 | C1 (Forward+/GRD/STP/APV) sonrası Frame Debugger — Claude C1 bitince |
| U8 | Sonniss / ses (indirme listesi + SoundId bağlama) |

## Kabul

Override yoksa prosedürel yedek bozulmaz. Her teslim: önce/sonra ekran + lisans satırı `ThirdParty/README.md`.

## Dikey dilim

Kuzgun Köyü (~300×300 m) — M3 kapısı: Orta 60 FPS + sahip onayı.

---

## KARAR GELDİ (2026-10-06 00:45, Claude) — `Docs/GERCEKCILIK_PLANI.md`
- **URP'de kalınıyor, HDRP YOK** (HDRP 2026'da bakım modunda; viewmodel stacking URP'de hazır; 60 FPS hedefi).
- Hedef: GTX 1660S / RTX 3050, 1080p Orta, ort. 60 FPS, %1 low ≥ 45.
- **Cursor sırası:** plandaki **§4.3 U1–U11** görevleri birebir uygulanır. Önce **U1** (C1 pipeline ajanı bitince) + **U2/U4** (Faz A ücretsiz varlıklar: ambientCG/Poly Haven doku + HDRI, §3.3).
- Claude şu an **G3 dalgası** (C1–C15, 15 ajan) yazıyor: `Rendering/*`, `Content/*`, `World/Terrain*|Tree*|Rock*|Vegetation*`, `Weapons/WeaponViewModel*`, `Audio/*`, `Vfx/*`, `Characters/Soldier*`, `Transport/*Model*`, `Vehicles/*ModelBuilder*`, katalog isim profili. Bu dosyalara dokunma; hata görürsen DURUM.md'ye not.
- Yeni slotlar (C7): TerrainLayer / Vegetation / Rock / Prop / ViewmodelArms / WeaponAnimation / Sky / DecalSet → Varlık Eşleyici'de görünecek; doğrulayıcı menüsü OK/UYARI/HATA raporu üretir.
- Faz B (150–450 USD) ve Faz C (özel model 15–40 bin USD) satın alma **kullanıcı onayı** ister — sen indirme yapma, listeyi hazırla.

## EK (2026-10-06 00:55, Claude) — AAA benchmark + ses önceliği
- Kullanıcı kararı: **Unity/URP'de kalınıyor, motor değişmiyor.** Hedef görünüm: Tarkov silah hissi + Sons of the Forest çevresi + bizim askerî atmosfer.
- **Önce küçük AAA BENCHMARK SAHNESİ** (`AAA_Benchmark`: 1 asker, MPT-76, Kirpi, 1 ev, küçük orman, çamur+kaya+çim, gündüz+sis, ateş VFX, reload, decal, ses). Kullanıcı "gerçekten çok iyi" demeden büyük haritalara yayma YOK. Ayrıntı: `Docs/CURSOR_AAA_BENCHMARK.md` (G4 ajanı yazıyor) ve `Docs/AAA_URP_20.md` (20 madde).
- Claude paralel dalgalar: **G3** (C1–C15), **G4** (renderer feature: contact shadow, SSR-lite, volumetrik sis/ışık, planar yansıma, terrain height-blend+POM+çamur, GPU mesh çim+rüzgâr shader, HLOD/streaming, VFX Graph köprüsü, benchmark sahnesi), **G5** (ses: balistik akustik, silah foley, diyalog v2, Türkçe ses v2, HDR miks + ortam).
- **Cursor öncelik sırası:** (1) G4 shader'larını Unity'de derle ve hataları düzelt (`Assets/_Project/Shaders/**` — burada derlenemiyor), (2) benchmark sahnesini kur, (3) ücretsiz varlıklar: ambientCG/Poly Haven çamur-kaya-çim + HDRI, Megascans ağaç, Mixamo tüfek animasyonları, **Sonniss GDC silah kayıtları** → `Resources/Audio/Weapons/<weaponId>/<katman>_<n>.wav` (G5 kuralı, `Docs/SES_GERCEKCILIK.md`), (4) VFX Graph grafiklerini `Docs/VFX_GRAPH_SPEC.md`'ye göre üret, (5) önce/sonra ekran + FPS CSV.
