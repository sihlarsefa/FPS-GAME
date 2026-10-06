# Performans (60 FPS hedefi, orta seviye Windows PC, 40-60 savaşçı)

Kaynak: `Infrastructure/Rendering/PerformanceProfile.cs` + `PostProcessing.ApplyQuality`.

## Kalite tablosu

| Ayar | Düşük | Orta | Yüksek | Ultra |
|---|---|---|---|---|
| Gölge mesafesi (m) | 60 | 110 | 170 | 260 |
| Gölge kademesi | 1 | 2 | 3 | 4 |
| LOD bias | 0.7 | 1.0 | 1.4 | 2.0 |
| Küçük eşya/loot/mermi çizim mesafesi (m) | 90 | 140 | 200 | 300 |
| Bot/oyuncu çizim mesafesi (m) | 220 | 320 | 450 | 600 |
| Arazi ağaç mesafesi (m) | 450 | 700 | 900 | 1100 |
| Ağaç billboard başlangıcı (m) | 90 | 130 | 170 | 220 |
| Tam LOD ağaç sayısı | 800 | 1500 | 2500 | 4000 |
| Arazi ayrıntı (çim) mesafesi (m) | 0 | 40 | 70 | 100 |
| Ek ışık sınırı (öneri) | 2 | 4 | 6 | 8 |

Kamera katman kırpması `camera.layerCullDistances` (küresel) ile uygulanır; CameraRig yeni kamera kurarken ve kalite değişince yeniden uygular.

## Önlemler
- Malzemeler: `MaterialLibrary.Lit/Unlit` önbellekli, paylaşımlı malzeme döner (SRP Batcher dostu); sıcak yollarda `renderer.material` kullanmayın.
- SceneBuilder: tüm dünya nesneleri BatchingStatic + OccludeeStatic + NavigationStatic; büyük yapılar (sınır kutusu >= 12 m) ek olarak OccluderStatic.
- Oklüzyon: editörde `HAREKÂT/Performans/Oklüzyon Bake (açık sahne)` (isteğe bağlı, kamera `useOcclusionCulling` açık).
- Kayalar chunk başına birleştirilmiş mesh; ağaçlar arazi ağacı (billboard LOD).
- VFX havuz tavanları: iz mermisi 160, delik 150, yanık 16, namlu ışığı 3, patlama ışığı 2, parçacık havuzları 8-24.

## Öneriler (kapsam dışı, AI)
- Bot Update LOD: oyuncuya >120 m botlar 5-10 Hz karar, >250 m animasyon/Hitbox kapalı; 40-60 bot için raycast bütçesi kare başına sabit.
