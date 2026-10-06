# CANLI VARLIK KAYNAKLARI (gerçekçi silah / karakter / animasyon)

Tarih: 2026-10-06. Amaç: hesap/anahtar GEREKTİRMEDEN curl ile indirilebilen, ticari kullanıma uygun kaynaklar.
Gerçekçilik: 1 (stilize) - 5 (fotogerçekçi). Etiket: **[AJAN-OK]** = GİRİŞSİZ + TİCARİ-GÜVENLİ (ajan indirebilir). **[AJAN-HAYIR]** = login / ticari değil / belirsiz.
Not: Aşağıdaki lisans cümleleri sayfalardan alınmıştır; doğrulama tarihi bugündür. "DOĞRULANMADI" yazanlar indirme öncesi tekrar kontrol edilmeli. Aynı kaynaklardan indirirken lisans metnini Assets/ThirdParty/README satırına yazın (CC-BY için zorunlu).

## 1. Ateşli silahlar

| Kaynak | İndirme URL kalıbı | Lisans (anahtar cümle) | Gerçekçilik | Format / rig | Etiket |
|---|---|---|---|---|---|
| Quaternius Ultimate Gun Pack (50+ silah) | https://quaternius.itch.io/50-lowpoly-guns -> "Download Now" -> "No thanks, just take me to the downloads" (login yok, name-your-price) ; OGA kopyası: https://opengameart.org/content/low-poly-guns-pack | "Free for everyone to use in any project, even commercially!" (CC0) | 2 (stilize low-poly, AK/pompalı/keskin nişancı/tabanca var) | FBX/OBJ/Blend, rig yok (şarjör ayrı olmayabilir) | **[AJAN-OK]** (itch indirme sayfası tarayıcı/oturumsuz token ister; sorun olursa OGA kopyası) |
| Kenney Blaster Kit | https://www.kenney.nl/assets/blaster-kit (curl 403 verdi, tarayıcı UA ile dene) ; https://kenney-assets.itch.io/blaster-kit | "CC0 1.0 Universal ... use in any project including commercial ones" | 1 (oyuncak/bilimkurgu; kullanışsız) | FBX/OBJ/GLB | Ticari OK, görsel değeri düşük |
| OGA "Low-Poly M4A1" | https://opengameart.org/sites/default/files/m4a1.zip (HTTP 200 doğrulandı) | CC0 - "you can use this for any purpose" | 2 (PSX tarzı, 167 üçgen, el boyaması 1k) | .blend + FBX + PNG, şarjör ayrı | **[AJAN-OK]** (yer tutucu) |
| OGA "AK-47 High-poly" (Lamoot) | https://opengameart.org/content/ak-47 -> dosya linkleri (blend/obj) | CC0 | 3 (yüksek poligon, dokusuz olabilir) | .blend/.obj, rig yok | **[AJAN-OK]**, doku DOĞRULANMADI |
| OGA "Various Small Arms Collection" (Tabasco) | https://opengameart.org/ içinde arama "small arms collection" | CC0 ve CC-BY 3.0 karışık (tek tek kontrol et) | 2-3 | .blend/.obj | Dosya başına lisans kontrolü gerek |
| OGA "AK-47 Low Poly" (Vinrax) | https://opengameart.org/content/ak-47-low-poly (Ak47.zip 154 KB) | CC-BY 3.0 (atıf zorunlu); "no textures" | 2 | zip, dokusuz | Ticari OK + atıf; düşük değer |
| OGA "3D Weapons under CC0" (treesclimber) | https://opengameart.org/content/3d-weapons-under-cc0 | CC0 | 2-4 karışık (80+ silah, tüfek/pompalı/sniper dahil) | .blend vb. | **[AJAN-OK]** |
| Smithsonian 3D Open Access | https://3d.si.edu/collections/openaccesshighlights ; "Download" bağlantısı GLB (CC0 işaretli nesneler) | "download, transform, and share ... for any purpose, for free" (CC0) | 4-5 (tarama, fotogrametri) ama sadece müze nesneleri; modern tüfek yok, tarihi silah/miğfer olabilir | GLB/OBJ, rig yok, yüksek poligon (retopo gerekir) | **[AJAN-OK]** ama içerik sınırlı |
| NASA 3D Resources | https://nasa3d.arc.nasa.gov/models | Çoğu kamu malı; sayfa başına kontrol | 3-4 | araçlar/uzay; silah yok | Alakasız (helikopter/araç için bakılabilir) |
| Sketchfab (CC0 / CC-BY, ör. "Firearms Kit 1.0 (CC0)", "SF Operator (CC0)") | https://sketchfab.com/3d-models/... | Model başına CC0/CC-BY | 3-5 | glTF/FBX | **[AJAN-HAYIR]: indirme için LOGIN gerekir (API token + hesap).** Kullanıcı elle indirirse en yüksek kalite kaynağı |
| CGTrader / TurboSquid ücretsiz bölümleri | cgtrader.com/free-3d-models/weapon | Çoğu "Royalty Free License" ama çoğu ücretsiz modelde editorial/no-AI sınırı | 4-5 | FBX/OBJ | **[AJAN-HAYIR]** login şart |
| BlendSwap | blendswap.com | CC0/CC-BY/CC-BY-SA karışık | 2-4 | .blend | **[AJAN-HAYIR]** indirme için login |
| Hum3D free, itch.io ücretsiz paketler | hum3d.com / itch.io | Genelde kısıtlı veya login/çerez | 3-5 | karışık | **[AJAN-HAYIR]** / dosya başına kontrol |
| KayKit / Quaternius genel | kaylousberg.itch.io ; quaternius.com | CC0 | 1-2 stilize | FBX/GLB | Ticari OK, stil uymaz |
| Unity Asset Store FPS paketleri | assetstore.unity.com | Asset Store EULA | 4-5 (ücretli/free) | prefab | **[AJAN-HAYIR]** Unity hesabı |

Bulgu: Hesapsız + ticari-güvenli + gerçekten gerçekçi (4-5) modern tüfek YOK. Gerçekçi görünüm için seçenekler: (a) mevcut prosedürel silahı GunLit shader + Quaternius/OGA mesh referansıyla zenginleştirmek, (b) kullanıcının Sketchfab CC0/CC-BY indirmelerini elle ThirdParty/Models içine koyması, (c) ücretli paket (Asset Store / CGTrader) satın alımı.

## 2. İnsan / asker modelleri

| Kaynak | İndirme | Lisans | Gerçekçilik | Format / rig | Etiket |
|---|---|---|---|---|---|
| Renderpeople ücretsiz (Rigged / Posed / Animated) | https://renderpeople.com/free-3d-people/ (sayfadan model seç; indirme tarayıcıda, mobilde yok) | "all free models are subject to the same terms of use as the charged models in the shop, so they may be used for commercial purposes" ; sayfa: "Free Download, Free Commercial Use, No Registration Required" | 5 (fotogerçek tarama; ama sivil kıyafet, asker değil) | FBX/OBJ/GLB, Rigged sürümlerde iskelet; Mixamo uyumlu | **[AJAN-OK]*** (kayıt gerekmez ama indirme linki JS ile üretilir; curl çalışmazsa tarayıcı otomasyonu). Ticari sınır: model yeniden dağıtım (ham dosya paylaşımı) yasak olabilir, oyun içinde gömmek serbest - EULA'yı oku |
| MakeHuman 1.2 | https://github.com/makehumancommunity/makehuman (kaynak AGPL) ; makehumancommunity.org indirme; asset'ler: http://www.makehumancommunity.org/assets.html | "Models exported from an official version are released under CC0"; "Targets, proxies and the base mesh are considered graphical assets, covered by CC0" (2020 değişikliği). Program kodu AGPL (oyuna gömülmez, sadece araç olarak kullanılır) | 3 (anatomi iyi, doku orta; kıyafet/üniforma için ekstra asset gerekir) | Çıktı FBX/DAE/OBJ, game-engine iskeleti (Unity uyumlu), LOD'lu | **[AJAN-OK]** ama GUI aracı; headless betikle üretmek zor. Askeri kıyafet = Blender'da elle/ücretsiz CC0 üniforma asset ile. Orta öncelik |
| Mixamo | mixamo.com | Adobe Terms: ticari OK | 3-4 karakter, 4 anim | FBX, otomatik rig | **[AJAN-HAYIR]: Adobe ID LOGIN gerekir** |
| Ready Player Me | readyplayer.me | Hizmet 31 Ocak 2026'da kapandı (Netflix satın aldı) | - | - | **[AJAN-HAYIR]: KULLANILAMAZ** |
| KayKit / Quaternius karakterleri | itch.io / quaternius.com | CC0 | 1-2 stilize | FBX, rigli | Ticari OK, gerçekçi değil |
| Sketchfab "SF Operator (CC0)" vb. asker | sketchfab.com | CC0 | 4 | glTF | **[AJAN-HAYIR]** login |
| Unity "Synty/ Asset Store" askerler | assetstore | EULA | 3-5 | prefab | **[AJAN-HAYIR]** hesap |

Karakter için en gerçekçi hesapsız-ticari yol: Renderpeople rigged ücretsiz örnekleri (insan oranı/doku kalibrasyonu) + MakeHuman CC0 taban mesh'e üniforma/teçhizat (miğfer, yelek: Poly Haven/Smithsonian/OGA CC0 parçalar) giydirmek. Mevcut SoldierModel + GX3 karakter shader'ı (Fabric/Skin/Gear) bu mesh'lere uygulanabilir.

## 3. Animasyon kütüphaneleri

| Kaynak | İndirme | Lisans | Gerçekçilik | Format | Etiket |
|---|---|---|---|---|---|
| CMU Mocap (2.548 hareket: yürü/koş/zıpla/eğil/tırman/dövüş/silah yok) | Orijinal ASF/AMC: http://mocap.cs.cmu.edu (SSL sertifikası hatalı, curl -k veya http); BVH dönüşümleri: cgspeed "CMU BVH" ; GitHub aynası https://github.com/Shriinivas/cmubvh ; FBX dönüşümü 4TU (şu an bakımda, DOI doğrulanamadı) | CMU: "free for all uses" (orijinal sitedeki ifade; ticari dahil, kayıt yok). cgspeed dönüşümü de "Free for research and commercial use worldwide" | 4 (gerçek mocap, ancak 2000'ler kalitesi, gürültülü) | BVH / AMC | **[AJAN-OK]** (lisans metni sitede curl ile doğrulanamadı: sertifika; indirmeden önce bir kez tarayıcıdan oku) |
| Bandai Namco Motiondataset | https://github.com/BandaiNamcoResearchInc/Bandai-Namco-Research-Motiondataset (dataset/ klasörü) | **CC BY-NC 4.0**; ticari kullanım YOK | 4 | BVH | **[AJAN-HAYIR]** ticari değil |
| Ubisoft LaFAN1 | https://github.com/ubisoft/ubisoft-laforge-animation-dataset (lafan1.zip 137 MB) | **CC BY-NC-ND 4.0** | 4-5 | BVH | **[AJAN-HAYIR]** ticari değil + türev yasak |
| 100STYLE (Ian Carter) | ianwcarter.com/100style 404 verdi; veri seti Zenodo/Kaggle'da, lisans DOĞRULANMADI (CC BY 4.0 olduğu söyleniyor, kontrol et) | DOĞRULANMADI | 4 (stil yürüyüşleri, silah yok) | BVH | Belirsiz, kullanma |
| Mixamo | mixamo.com | Ticari OK | 4 | FBX | **[AJAN-HAYIR]** Adobe login |
| Quaternius / KayKit animasyon paketleri | itch.io | CC0 | 2 | FBX/GLB | Ticari OK, stilize |
| Renderpeople "Animated" ücretsiz örnekler | bkz. bölüm 2 | aynı EULA | 5 | FBX | tek tek klip |

Silahlı FPS hareketi (nişan, yeniden doldurma, sekme) için hesapsız ticari-güvenli mocap kütüphanesi yok. Seçenekler: prosedürel (zaten var: ProceduralAnimator, WeaponHandling), CMU yürüyüş/koşu/eğilme/tırmanma temeli üstüne Animation Rigging ile silah tutuşu (TwoBoneIK).

### BVH -> Unity Humanoid retarget
1. Blender (ücretsiz): BVH içe aktar, "Rokoko Studio Live" veya ücretsiz "Auto-Rig Pro (ücretli)" yerine **Blender BVH Retargeter** / elle "Copy Rotation" kısıtlamaları ile Mixamo-adlı iskelete aktar; FBX dışa aktar (Apply Transform, -Z forward, Y up, ölçek 0.01 CMU için).
2. Unity: FBX içe aktarımda Rig > Animation Type = Humanoid, Avatar "Create From This Model"; gerekirse Configure'da kemik eşlemesini düzelt. Klip döngüsü için Loop Time + Loop Pose; Root Transform Rotation/Y/XZ "Bake Into Pose" işaretle.
3. Toplu işleme: Editor betiği (AssetPostprocessor) ile ModelImporter.animationType = Humanoid ayarla.
4. CMU ölçeği: BVH birimleri inç/cm karışık; hız kontrolünü 1.0 m/sn yürüyüş ile doğrula.

## 4. Unity resmi paketler ve demo içerikleri

| Paket / içerik | ID, sürüm | Notlar | Etiket |
|---|---|---|---|
| glTFast | com.unity.cloud.gltfast, 6.x serisi (docs'ta 6.9'a kadar sürüm sayfası doğrulandı; kurulum: Package Manager > Add by name `com.unity.cloud.gltfast`) | Unity 6, URP uyumlu (docs: "Universal, High Definition and the Built-In Render Pipelines"). GLB/glTF (Smithsonian, Renderpeople GLB) için gerekli | **[AJAN-OK]** manifest.json'a eklenir (registry.unity.com, hesapsız) |
| Animation Rigging | com.unity.animation.rigging, 1.3.x/1.4.x (kesin sürüm DOĞRULANMADI; Unity 6 ile gelen "verified" sürümü Package Manager'dan seç) | TwoBoneIK, MultiAim: silah tutuşu, sol el IK, bakış | **[AJAN-OK]** |
| Visual Effect Graph | com.unity.visualeffectgraph; sürüm editör sürümüne bağlı (Unity 6 = 17.x, 6000.6 için tam sayı DOĞRULANMADI; Package Manager varsayılanı) | Namlu alevi, toz, kıvılcım; URP destekli. Mevcut CPU parçacık sistemi yeterliyse isteğe bağlı | **[AJAN-OK]** |
| Book of the Dead: Environment (Unity) | Asset Store (login) ; forum: "can be used in commercial projects ... Unity Asset Store EULA, including content provided by Quixel" | Ticari kullanım OK ama Asset Store LOGIN gerektirir; HDRP odaklı, URP'ye taşımak ağır | **[AJAN-HAYIR]** |
| Viking Village / diğer Unity demoları | Asset Store EULA / Unity demo koşulları | Çoğu Unity hesabı + bazıları yalnız Unity ile kullanım şartlı; DOĞRULANMADI | **[AJAN-HAYIR]** |

## İndirme planı (görsel etkiye göre ilk 10)

| # | Ne | Nereden | Lisans | Etki | Not |
|---|---|---|---|---|---|
| 1 | glTFast paketi | manifest: com.unity.cloud.gltfast | Unity paket | Altyapı: GLB model (Smithsonian, Renderpeople) için | Önce bu |
| 2 | Animation Rigging paketi | manifest: com.unity.animation.rigging | Unity paket | Silah tutuşu/IK = en büyük "canlı" farkı | Mevcut ProceduralAnimator ile birleştir |
| 3 | Renderpeople ücretsiz Rigged 2-3 model | renderpeople.com/free-3d-people | Ticari OK, kayıtsız | Fotogerçek insan oranı + doku referansı | Tarayıcı otomasyonu gerekebilir; EULA'yı ThirdParty README'ye yaz |
| 4 | CMU mocap seçili BVH (yürü, koş, eğil, zıpla, tırman) | mocap.cs.cmu.edu / Shriinivas/cmubvh | "free for all uses" | Doğal gövde hareketi | Blender ile Humanoid'e retarget (yukarıdaki adımlar) |
| 5 | Quaternius Ultimate Gun Pack | quaternius.itch.io/50-lowpoly-guns | CC0 | Silah geometri tabanı (tüfek/sniper/pompalı/tabanca) | Stilize; GunLit shader ile gerçekçileştir |
| 6 | OGA "3D Weapons under CC0" (tüfek/sniper/pompalı alt kümesi) | opengameart.org/content/3d-weapons-under-cc0 | CC0 | Daha detaylı alternatifler | Parça parça lisans doğrula |
| 7 | OGA AK-47 High-poly (Lamoot) | opengameart.org/content/ak-47 | CC0 | MPT-76 benzeri tüfek siluet referansı | Dokusu yoksa GunLit ile boya |
| 8 | MakeHuman 1.2 taban asker | makehumancommunity.org + CC0 asset'ler | CC0 çıktı | Üniforma giydirilebilir, kendi markamız | Araç AGPL, yalnızca çıktı kullan |
| 9 | Smithsonian CC0 GLB (miğfer, askeri eşyalar, varsa) | 3d.si.edu/collections/openaccesshighlights | CC0 | Gerçek tarama detayı, prop | İçerik sınırlı, retopo gerekir |
| 10 | VFX Graph paketi | manifest: com.unity.visualeffectgraph | Unity paket | Namlu alevi, toz, patlama kalitesi | Düşük kademede kapat |

Kullanıcıdan gereken (ajan yapamaz): Sketchfab CC0/CC-BY gerçekçi tüfek/asker modelleri (login), Mixamo animasyonları (Adobe login), Asset Store paketleri. En yüksek gerçekçilik sıçraması için bunlar önerilir: Sketchfab'dan 3-4 CC0 tüfek + Mixamo "rifle" animasyon seti, Assets/ThirdParty/Models ve Animations altına bırakılırsa ThirdPartyBinder hazır.
