namespace Project.Core.Domain
{
    /// <summary>Günün saati ön ayarı.</summary>
    public enum TimeOfDay
    {
        Gunduz = 0,
        Safak = 1,
        Aksam = 2,
        Gece = 3
    }

    /// <summary>Hava durumu.</summary>
    public enum WeatherKind
    {
        Acik = 0,
        Yagmur = 1,
        Kar = 2
    }

    /// <summary>Saf mantık: adlar, varsayılan hava, gece görüş çarpanları (Unity bağımlılığı yok).</summary>
    public static class AtmosphereRules
    {
        public static readonly string[] TimeNames = { "Gündüz", "Şafak", "Akşam", "Gece" };
        public static readonly string[] WeatherNames = { "Açık", "Hafif yağmur", "Kar" };

        public static string Name(TimeOfDay t) => TimeNames[Clamp((int)t, TimeNames.Length)];
        public static string Name(WeatherKind w) => WeatherNames[Clamp((int)w, WeatherNames.Length)];

        public static TimeOfDay TimeFromIndex(int i) => (TimeOfDay)Clamp(i, TimeNames.Length);
        public static WeatherKind WeatherFromIndex(int i) => (WeatherKind)Clamp(i, WeatherNames.Length);

        /// <summary>Haritanın varsayılan havası: Ayaz Geçidi karlı, diğerleri açık.</summary>
        public static WeatherKind DefaultWeatherForMap(string mapId) => mapId == MapCatalog.AyazGecidi ? WeatherKind.Kar : WeatherKind.Acik;

        /// <summary>Sis yoğunluğu çarpanı (gece görüş sınırlı; yağmur/kar sisi artırır).</summary>
        public static float FogMultiplier(TimeOfDay t, WeatherKind w)
        {
            float m = t == TimeOfDay.Gece ? 2.6f : t == TimeOfDay.Safak ? 1.5f : t == TimeOfDay.Aksam ? 1.15f : 1f;
            if (w == WeatherKind.Yagmur) m *= 1.35f;
            else if (w == WeatherKind.Kar) m *= 1.25f;
            return m;
        }

        /// <summary>Haritaya özgü ek sis çarpanı: Mavi Liman'da deniz sisi (şafakta en yoğun), diğer haritalarda 1.</summary>
        public static float MapFogMultiplier(string mapId, TimeOfDay t)
        {
            if (MapCatalog.Normalize(mapId) != MapCatalog.MaviLiman)
                return 1f;
            return t == TimeOfDay.Safak ? 1.9f : t == TimeOfDay.Aksam ? 1.4f : t == TimeOfDay.Gece ? 1.2f : 1.35f;
        }

        /// <summary>Haritada deniz sisi (mavimsi-gri ton) uygulanır mı?</summary>
        public static bool HasSeaFog(string mapId) => MapCatalog.Normalize(mapId) == MapCatalog.MaviLiman;

        /// <summary>Namlu alevi ölçek çarpanı: gece belirgin biçimde güçlü.</summary>
        public static float MuzzleFlashBoost(TimeOfDay t) => t == TimeOfDay.Gece ? 1.8f : t == TimeOfDay.Safak || t == TimeOfDay.Aksam ? 1.25f : 1f;

        /// <summary>El feneri yalnız gece/şafakta anlamlı (gündüz de açılabilir ama etkisiz).</summary>
        public static bool FlashlightUseful(TimeOfDay t) => t == TimeOfDay.Gece || t == TimeOfDay.Safak;

        private static int Clamp(int i, int len) => i < 0 ? 0 : i >= len ? len - 1 : i;
    }
}
