# Sıradaki entegrasyon işleri (G36 sonrası, 2026-10-06)

Durum: 0 derleme hatası, 2695 EditMode testi geçiyor (commit 3ae63a8e4, dal `tasima-2026-10-06`).
G36'da yazılan sistemlerin bir kısmı henüz oyuna BAĞLI DEĞİL. Öncelik sırası:

## 1. Nişangah / silah tutuşu (kullanıcının şikâyet ettiği yer — önce bunlar)
- `Infrastructure/Weapons/WeaponViewModel.cs` `ApplyPose`: `damp` değeri `Mathf.Lerp(1, OpticMath.AdsSwayFactor(kind), aim)` olmalı.
  Kök neden: sabit 0.18 salınım sönümü ADS'de ~6.4 mrad ekranda kaymaya yol açıyor (5 mm ofset, 14 cm göz mesafesi).
- Kırmızı nokta ve optiklerde rotasyon `Quaternion.identity` yerine `SightAlignment.SolveOptic` kullanılmalı.
- `Presentation/Player/ScopeInput.cs` `Drive()`: `ScopeFrame`'in yeni alanları (`HasBore/BoreInCamera`, `HasLensCenter/LensCenterCamera`,
  `EyeLateral`, `WindowRadius`, `EyeRelief`, `ObjectiveMm`, `FirstFocalPlane`, `MinMagnification`, `ReticleHoldover`) viewmodel'den doldurulmalı.
- Fare hassasiyetinde `OpticMath.SensitivityScale` kullanılmalı; `Shaders/Weapon/HarekatCollimatorDot.shader` kırmızı nokta penceresine bağlanmalı.
- `Application/Viewmodel/ViewmodelMotionSolver.Step` çıktısı `WeaponViewModel` LateUpdate'te kök ofsete eklenmeli;
  `CameraPitch/CameraYaw/CameraDipM` → `Infrastructure/Rendering/CameraRig.cs`.
- `Infrastructure/Player/CharacterControllerMotor.Feel.cs`: ateş öncesi `CanFireFromMovement` (koşudan ateşe geçiş süresi) kontrolü.
- ÇÖZÜLMEDİ: `ViewmodelMeshes.cs` el mesh'lerinin avuç içi teması (ellerin silaha girmesi/havada kalması) — görsel doğrulamayla ayarlanmalı.
- `ViewmodelDynamics.DefaultViewmodelFov` 54 → 60 önerildi.

## 2. Görsel doğrulama (build gerekli)
Hiçbir G33–G36 lobi/silah değişikliği henüz ekranda görülmedi. Build öncesi: `df -h /` ≥ 10 GB ve `sysctl vm.swapusage` kontrolü;
build sırasında ajan çalıştırma. Sonra `zsh Tools/UnityVerify/oto_ekran.sh --menu "$PWD/Logs/otoekran/menu"` (mutlak yol).

## 3. Kodda bırakılmış tüm ENTEGRASYON notları
```
Application/Viewmodel/ViewmodelMotionSolver.cs:132:        // ENTEGRASYON: Infrastructure/Weapons/WeaponViewModel.cs icinde LateUpdate'te bu solver'in Step cikisi kok ofsetine eklenmeli (bob/sway/recoil/iniş/koşu).
Application/Viewmodel/ViewmodelMotionSolver.cs:133:        // ENTEGRASYON: Infrastructure/Rendering/CameraRig.cs icinde CameraPitch/CameraYaw/CameraDipM cikislari kamera ofsetine eklenmeli; Footfalls adim sesi/toz icin kullanilabilir.
Online/Netcode/JoinStateProvider.cs:13:    /// Bölge servisine uygulama (restore) ENTEGRASYON: olaya abone olan sistem yapar.
Infrastructure/Pooling/HotPathIntegrationNotes.cs:23:    /// ENTEGRASYON: Vfx/ParticleEffectPool.cs, DecalPool.cs, TracerPool.cs, ShellCasingPool.cs, FlashLightPool.cs,
Infrastructure/Pooling/HotPathIntegrationNotes.cs:26:    /// ENTEGRASYON: Audio/AudioPool.cs içinde: tüm kaynaklar doluyken VoiceStealer.PickVictim ile en önemsiz sesi kes
Infrastructure/Pooling/HotPathIntegrationNotes.cs:28:    /// ENTEGRASYON: Rendering/AutoQuality.cs içinde: PerfMonitor.AdviceChanged olayını dinleyip kalite kademesini
Infrastructure/Pooling/HotPathIntegrationNotes.cs:30:    /// ENTEGRASYON: Rendering/MemoryJanitor.cs içinde: sahne geçişinde GcTuningMath.ShouldCollectAtSafePoint ile
Infrastructure/World/InteriorLightShafts.cs:8:    /// ENTEGRASYON: BuildingGenerator pencere açıklığı hook'u: InteriorLightShafts.TryBuild(parent, center, along, w, h, inward, sunDir, floorY).
Infrastructure/World/InteriorDecorBuilder.cs:10:    /// ENTEGRASYON: BuildingGenerator pencere açıklığı hook'u: InteriorDecorBuilder.WindowSill(parent, sillCenter, along, width, inward, seed) çağırmalı (pencere konumu burada bilinmiyor);
Infrastructure/World/TreeScatter.cs:458:        /// Orman içi düşmüş kütük noktaları (~36 m hücre). ENTEGRASYON: prop üretimi (PropFactory/MicroPoi "kütük") bu listeyi tüketir.
Infrastructure/World/Pois/PoiDetailProps.cs:13:    /// // ENTEGRASYON: hazır asset gelince PoiDetailProps.ContentOverrides ile (ad -> prefab) kalemler değiştirilebilir.
Infrastructure/Weapons/ScopeController.cs:26:        // ENTEGRASYON: Presentation/Player/ScopeInput.cs Drive() içinde bu alanları viewmodel'den doldur
Infrastructure/Weapons/SightVisuals.cs:7:    /// ENTEGRASYON: WeaponBlueprints.cs: IronFrontPost genişliğini SightVisuals.FrontPostWidth, gez kulaklarını
Infrastructure/Weapons/Skins/WeaponSkinApplier.cs:9:    /// ENTEGRASYON: Presentation lobi VİTRİN'i WeaponSkinCatalog.All'ı listeler, seçilen Id loadout'a yazılır
Infrastructure/Support/SupportHelicopter.cs:107:                if (h._loop != null) h._loop.dopplerLevel = 1.3f; // geçişte frekans kayması (ENTEGRASYON: GameAudio.cs havuz kaynağında doppler 0 sıfırlaması)
Infrastructure/AI/BotController.cs:358:            Audio.Foley.GearFoleyEmitter.Attach(gameObject, false, () => Combatant != null && Combatant.Inventory != null && Combatant.Inventory.ActiveWeapon != null ? Combatant.Inventory.ActiveWeapon.WeaponId : null); //
Infrastructure/Rendering/Perf/StaticBatchAdvisor.cs:37:                        // ENTEGRASYON: world/prop yerleştirme kodu icinde prop kökü kurulduktan sonra StaticBatchAdvisor.Apply(kök, PipelineTiers kademesi) çağrılmalı.
Infrastructure/Characters/SoldierModel.Lean.cs:8:    /// eklenir. ENTEGRASYON: istenirse ana LateUpdate içine taşınabilir (spineYaw yanına roll olarak).
Infrastructure/Characters/Animation/WearyPoseLibrary.cs:39:    /// ENTEGRASYON: PipelineTiers.Tier düşükse çağıran taraf Step'i her 2 karede bir çağırmalı (bu sınıf Tier sorgulamaz).
Infrastructure/Characters/SoldierModel.Weary.cs:11:    /// ENTEGRASYON: WearyBreathIn olayı sesli nefes (Audio) için kancadır; SetWearyAiming ADS durumunu bildirmek içindir.
Infrastructure/Player/CharacterControllerMotor.Feel.cs:53:        /// ENTEGRASYON: WeaponViewModel/ScopeController bu metodu çağırmalı; ateş öncesi CanFireFromMovement kontrol edilmeli.
Presentation/UI/CrosshairView.cs:47:        // ENTEGRASYON: AdvancedDisplay.cs / ayar paneli icinde CrosshairSettings.Apply(...) cagrisi sonrasi CrosshairView.ApplyAppearance() tetiklenmeli.
Presentation/UI/MenuBackdropBuilder.cs:811:        // ENTEGRASYON: MenuBackdropDiorama.cs MenuDioramaBuilder.BuildSoldier/BuildBackgroundSquad içinde canlı sahne için
Presentation/UI/Settings/ExtendedSettingsRuntime.cs:58:        /// ENTEGRASYON: UnityInputReader.cs içinde look okumasından sonra bu çağrı yapılmalı.
Presentation/UI/Lobby/LobbyVignette.cs:29:            // ENTEGRASYON: MainMenuController.cs icinde ana canvas'in en arka cocugu olarak LobbyVignette.Create(canvas.transform) cagrilip SetAsFirstSibling yapilacak.
Presentation/UI/Lobby/LobbyShell.cs:72:            // ENTEGRASYON: lobiyi kuran kod Squad.Roster'a yerel oyuncuyu ekleyip Squad.InviteRequested'a davet arayüzünü bağlamalı.
Presentation/UI/Lobby/LobbyFlow.cs:30:        /// Bir bot katıldığında (ad, sıra 0..8, rol). ENTEGRASYON: Infrastructure/Audio/Dialogue API'si bu olaya telsiz
Presentation/UI/Hud/CompassLayout.cs:10:    /// CompassView bu sınıfı kullanabilir (ENTEGRASYON: CompassView.PlaceMarker).
Presentation/UI/Hud/AmmoPipStrip.cs:10:    /// ENTEGRASYON: HudWeaponView.cs içinde AmmoPipStrip.Create(...) ile kurulup her karede Set(ammo, magSize, time) çağrılmalı.
```
