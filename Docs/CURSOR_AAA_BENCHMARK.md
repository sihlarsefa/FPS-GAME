# CURSOR — AAA Benchmark Sahnesi (S8)

> **Kural:** Bu küçük sahne ("AAA_Benchmark", 150×150 m) sahibinin ağzından **"gerçekten çok iyi"** denmeden 10 km'lik haritaya
> veya mevcut harekât haritalarına (Kuzgun Vadisi, Ayaz Geçidi, Mavi Liman, Kartal Yaylası) hiçbir AAA grafik özelliği
> **yayılmaz**. Yüksek grafikte darboğaz kod değil: asset kalitesi + shader + ışık + animasyon + VFX + optimizasyon *aynı anda* iyi olmalı.
> Hedef görünüm: **Tarkov silah hissi + Sons of the Forest çevre kalitesi + bizim askeri atmosfer.**

## Sahne ve kullanım
- Menü: `HAREKÂT/Kurulum/AAA Benchmark Sahnesi` (sahneyi üretir + Build Settings). Entegrasyon: `ProjectSetup.RunAllSteps` içine `AaaBenchmarkSceneBuilder.EnsureScene()` satırı.
- Oyunda: ana menü → **AAA TEST SAHNESİ** (Oyna) veya **AAA VİTRİN** (betikli kamera). **F9** modlar arası, **F12** ekran görüntüsü → `Logs/benchmark/aaa_*.png`.
- İçerik: çamur + kaya + çim bölgeleri, küçük orman, 1 köy evi (girilir), 1 Kirpi, 1 nöbetçi asker, MPT-76'lı oyuncu, çelik hedefler + beton duvar, gündüz + hafif sis.
- Sabit referans pozlar için Vitrin planları: 1 silah yakın plan/reload, 2 asker, 3 Kirpi, 4 ev içi, 5 orman, 6 hedeflere ateş. Her değişiklikten önce/sonra aynı planlarda F12 ile karşılaştır.
- Her özellik kademe başına açık/kapalı + maliyet düğmesine sahip olmalı; Düşük'te kapalı veya en ucuz. Benchmark Ultra'da kurulur.

## 20 maddelik kontrol listesi (nasıl doğrulanır)
| # | Madde | Bu sahnede nasıl doğrulanır |
|---|---|---|
| 1 | Custom URP renderer features | Frame Debugger: özel pass'ler (SSAO, kontak gölge, sis, ışık hüzmesi...) sırayla görünür; Ultra'da hepsi var, Düşük'te yok. |
| 2 | SSR benzeri çözüm | Çelik hedef/Kirpi camı/su birikintisine bak: ekranda görünen nesneler yansır; kenarda sıçrama/titreme yok. |
| 3 | SSAO | Ev köşeleri, kaya dipleri, ağaç kökleri koyulaşır; halo/gürültü yok; Düşük'te kapalı. |
| 4 | Contact shadows | Silah/asker ayakları, çelik hedef direği, kaya-zemin teması ince gölge verir; kenarda sızma yok. |
| 5 | Volumetric / custom fog | Vitrin plan 5 (orman): uzak ağaçlar katman katman silinir; sis rengi gökyüzü ufkuna uyar. |
| 6 | Volumetric light shafts | Orman planında güneş ağaç aralarından hüzme verir; kamera güneşe bakınca patlama yok. |
| 7 | Yüksek kaliteli CSM | Asker/Kirpi/ev gölgesi 4 kademede keskin; kademe geçişi görünmez; ADS'te silah gölgesi doğru. |
| 8 | Reflection probes | Sahnede gerçek zamanlı probe var (`Benchmark Yansıma Probu`); ev içi/dışı ayrı probe değerlendir (iç mekân sızıntı yok). |
| 9 | Planar reflection gereken yüzeyler | Su birikintisi/cam/cilalı metal için özel çözüm; yalnız gereken yüzeyde çalışır, maliyet ölç. |
| 10 | PBR standardizasyonu | Tüm malzemeler aynı albedo/normal/MRAO sözleşmesi; Rendering Debugger → Albedo/Smoothness doğrulama aralıklarında. |
| 11 | Terrain height blending | Çamur–çim–kaya geçişleri yükseklik haritasıyla keskin/doğal; ham alfa karışımı gibi bulanık değil. |
| 12 | Mesh çim + GPU instancing | Çim yoğunluğu ve mesafe kademeye göre; SRP Batcher/instancing açık, draw call sayısı Stats'ta düşük. |
| 13 | Rüzgâr shader'ı | Çim, yaprak, dal rüzgârda uyumlu sallanır; Kirpi/asker yakınında eğilme (opsiyonel). |
| 14 | Decal sistemi | Çelik hedef ve beton duvara ateş: mermi izi, kıvılcım, yanık; kademe başına sınır, eski izler solar. |
| 15 | Parallax / detail mapping | Duvar, kaya, çamurda yakından derinlik; uzakta kapanır, kayma/yüzme yok. |
| 16 | VFX Graph | Namlu alevi/dumanı, kovan, toz, çamur sıçraması; GPU maliyeti Profiler'da ölç; yedek (partikül sistemi) var. |
| 17 | Post-process + color grading | Plan 1–6'nın hepsinde tutarlı renk; siyah/beyaz noktalar kırpılmıyor (histogram); Tarkov'dan yumuşak kontrast, askerî soluk ton. |
| 18 | Dinamik çözünürlük / upscaling | Kasıtlı yük bindir (Vitrin plan 6): çözünürlük ölçeği iner/çıkar; STP/FSR keskinlik ve gölgelenme (ghosting) yok. |
| 19 | LOD + HLOD + occlusion + streaming | Ağaç/kaya/ev LOD geçişi fark edilmez; ev içindeyken dış nesneler occlusion ile atılır; doku streaming bütçesi içinde. |
| 20 | GPU/CPU frame-budget profiler | Profiler/overlay: 16.6 ms bütçesi; Ultra ve Orta için ayrı ölç, bütçe tablosunu `Docs/PERFORMANS.md`'ye yaz. |

Bu listenin her maddesi için **önce/sonra** F12 ekran görüntüsü kaydet (`Logs/benchmark/`) ve PR'a ekle.

## Önce bağlanacak ücretsiz varlıklar
1. **ambientCG** — çamur (Ground0xx/Mud), kaya (Rock0xx), çim (Grass0xx) PBR setleri → `TerrainLayer` (albedo + normal + MRAO/mask). `TerrainTextureFactory.TryGetOverrideLayer` kancasını kullan (Content/ContentOverrides). Önce çamur + kaya + çim.
2. **Poly Haven** — gündüz HDRI (kapalı/yarı bulutlu) → `SkyEnvironment`; kaya ve ağaç gövdesi için tarama dokuları.
3. **Quixel Megascans** (ücretsiz katalog) — çam/meşe ağaç, çalı, kaya, çim demetleri → `VegetationTuning`/`ContentOverrides.TryGetVegetation` (LOD'lu prefab; billboard + rüzgâr maskesi).
4. **Mixamo** — tüfek tutuşu, yürüyüş, nöbet bekleme, şarjör değiştirme animasyonları → `MixamoImportHelper` + asker humanoid override (nöbetçi asker ve viewmodel el hareketi).
5. **Sonniss GDC** (ücretsiz) — silah atışı (yakın/uzak/vadi yankısı katmanları), şarjör, adım sesleri (çamur/çim/beton/metal) → ses katmanı (`Audio/*`, Acoustics).
Her varlık için `Assets/ThirdParty/README.md` ve `Resources/Credits.json` lisans kaydı (CC0/CC-BY; NC/ND yok).

## Çalışma sırası
1. Sahne açılışını ve Vitrin akışını doğrula (hatasız Console).
2. Kodsuz kazanımlar: ambientCG/Poly Haven/Megascans bağla → F12 karşılaştır.
3. Madde 3, 4, 7, 17 (en görünür, ucuz) → 5, 6, 2 → 11, 12, 13, 14 → 15, 16 → 18, 19, 20.
4. Her madde: kademe başına açık/kapalı + maliyet ms'si not edilir.
5. Sahibi sahneyi izler; "gerçekten çok iyi" derse özellikler harita bazında bir bir yayılır (önce Kuzgun Vadisi Kuzgun Köyü dikey dilimi). **Aksi halde büyük haritaya geçilmez.**

## Bilinen belirsizlikler (Cursor kontrol etsin)
- G4 özellikleri `AaaBenchmarkFeatureInstaller` ile yansımayla açılıyor; bir özelliğin Install kancası farklı adlandırılmışsa konsoldaki "atlandı" listesinden bul ve ad parçasını `FeatureNameFragments`'a ekle ya da doğrudan çağır.
- Hafif sis: `Atmosphere` sis yoğunluğunu yeniden yazarsa `SetupAtmosphere` değeri ezilir.
- Vitrin plan 1/6: `PlayerWeaponHandler.Tick` oyuncunun kendi Tick'iyle birlikte (dt=0) çağrılıyor; çift tetik/şarjör davranışını doğrula.
- Ev kapısı yönü: cephe 0 = yerel +Z varsayıldı (kapı oyuncuya bakar); yanlışsa `AaaBenchmarkBootstrap.SetupProps` içinde `_houseYaw` ayarla.
