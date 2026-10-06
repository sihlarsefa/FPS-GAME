using System;
using Project.Core.Interfaces;

namespace Project.Application.Settings
{
    /// <summary>ExtendedSettings'i ISettingsStore'a (PlayerPrefs "harekat." önekiyle) yazar/okur. Bozuk/yok mağaza güvenlidir.</summary>
    public static class ExtendedSettingsStore
    {
        public static ExtendedSettings Load(ISettingsStore store)
        {
            if (store == null)
                return NewDefault();
            try
            {
                return ExtendedSettings.FromReader((k, def) => store.GetFloat(k, def));
            }
            catch (Exception)
            {
                return NewDefault();
            }
        }

        public static void Save(ISettingsStore store, ExtendedSettings settings)
        {
            if (store == null || settings == null)
                return;
            var clean = settings.Clone();
            clean.Sanitize();
            try
            {
                store.SetInt(ExtendedSettings.Keys.Version, ExtendedSettings.CurrentVersion);
                foreach (var kv in clean.ToPairs())
                    store.SetFloat(kv.Key, kv.Value);
                store.Save();
            }
            catch (Exception)
            {
                // Mağaza yazılamadı: bellekteki ayarlar geçerli kalır.
            }
        }

        public static ExtendedSettings NewDefault()
        {
            var d = new ExtendedSettings();
            d.Sanitize();
            return d;
        }
    }
}
