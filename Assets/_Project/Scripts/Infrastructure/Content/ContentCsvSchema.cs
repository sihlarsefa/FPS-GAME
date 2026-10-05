namespace Project.Infrastructure.Content
{
    /// <summary>
    /// Design/Assets/*.csv kolon adları — C3-1 ve Varlık Eşleyici ortak sözleşmesi.
    /// Kolon adlarını değiştirme; C3-1 bu başlıklarla yazar, F3-4 okur.
    /// </summary>
    public static class ContentCsvSchema
    {
        public const string DesignAssetsFolder = "Design/Assets";

        public const string MaterialsFile = "materials.csv";
        public const string SoundsFile = "sounds.csv";
        public const string AnimationsFile = "animations.csv";
        public const string ModelsFile = "models.csv";

        public static readonly string[] MaterialsColumns =
        {
            "MaterialId", "kaynak_site", "varlik_adi", "url", "lisans", "ucretsiz_mi", "notlar"
        };

        public static readonly string[] SoundsColumns =
        {
            "SoundId", "kaynak_paket", "dosya_onerisi", "katman(yakın/orta/uzak)", "lisans", "notlar"
        };

        public static readonly string[] AnimationsColumns =
        {
            "durum", "mixamo_animasyon_adi", "in_place", "notlar"
        };

        public static readonly string[] ModelsColumns =
        {
            "kimlik(WeaponId/arac/asker/bina)", "gecici_hazir_model_onerisi", "kaynak", "fiyat_araligi",
            "benzerlik_notu", "lisans"
        };
    }
}
