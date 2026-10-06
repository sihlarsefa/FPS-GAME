# Ses Kaynak Listesi (V3-ses-kaynaklari)

Durum: 2026-10-06. İndirilen/işlenen her şey CC0 (OpenGameArt sayfasında lisans rozeti doğrulandı). Kayıt: `Assets/ThirdParty/README.md` "Ses" bölümü.

## Ne bağlandı
- `Resources/Audio/Weapons/_class/<kalibre>/`: gerçek atış (`bang`, `distant`, `tail_outdoor`), şarjör/sürgü/kurma/boş tetik/selektör foley. Kalibre eşlemesi: pistol9 = Walther PPQ + 1911, smg9 = Carl Gustav M45 + PPSh, rifle556 = AR-15 + Savage .300 BLK, rifle762 = AK-47 + SKS, dmr762 = SKS + Savage, sniper762 = Mosin Nagant + Tikka .30-06, lmg762 = AK-47, shotgun12 = Nova + Model 12 + Charles Daly.
- `Resources/Audio/Ambience/<dagcam|yayla|kar|kiyi>/`: rüzgâr, böcek (cırcır), yağmur (dış/çatı), kuş, uzak silah/patlama.
- `Resources/Audio/SFX/Explosion/explosion_1.wav` -> menü `HAREKÂT/İçerik/Gerçek Ses Kaynaklarını Bağla` (veya batch `-executeMethod Project.EditorTools.AudioSourceBinder.BindBatch`) ContentOverrides'ta `SoundId.Explosion` yuvasına bağlar. Yeni .wav'lar için Unity bir kez içe aktarmalı (.meta üretimi).
- İçe aktarma kuralı: `AudioImportRules` (Weapons ADPCM mono, Ambience Vorbis akış, SFX Vorbis mono).

## Eksik (CC0 bulunamadı; kulakla/sahada denenmesi gereken)
1. Mermi vızıltısı / çatlama (BulletWhiz, BulletCrack), 2. kovan düşmesi beton/toprak (ShellCasing), 3. şeritli MG (MG3) sürgü/kemer, 4. gerçek sürgü (JNG-90 `bolt`), 5. baykuş/köpek/horoz/koyun çanı/martı, 6. gece orman döngüsü, gök gürültüsü, 7. bastırılmış atış (`supp`), 8. kapalı alan kuyruğu (`tail_indoor`) ve vadi (`tail_valley`).
Not: Orta mesafe kayıtları doğal yansıma içerir; vadi yankısı için `tail_valley` yine prosedürel/oyun içi gecikme kullanır.

## Sahibi için adımlar (girişsiz alınamayan / ücretli-kontrollü kaynaklar)
1. **Sonniss GDC Game Audio Bundle** (royalty-free, ticari kullanım serbest, atıf gerekmez): https://sonniss.com/gameaudiogdc — her yıl paket çok GB'tır; kayıt gerektirir. Lisans metni paket içindeki `License.pdf` dosyasındadır; kullanmadan önce okuyun. Yalnız gereken klasörleri (Weapons, Explosions, Foley) çıkarın, yukarıdaki isimlendirmeyle `Resources/Audio/Weapons/<weaponId>/<katman>_<n>.wav` altına koyun (silaha özel klasör sınıf klasörünün önüne geçer).
2. **Freesound (yalnız CC0 filtresi)**: https://freesound.org/search/?f=license%3A%22Creative+Commons+0%22 — arama: "bullet whiz", "bullet crack", "bullet flyby", "shell casing concrete", "bolt action rifle", "owl", "rooster", "dog bark distant". İndirmek için ücretsiz hesap gerekir. Her dosyayı README tablosuna işleyin (yazar + URL).
3. **Boom Library / Pro Sound Collection** ücretli; satın alınırsa lisans "sınırsız oyun içi kullanım" olmalı.
4. İşleme betiği: 44.1 kHz mono WAV, başı 3 ms kırp, ses düzeyi: atış RMS -10..-15 dBFS (ilk 150 ms), tepe <= -1 dBFS; foley tepe -3 dBFS; döngü ortamı RMS -26..-30 dBFS ve 1.5 sn çapraz geçişli döngü.
