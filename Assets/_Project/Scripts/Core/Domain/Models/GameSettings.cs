namespace Project.Core.Domain
{
    /// <summary>Oyuncu tercihleri. Kalıcılık ISettingsStore üzerinden yapılır.</summary>
    public sealed class GameSettings
    {
        public float MouseSensitivity { get; set; } = 0.12f;
        public float AdsSensitivityMultiplier { get; set; } = 0.8f;
        public float FieldOfView { get; set; } = 64f;
        /// <summary>Silah (viewmodel) görüş açısı 50-80 (varsayılan 54).</summary>
        public float ViewmodelFov { get; set; } = 54f;
        /// <summary>Nişan alırken (ADS) hassasiyet yakınlaştırma/FOV oranıyla ölçeklensin (Tarkov/CoD tarzı).</summary>
        public bool AdsFovRelativeSensitivity { get; set; } = true;
        /// <summary>Hacimsel sis kullanıcı tercihi (kalite kademesi kapalıysa yine kapalı).</summary>
        public bool VolumetricFog { get; set; } = true;
        /// <summary>Temas gölgeleri kullanıcı tercihi.</summary>
        public bool ContactShadows { get; set; } = true;
        /// <summary>Ekran-uzayı yansımalar (SSR) kullanıcı tercihi.</summary>
        public bool ScreenSpaceReflections { get; set; } = true;
        public float MasterVolume { get; set; } = 0.8f;
        public float AmbientVolume { get; set; } = 0.6f;
        public bool InvertY { get; set; }
        public int QualityLevel { get; set; } = 2;
        public bool Fullscreen { get; set; } = true;
        public bool ShowFps { get; set; }

        // ---- Gelişmiş ayarlar ----
        /// <summary>Çözünürlük genişliği/yüksekliği (0 = ekranın yerel çözünürlüğü).</summary>
        public int ResolutionWidth { get; set; }
        public int ResolutionHeight { get; set; }
        /// <summary>Pencere modu: 0 tam ekran, 1 çerçevesiz pencere, 2 pencere (-1 = eski Fullscreen alanından türet).</summary>
        public int WindowMode { get; set; } = -1;
        public bool VSync { get; set; }
        /// <summary>Kare sınırı: 0 sınırsız, aksi hâlde 30/60/120/144.</summary>
        public int FrameRateCap { get; set; } = 60;
        /// <summary>Render ölçeği 0.5-1.0.</summary>
        public float RenderScale { get; set; } = 1f;
        public float SfxVolume { get; set; } = 1f;
        public float MusicVolume { get; set; } = 1f;
        public float VoiceVolume { get; set; } = 1f;
        /// <summary>Nişangâh rengi: 0 beyaz, 1 yeşil, 2 sarı, 3 camgöbeği, 4 kırmızı.</summary>
        public int CrosshairColor { get; set; }
        /// <summary>Nişangâh boyut çarpanı 0.5-2.</summary>
        public float CrosshairSize { get; set; } = 1f;
        /// <summary>HUD ölçeği 0.8-1.2.</summary>
        public float HudScale { get; set; } = 1f;
        /// <summary>HUD opaklığı 0.3-1.</summary>
        public float HudOpacity { get; set; } = 1f;
        /// <summary>Kamera sarsıntı şiddeti 0 (kapalı) - 1.5; 1 = varsayılan.</summary>
        public float CameraShakeIntensity { get; set; } = 1f;
        /// <summary>Hareket bulanıklığı (şimdilik yer tutucu).</summary>
        public bool MotionBlur { get; set; }
        /// <summary>Renk körlüğü (deuteranopi) paleti.</summary>
        public bool ColorBlindMode { get; set; }
        /// <summary>Renk körlüğü paleti: 0 kapalı, 1 deuteranopi, 2 protanopi, 3 tritanopi.</summary>
        public int ColorBlindPalette { get; set; }

        // ---- Erişilebilirlik ----
        /// <summary>Gamepad nişan yardımı gücü 0-100 (yalnızca gamepad girdisinde; fare etkilenmez).</summary>
        public int AimAssistStrength { get; set; } = 50;
        /// <summary>Telsiz altyazı boyutu: 0 küçük, 1 normal, 2 büyük, 3 çok büyük.</summary>
        public int SubtitleSize { get; set; } = 1;
        /// <summary>Telsiz altyazılarının arkasında yarı saydam zemin.</summary>
        public bool SubtitleBackground { get; set; }
        /// <summary>Nişan: true = Aç-Kapa (tek tık), false = Bas-Tut. Varsayılan Aç-Kapa (sağ tık tek basışla nişan).</summary>
        public bool ToggleAds { get; set; } = true;
        /// <summary>Eğilme tuşu aç/kapa (basılı tutmak yerine).</summary>
        public bool ToggleCrouch { get; set; }

        /// <summary>Maçtaki tim sayısı (her tim 10 kişi). Varsayılan 4 tim = 40 asker.</summary>
        public int TeamCount { get; set; } = 4;

        public BotDifficulty Difficulty { get; set; } = BotDifficulty.Normal;
        public InsertionMethod Insertion { get; set; } = InsertionMethod.Helicopter;
        /// <summary>Arayüz dili: tr, en, de, az, ar.</summary>
        public string Language { get; set; } = "tr";
        /// <summary>Seçili harekât haritası (MapCatalog kimliği).</summary>
        public string SelectedMap { get; set; } = MapCatalog.Kuzgun;
        /// <summary>İsim profili: 0 Gerçek, 1 Kurgusal (silah/araç görünen adları).</summary>
        public int NameProfile { get; set; }
        public string PlayerName { get; set; } = "Komutan";

        /// <summary>Eski alan (tim sayısından türetilir).</summary>
        public int BotCount { get; set; } = 39;

        public GameSettings Clone() => (GameSettings)MemberwiseClone();
    }
}
