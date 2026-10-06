namespace Project.Infrastructure.Pooling
{
    /// <summary>
    /// Sıcak yolda ayırma yapan yerlerin taraması ve bağlantı notları (derleme etkisi yok).
    /// Bu dosya sahipsiz kodlara dokunmaz; sahipler aşağıdaki maddeleri uygular.
    ///
    /// Tarama (Update/LateUpdate/FixedUpdate/OnGUI gövdeleri, Infrastructure+Presentation):
    ///  - Presentation/DevTools/PerfOverlay.cs: OnGUI içinde her karede $"..." metin enterpolasyonu (satır ~103-109)
    ///    ve FmtMs'te float.ToString. Yalnız F3 açıkken; yine de ~300 B/kare. Çözüm: PerfMonitor.Frame/Alloc
    ///    + IntStringCache, metni saniyede 1 kez üret.
    ///  - Presentation/UI/UiQaOverlay.cs:77 FindObjectsByType&lt;Canvas&gt; (her tarama); sonucu önbelleğe al
    ///    ya da ListPool kullan. Presentation/UI/HitFeedbackDirector.cs:120,137 ve Presentation/Player/PlayerCameraFx.cs:196
    ///    FindFirstObjectByType/FindObjectsByType: yalnız referans null iken çağrılıyor mu doğrula; kaybolursa her karede aranır.
    ///  - Infrastructure/Vehicles/VehicleFeel.cs (3 nokta) ve Presentation/UI/LobbySoundscape.cs: Update içinde
    ///    ToString/yeni koleksiyon; ListPool&lt;T&gt;.Get ile değiştir.
    ///  - Infrastructure/Rendering/Perf/ParticleCameraCuller.cs ve AudioDistanceCuller.cs: yeniden taramada
    ///    FindObjectsByType + AddRange; ListPool ve tarama aralığını uzat (örn. 5 sn).
    ///  - Infrastructure/Transport/FlyableHelicopter.cs:552 ve Infrastructure/Vehicles/DrivableVehicle.cs:504:
    ///    Destroy(gameObject, süre) ile gecikmeli yok etme; enkaz/VFX için ComponentPool + TimedReleaseScheduler.
    ///  - ~153 dosyada void Update(): zamanlayıcı/aralık gerektirenleri (UI yenileme, ambiyans, AI düşük sıklık) 
    ///    IBatchTickable + PerfMonitor.Ticks.Register(this, aralık, Time.unscaledTimeAsDouble) ile toplu yöneticiye taşı.
    ///
    /// ENTEGRASYON: Vfx/ParticleEffectPool.cs, DecalPool.cs, TracerPool.cs, ShellCasingPool.cs, FlashLightPool.cs,
    ///   SmokeGlowPool.cs ve Audio/AudioPool.cs: kendi Stack/List havuzları ObjectPool&lt;T&gt;/ComponentPool&lt;T&gt; ile
    ///   birleştirilebilir (PoolStats.Misses sıcak yolda 0 olmalı; yükleme anında Prewarm çağır).
    /// ENTEGRASYON: Audio/AudioPool.cs içinde: tüm kaynaklar doluyken VoiceStealer.PickVictim ile en önemsiz sesi kes
    ///   ve üst sınırı VoiceStealer.MaxVoices(QualitySettings.GetQualityLevel()) ile belirle.
    /// ENTEGRASYON: Rendering/AutoQuality.cs içinde: PerfMonitor.AdviceChanged olayını dinleyip kalite kademesini
    ///   düşür/yükselt; parçacık/dekal yoğunluğunu PerfMonitor.BudgetScale ile çarp.
    /// ENTEGRASYON: Rendering/MemoryJanitor.cs içinde: sahne geçişinde GcTuningMath.ShouldCollectAtSafePoint ile
    ///   yalnız güvenli anda elle GC.Collect çağır.
    /// </summary>
    internal static class HotPathIntegrationNotes { }
}
