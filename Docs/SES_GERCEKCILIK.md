
## A1-balistik-akustik

Konum: `Infrastructure/Audio/Acoustics/` (`AcousticsMath.cs` saf, `AcousticsQueue.cs` saf kuyruk + ışın bütçesi, `Acoustics.cs` giriş noktası + `AcousticsHost`). Testler: `Tests/EditMode/AcousticsMathTests.cs`.

Davranış:
- **Ses hızı gecikmesi:** 343 m/s. Mermi, namlu raporundan önce gelir (mermi hızı sınıfa göre 340-900 m/s).
- **Crack / whiz:** `Acoustics.OnBulletPassed(rayOrigin, rayDir, maxDist, caliber)` ışının dinleyiciye en yakın noktasını hesaplar; < 6 m ise whiz, süpersonik sınıfta (tabanca hariç) ek olarak `BulletCrack`. Ses atıcıdan değil mermi yolu üzerindeki en yakın noktadan çalınır; mermi varış süresi kadar gecikir. Yerel oyuncunun kendi mermisi (<2 m) atlanır.
- **Hava sönümü:** çap sınıfı (tabanca/tüfek/MG/keskin nişancı/patlama) başına seviye eğrisi ve alçak geçiren kesim (mesafeyle 22 kHz -> sınıf tabanı). Susturucu: menzil x0.35, seviye x0.3.
- **Vadi yankısı:** dinleyiciden atış yönü ve +-35/+-70 derece 5 ışın (40-400 m çarpma), yol farkından gecikme (0.08-2.5 sn), en fazla 3 yankı, her biri daha sönük + filtreli (`ShotTailValley` > 0.35 sn, aksi halde `ShotDistantMid`). 0.35 sn bekleme, saniyede 40 ışın bütçesi.
- **Kapalı alan slapback:** atıcı konumunda tavan (<8 m) + 4 yatay duvar ışını; kapalıysa en yakın duvara göre 20-120 ms gecikmeli `ShotTailIndoor`.
- Kendi 10 kanallı havuzu (`[Acoustics]`, AudioLowPassFilter) vardır; klip `GameAudio.GetClip` ile alınır, yoksa o katman atlanır (ana atış GameAudio'da prosedürel yedekle kalır). Sunucuda (`GameAudio.Enabled` false) no-op.

Cursor'un Unity'de doğrulaması/bağlaması gerekenler:
1. ENTEGRASYON kancalarını bağla (aşağıda); GameAudio'daki mevcut gecikme çift duyulmasın diye `OnShot(..., playMainReport: false)` bırakıldı (ana rapor GameAudio'da).
2. Kuzgun Vadisi'nde uzak atışta 1-3 gecikmeli yankı duyuluyor mu; arazi/bina collider'ları var mı (Physics.DefaultRaycastLayers).
3. Mermi geçişinde crack yönü mermi yolundan geliyor mu; ışın bütçesi profilleri (Profiler).
4. `BulletCrack`, `ShotTailValley`, `ShotTailIndoor` klipleri (C11 üretiyor/override) mevcut mu.
5. Kapalı alanda slapback (bina içi atış) ve tabanca/susturucu seviyeleri kulakla ayarlanmalı (`AcousticsMath.Profile`).

## A2-silah-foley

**Ne yapar.** Her silahın ses profili ve çok adımlı silah/teçhizat foley'i. Hazır kayıt yoksa prosedürel yedek (`FoleySynth`) devreye girer; hiçbir koşulda sessizlik/istisna yok.

- `Application/Catalogs/WeaponSoundProfile.cs`: `WeaponSoundProfiles.Get(weaponId)`; katalogdaki 16 silahın hepsi için açık kayıt (kalibre sınıfı `WeaponCaliber`, mekanik/çekirdek/kuyruk/bastırılmış set adı, alçak frekans vuruş Hz + şiddet, `TailCutSeconds(fireInterval)` atış hızına duyarlı kuyruk kesimi, kovan türü, kovan atışta mı düşer (JNG-90 ve Escort sürgü/pompada)).
- `Infrastructure/Audio/Foley/`: `WeaponFoley.Play(FoleyStep, weaponId, position, isLocal)`; adımlar: şarjör bırak, çıkar, kese, tak, tokat, sürgü, şarjör kolu, kovan sürme, seçici tıkı, boş tetik, silah çekme/kılıfa koyma, ADS hışırtı, koşu teçhizat tıkırtısı, plaka taşıyıcı, yüzüstü yatma, iniş (gövde).
- `WeaponFoley.BeginReload(weaponId, süre, boşMu, kaynakTransform, isLocal)`: `ReloadFoleyPlanner` kategoriye göre adım planı (tabanca/tüfek/LMG kapak/nişancı sürgü/pompalı mermi mermi) çıkarır; taktik doldurmada sürgü yok, boşta var. `CancelReload(transform)` ile iptal.
- `GearFoleyEmitter.Attach(go, isLocal, weaponIdProvider)`: konum farkından hız, koşu tıkırtısı (adım mesafesine bağlı), koşuya başlarken plaka sesi, iniş (düşme hızına göre). Ses kapalıysa/dinleyici uzaksa çalmaz. Kendi 14 sesli havuzu var (GameAudio havuzundan bağımsız).

**Klip adlandırma (Cursor/Sonniss).** `Assets/_Project/Resources/Audio/Weapons/<weaponId>/<katman>_<n>.wav`; ör. `ar_mpt55/bang_1.wav`. Yedek: `Resources/Audio/Weapons/_class/<kalibre>/<katman>_<n>.wav` (kalibre klasörleri: pistol9, smg9, rifle556, rifle762, dmr762, sniper762, lmg762, shotgun12). Hiçbiri yoksa prosedürel. Aynı katmandan birden fazla `_n` varsa rastgele (art arda aynısı seçilmez).
Atış katmanları: `bang, mech, thump, tail_outdoor, tail_indoor, tail_valley, distant, supp, shell`. Foley katmanları: `magrelease, magout, pouch, magin, slap, bolt, chargehandle, shellinsert, selector, dryfire, equip, holster, ads, sprint, plate, prone, land`. Mono veya stereo, 44.1/48 kHz; Load Type Decompress On Load (kısa klipler) önerilir.

**Cursor Unity'de doğrulayacak/bağlayacak.** (1) Play Mode'da bir silahla boş/dolu doldurma: adımlar sürede sırayla duyulmalı; sürgü yalnız boş doldurmada. (2) Koşu/iniş/yüzüstü sesleri ve seviye dengesi (`FoleyStepInfo` hacimleri). (3) Sonniss kaydı bırakıp klasör adının keşfedildiğini doğrulama. (4) Entegrasyon kancaları (aşağıda, DURUM/ENTEGRASYON listesi): bot/oyuncu doldurma ve çekme çağrıları. (5) C11 katmanlı atışına `TryGetFireClip` bağlantısı. Testler: `WeaponFoleyTests` (Editor'de koşar).

## A5-ortam-hdr-miks

Kod: `Infrastructure/Audio/Mix/*` (ad alanı `Project.Infrastructure.Audio.HdrMix`; `Mix` adı SynthDsp.Mix ile çakıştığı için ad alanı HdrMix), `Infrastructure/Audio/Ambience/*`. Test: `Tests/EditMode/AudioHdrMixTests.cs` (18 test, `#if UNITY_EDITOR`).

**HDR miks (Frostbite tarzı).** `HdrWindow`: çalışan bir dB penceresi (yükseklik 40 dB, boşta tepe -30 dB). Yüksek olay (patlama, kendi makineli tüfeğimiz) tepeyi anında yükseltir, 0.45 sn tutar, sonra 9 dB/sn gevşer; eş zamanlı olaylar enerji olarak toplanır (+3 dB), tepe +6 dB'de sınırlı. Pencerenin altında kalan kaynaklar (uzak ayak sesi, ortam) kırpılmak yerine `PushRatio` (0.75) ile aşağı itilir (en çok -30 dB). Tepe -3 dB'yi aşınca ana kazanç hafif kısılır (`HeadroomLinear`). `AudioMix.HdrGain(float sourceDb)` / `HdrGainFor(kind, pos)` ile sessiz sesler bu kazançla çarpılabilir.

**Miks anlık görüntüleri (AudioMixer varlığı gerekmez).** `MixSnapshotState`: Tinnitus, Underwater, Indoor, AdsFocus, Downed, Dead. Birleşim kuralı: kesim en küçük, kazançlar çarpım. Kulak çınlaması: yakın patlamada (etki yarıçapının 1.6 katı içinde) 3 ile 6 sn, 3.8 kHz çınlama + dünya 650 Hz alçak geçiren + ortam kısma; ilk %55 tam, sonra yumuşak sönüm; kulaklık koruması çarpanı (`earProtected`). Uygulama: dinleyiciye `AudioLowPassFilter` eklenir, `AudioListener.volume = GameAudio.MasterVolume * miks * headroom` (her karede). Çınlama kaynağı `bypassListenerEffects` ile filtreden etkilenmez. Opsiyonel mixer: `Resources/Audio/MainMixer` bulunursa ve şu açık parametreler tanımlıysa onu sürer: `WorldLowpass` (Hz), `MasterGainDb`, `AmbienceGainDb`, `ReverbSendDb`; tanımlı değilse otomatik dinleyici yoluna döner.

**Ortam yatakları.** `AmbienceBeds.Configure(mapId, TimeOfDay, WeatherKind, nearVillage)`; biyom: kuzgun=DagCam (çam ormanı, dağ), ayaz=Kar, liman=Kiyi, kartal=Yayla. Döngüler: rüzgâr esintileri, çam hışırtısı, böcek/cırcır (gece), yağmur (dışarı), çatı yağmuru (içeride), kar rüzgârı, dalga. Tek seferlik (Poisson, tür başına en az 5 sn): kuş (gündüz, şafakta koro), baykuş (gece), köpek havlaması/uluması, horoz (şafak, köy), balta, koyun çanı, martı. Köy yaşamı `nearVillage` bayrağıyla (ezan yok, genel uzak köy). İç mekânda döngüler kısılır, çatı yağmuru açılır, tek seferlik olaylar 0.3x.

**Uzak çatışma.** `DistantBattleScheduler`: 140 ile 1500 m arasındaki gerçek atış/patlama olayları ses hızı gecikmesiyle (mesafe/343) zamanlanır, mesafeyle kısılır ve alçak geçirilir; otomatik atış 0.11 sn aralıkla seyreltilir, kuyruk 24 ile sınırlı. Son 18 sn gerçek olay yoksa sentetik atış dizileri ve patlamalar üretilir (`SetSyntheticDistantBattle(false)` ile kapanır). Yön korunur.

**Yedek/override.** Klip arama: `Resources/Audio/Ambience/<dagcam|kar|kiyi|yayla>/` altında `loop_windgust`, `loop_forestrustle`, `loop_insects`, `loop_rainoutdoor`, `loop_rainroof`, `loop_snowwind`, `loop_waves`; `shot_bird`, `shot_owl`, `shot_dogbark`, `shot_doghowl`, `shot_rooster`, `shot_axechop`, `shot_sheepbell`, `shot_gull`, `shot_distantgun`, `shot_distantblast` (isteğe bağlı `_1`..`_6` varyantları). Yoksa `AmbienceSynth` (prosedürel) üretir; uzak çatışmada GameAudio'nun `ShotDistantMid/Far/Explosion` klipleri kullanılır.

**Cursor Unity'de doğrulamalı / bağlamalı:**
1. Maç başında `AmbienceBeds.Configure(mapId, time, weather, nearVillage)` ve `AudioMix.Bind(eventBus, localPlayerId)` çağrısı (aşağıdaki ENTEGRASYON listesi).
2. Çınlama: bir bombayı 3 m yakına at, 3 ile 6 sn çınlama + boğuk dünya duyulmalı, sonra açılmalı; menü/UI sesi boğuklaşıyorsa kabul (dinleyici filtresi) ya da MainMixer ile UI grubunu ayır.
3. Düzey: `LoopMaxVolume` 0.55 ve tek seferlik 0.6 çarpanı kulakla ayarlanmalı; mevcut `GameAudio.SetAmbience(Ambience/Wind)` ile rüzgâr çift olabilir, gerekirse eski ortam döngüsünü kapat.
4. Pencere değerleri (`PushRatio`, `ReleaseDbPerSecond`) makineli tüfek ateşinde ayak sesi/ortamın hissedilir geri çekilmesine göre ayarlanmalı.
5. Opsiyonel MainMixer asset'i ve açık parametreler (yukarıda); kurulursa mixer yolu ile dinleyici yolunun çift uygulanmadığını doğrula (`_mixerDrives`).
6. Gerçek klipler `Resources/Audio/Ambience/<biyom>/` altına konabilir, yüklenme ve döngü noktası kontrolü.

## A3-diyalog-v2

Konum: saf mantık `Application/Dialogue/` (Unity'siz; testler gerçekten koşar), Unity tarafı `Infrastructure/Audio/Dialogue/` (`DialogueDirector`, `DialoguePlayback`, `DialogueClipLibrary` + `RadioClipProcessor`). Replik listesi: `Design/Audio/telsiz_replikleri_v2.csv` (590 satır; `id,text,stress,category,voiceHint`), çalışma zamanı kopyası `Resources/Audio/dialogue_lines_v2.csv` (iki dosya aynı tutulur). Testler: `Tests/EditMode/DialogueV2Tests.cs` (36, koşuyor) + `DialogueV2DataTests.cs` (CSV doğrulama, `#if UNITY_EDITOR`).

Davranış:
- **Stres hâlleri (sakin/çatışma/panik):** `StressTracker` asker başına çatışma ısısı (alınan hasar +0.4, yakın düşman atışı +0.06, kendi atışı +0.03, patlama +0.5; 0.12/sn söner) + sağlıktan skor; histerezis ve alta geçişte 3 sn bekleme. Satır seçimi, ses kimliği (panikte perde/hız/şiddet artar) ve telsiz bozulması stresle değişir.
- **Bağırma / telsiz:** `ProximityRouter`: bağırma menzili sakin 14 m, çatışma 32 m, panik 48 m (kritik x1.25). Yakındaki asker bağırır (3D, filtresiz, yüksek; araya duvar girerse low-pass + kısma), uzaktaki 900 m'ye kadar telsizle konuşur (filtreli); 900 m ötesi sessiz. Yalnız-bağırma kategorileri (şarjör, el bombası) uzaktan duyulmaz.
- **Bağlamsal replikler** (her biri sakin/çatışma/panik varyantlı, `BarkMemory` ile kategori + asker bazlı tekrar önleme): "Şarjör!", "Yeniden dolduruyorum!", "El bombası!", "Yaralandım!", "Vuruldum!", "Düşman düştü!" (kafadan/çoklu varyantı), "Bana ört!", "İlerliyorum!", "Sis atıyorum!" + sıhhiye, adam düştü, baskı ateşi, kanat, geri çekil, temiz, dost ateşi, nişancı uyarısı vb. Olay kaynakları: WeaponReloadStarted, PlayerDamaged, PlayerDied, WeaponFired, Explosion, ItemUsed (grenade_frag/grenade_smoke/bandage), SquadOrderIssued, Revived + saniyede ~bir kez rastgele "ortam" repliği.
- **Tespit cümlesi (kompozisyon):** `PhraseComposer`: [açılış] + "Saat üç yönü" + "yüz metre" + "düşman piyade" + [kapanış]. Saat yönü yerel oyuncunun bakışına göre, mesafe 5/10/25/50/100 m'ye yuvarlanır, sayı klip parçalarına bölünür (350 -> `num_3`,`num_100`,`num_50`,`unit_metre`). Panikte kısa saat ("Saat üç"), mesafe bazen atlanır. Parçalar eksikse `contact_full_*` bütün satır klibi, o da yoksa prosedürel konuşma.
- **8 ses kimliği:** `VoiceIdentities` (pitch/formant/speed/intensity), `VoiceAssigner` takımda benzersiz ve oturum boyunca sabit atar (asker kimliği FNV özeti). Uygulama: yeniden örnekleme (perde x hız^0.35), iki tepe EQ ile formant rengi, şiddet doyumu.
- **Rütbe duyarlı yanıt:** `RankReplies.AckCategory`: Er/Onbaşı/Çavuş/Sözleşmeli Er -> "Emredersiniz komutanım"; astsubay -> "Anlaşıldı komutanım"; üst rütbe astına "Anlaşıldı, devam edin"; eşit rütbe "Anlaşıldı". F1-F4 emrinde en yakın 2 asker (0.15 sn ve 1.1 sn gecikmeli) yanıtlar; Attack emrinde 3. asker "İlerliyorum!" der.
- **Telsiz işleme (`RadioDsp`):** 300-3400 Hz bant geçiren (4. derece; sinyal kötüleşince üst kesim 2.7 kHz'e iner), sıkıştırıcı, yumuşak doyum (tanh), squelch açılış tıkı + kapanış kuyruğu, taşıyıcı tıslaması + zayıf 1.15 kHz ton, 550 m ötesinde (sinyal < 0.55) rastgele kopmalar (40-140 ms), konuşan çatışmadaysa arka plan çatışma sızıntısı (sesin altında uzak patlama atımları + gümbürtü). Eski `RadioVoicePlayer.PlayRadio` da artık aynı işlemciden geçer (okunamazsa eski filtre zinciri).
- **Öncelik + ducking (`DialogueArbiter`):** telsiz 1 hat, bağırma 2 hat. High/Critical replikler KESİLMEZ (araya girilemez); meşgulse sıraya (4, 3 sn ömür) girer. Yalnız Normal ve altı kesilebilir. Kategori beklemesi, asker aynı anda iki şey söylemez. Duck hedefi: Critical 0.35, High 0.55, Normal 0.8; `DialogueDirector.DuckGain` (saldırı 80 ms, bırakma 600 ms).
- **Yedek (altın kural):** klip yoksa `ProceduralVoice` metinden formant vızıltısı üretir (ritim/uzunluk/stres uyumlu, anlaşılmaz), aynı kimlik + telsiz zincirinden geçer. Kitap yoksa sistem sessiz kalır. Sunucuda (GameAudio kapalı) çalışmaz.
- **Eski sistemle ilişki:** `RadioChatterSystem` (v1) v2 aktifken temas, yara, şarjör, emir onayı, sıhhiye çağrısı ve öldürme onayını bırakır; topçu, çember, komuta, zafer, tim elendi v1'de kalır. v2 telsiz hattı doluyken v1 düşük/normal öncelikli satırı atlar.

Cursor'un Unity'de doğrulaması/bağlaması gerekenler:
1. **Klipler (TTS/oyuncu):** `Design/Audio/telsiz_replikleri_v2.csv` içindeki her `id` için `Assets/_Project/Resources/Audio/Voice/v2/<id>.wav` üret (22.05 kHz mono, tüm parçalar AYNI hızda; birleştirmede farklı hız parçaları reddedilir). İlk öncelik parça kategorileri (`part_open/part_clock/part_num/part_unit/part_target/part_tail`, ~110 kısa klip) ve `contact_full`; sonra bark kategorileri. Import ayarı: Load Type = Decompress On Load, sıkıştırma PCM/ADPCM (`GetData` gerekir). Klipsiz de oyun çalışır (prosedürel yedek), fakat parça klipleri olmadan "saat/mesafe" anlaşılır olmaz.
2. **CSV kopyası:** `Resources/Audio/dialogue_lines_v2.csv` Unity'de TextAsset olarak görünmeli; `Design/` dosyası değişirse kopyala.
3. **ENTEGRASYON kancaları** (yasaklı dosyalar C11'de): `GameAudio.cs`: ambiyans/müzik/uzak çatışma/silah sesi hacimlerine `* DialogueDirector.DuckGain` çarpanı (ses katmanları 0.35'e kadar kısılır; Critical konuşma varken). `Dialogue` kabuğu `Project.Infrastructure.Audio.Dialogue` ad alanında.
4. **Kulakla ayar:** bağırma menzilleri (`ProximityRouter.ShoutRange`), 3D `maxDistance = menzil x1.6`, kimlik tablosu (`VoiceIdentities.Table`), squelch/tıslama düzeyleri (`RadioDspSettings.For`), duck değerleri. Telsiz sesinin anlaşılırlığı (4. derece 300-3400 Hz) ve sızan çatışma gürültüsü seviyesi.
5. **Sahne testi (Kuzgun Vadisi):** 1) yakın bot şarjör değiştirince bağırıyor mu (3D, filtresiz), 100 m+ uzaktaki telsiz mi; 2) hasar alınca saat yönü/mesafe doğru mu (düşmanın bulunduğu yön yerel oyuncunun bakışına göre); 3) F1-F4 emrinde Er "Emredersiniz komutanım" diyor mu; 4) panik/sakin farkı (sağlık düşük + yoğun ateş); 5) bomba/vuruldum sırasında diğer sesler kısılıyor mu (kanca bağlanınca); 6) bir asker her seferinde aynı sesle mi konuşuyor; 7) profiler: replik başına GC (float[] tamponları) ve DSP süresi (kısa satır birkaç ms).
6. Altyazı: v2 satırları `RadioChatterSystem.LineSpoken` olayıyla gider (mevcut altyazı görünümü yeterli); `RadioChatterSystem.RaiseSpoken` eklendi.
7. İsteğe bağlı: bot yapay zekâsı `DialogueDirector.Say(combatant, DialogueCats.Advance)` / `SaySpotted(combatant, worldPos, targetId)` çağırabilir (şu an olay tabanlı + rastgele ortam repliği).

## A4-turkce-ses-uretimi

Davranış: Türkçe telsiz replikleri (`Design/Audio/telsiz_replikleri.csv` 225 satır + 10 `player_*` + A3 çekirdek barklar `core_*`: Şarjör!, El bombası!, Yaralandım!, Düşman düştü!, Bana ört!, İlerliyorum!, Sis atıyorum!, Emredersiniz komutanım, saat 1-12 yönü, 50/100/200/300/400 metre) 4 ses x 3 stres olarak üretildi. Çıktı: `Assets/_Project/Resources/Audio/Voice/v2/<ses>/<stres>/<id>.wav` (22.05 kHz mono 16-bit) + `Voice/v2/voice_manifest_v2.json` (süre, ölçülen LUFS, metin, lisans). Eski `Voice/*.wav` dosyaları dokunulmadı. Sesler: `dfki_er`, `dfki_kalin`, `dfki_genc` (Piper tr_TR-dfki-medium, perde/formant kaydırmalı), `yelda` (macOS). Stres: `sakin`, `catisma`, `panik`. Lisans: dfki CC BY-NC-SA (ticari yayında KULLANILAMAZ); ayrıntı `Docs/SES_TTS_NOTU.md`. `Voice/v2` dosya boyutu büyüktür (197 MB, 3120 klip); yayın derlemesinde yalnızca seçilen ses/stres klasörleri paketlenmeli.

Yeni kod: `Infrastructure/Audio/VoiceV2/VoiceV2Resolver.cs` (saf: ses seçimi `VoiceForSpeaker(seed)`, stres `StressFor(can, bastırılma, çatışma, düşen dost)`, yol `ResourcePath`, eski yol `LegacyPath`); testler `VoiceV2ResolverTests`.

Cursor'ın doğrulaması/bağlaması:
- ENTEGRASYON: RadioVoicePlayer / Dialogue/DialogueClipLibrary: klibi önce `Resources.Load(VoiceV2Resolver.ResourcePath(VoiceV2Resolver.VoiceForSpeaker(seed), stress, id))`, bulunamazsa `VoiceV2Resolver.LegacyPath(id)`, o da yoksa sessizlik/prosedürel (altın kural).
- `v2` WAV'ları Unity'de içe aktarılınca: Load Type Compressed In Memory/Vorbis kalite ~0.5, Force To Mono, Preload kapalı (3120 klip).
- Kulakla dinleme: dfki_kalin/dfki_genc doğal mı (formant kaydırma robotik olabilir), panik klipleri anlaşılır mı, ses seviyeleri karışımda dengeli mi (panik +3 dB gürültülü).
- Ses oyuncusu kaydı gelince aynı klasör yapısına (`<ses>=oyuncu_adi`) bırakılır, kod değişmez.
