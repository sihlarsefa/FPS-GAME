using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>Donanım ekranındaki beş silah çubuğunun 0..1 değerleri (+ ham sayılar).</summary>
    public readonly struct LoadoutStatSet
    {
        public readonly float Damage;
        public readonly float FireRate;
        public readonly float Range;
        public readonly float Control;
        public readonly float Mobility;
        public readonly float RoundsPerMinute;
        public readonly int MagazineSize;
        public readonly float WeightKg;
        /// <summary>Nişan alma hızı 0..1 (yüksek = hızlı).</summary>
        public readonly float AdsSpeed;

        public LoadoutStatSet(float damage, float fireRate, float range, float control, float mobility, float rpm, int magazine, float weightKg, float adsSpeed = 0f)
        {
            AdsSpeed = adsSpeed;
            Damage = damage;
            FireRate = fireRate;
            Range = range;
            Control = control;
            Mobility = mobility;
            RoundsPerMinute = rpm;
            MagazineSize = magazine;
            WeightKg = weightKg;
        }
    }

    /// <summary>
    /// Silah + eklenti istatistiklerini katalog en büyük değerlerine göre 0..1'e normalleştirir (saf mantık).
    /// Hasar (atış başına, saçmalar dahil), atış hızı, menzil, kontrol (sekme+yayılım) ve hareketlilik (ağırlık+nişan süresi).
    /// </summary>
    public static class LoadoutStats
    {
        private static float _maxDamage, _maxRpm, _maxRange, _maxRecoil, _maxWeight;
        private static bool _ready;

        public static readonly string[] Labels = { "HASAR", "ATIŞ HIZI", "MENZİL", "KONTROL", "HAREKET", "NİŞAN" };

        private static void EnsureReady()
        {
            if (_ready)
                return;
            _ready = true;
            var all = WeaponCatalog.All;
            for (var i = 0; i < all.Count; i++)
            {
                var w = all[i];
                _maxDamage = Mathf.Max(_maxDamage, PerTrigger(w));
                _maxRpm = Mathf.Max(_maxRpm, WeaponCatalog.RoundsPerMinute(w));
                _maxRange = Mathf.Max(_maxRange, w.Range);
                _maxRecoil = Mathf.Max(_maxRecoil, RecoilScore(w));
                _maxWeight = Mathf.Max(_maxWeight, w.Weight + 1.5f);
            }

            _maxDamage = Mathf.Max(1f, _maxDamage);
            _maxRpm = Mathf.Max(1f, _maxRpm);
            _maxRange = Mathf.Max(1f, _maxRange);
            _maxRecoil = Mathf.Max(0.01f, _maxRecoil);
            _maxWeight = Mathf.Max(1f, _maxWeight);
        }

        private static float PerTrigger(WeaponDefinitionData w) => w.Damage * Mathf.Max(1, w.PelletCount);

        private static float RecoilScore(WeaponDefinitionData w) => w.RecoilVertical + w.RecoilHorizontal * 0.6f + w.AdsSpread * 0.4f;

        /// <summary>Silahın eklentisiz değerleri.</summary>
        public static LoadoutStatSet Compute(WeaponDefinitionData weapon) => Compute(weapon, null);

        /// <summary>Silah + takılı eklenti kimlikleriyle (null/boş öğeler yok sayılır) hesaplar.</summary>
        public static LoadoutStatSet Compute(WeaponDefinitionData weapon, IEnumerable<string> attachmentIds)
        {
            if (weapon == null)
                return default;
            EnsureReady();

            float recoilMul = 1f, adsSpreadMul = 1f, velocityMul = 1f, adsTimeMul = 1f, magMul = 1f, weight = weapon.Weight;
            if (attachmentIds != null)
            {
                foreach (var id in attachmentIds)
                {
                    if (string.IsNullOrEmpty(id))
                        continue;
                    var a = AttachmentCatalog.Get(id);
                    if (a == null || !a.IsCompatibleWith(weapon.Category))
                        continue;
                    recoilMul *= a.RecoilMultiplier;
                    adsSpreadMul *= a.AdsSpreadMultiplier;
                    velocityMul *= a.VelocityMultiplier;
                    adsTimeMul *= a.AdsTimeMultiplier;
                    magMul *= a.MagazineMultiplier;
                    weight += a.WeightKg;
                }
            }

            var rpm = WeaponCatalog.RoundsPerMinute(weapon);
            var recoil = weapon.RecoilVertical * recoilMul + weapon.RecoilHorizontal * 0.6f * recoilMul + weapon.AdsSpread * adsSpreadMul * 0.4f;
            var range = weapon.Range * (0.5f + 0.5f * velocityMul);

            var damage = MainMenuMotion.Fill(PerTrigger(weapon), _maxDamage);
            var fire = MainMenuMotion.Fill(rpm, _maxRpm);
            var rangeN = MainMenuMotion.Fill(range, _maxRange);
            var control = 1f - MainMenuMotion.Fill(recoil, _maxRecoil) * 0.92f;
            var mobility = (1f - MainMenuMotion.Fill(weight, _maxWeight)) * 0.8f + (1f - Mathf.Clamp01((weapon.AdsTime * adsTimeMul - 0.1f) / 0.6f)) * 0.2f;

            return new LoadoutStatSet(damage, fire, rangeN, Mathf.Clamp01(control), Mathf.Clamp01(mobility), rpm,
                Mathf.RoundToInt(weapon.MagazineSize * magMul), weight,
                1f - Mathf.Clamp01((weapon.AdsTime * adsTimeMul - 0.1f) / 0.6f));
        }

        public const int BarCount = 6;

        /// <summary>Eklentinin etkilerini kısa etiket olarak döndürür (örn. "+KONTROL", "-HAREKET"); pozitif etkiler önce. Saf mantık.</summary>
        public static string AttachmentEffects(AttachmentDefinition a)
        {
            if (a == null)
                return string.Empty;
            var good = new List<string>(3);
            var bad = new List<string>(2);
            if (a.RecoilMultiplier < 0.995f || a.AdsSpreadMultiplier < 0.995f) good.Add("+KONTROL");
            else if (a.RecoilMultiplier > 1.005f) bad.Add("-KONTROL");
            if (a.VelocityMultiplier > 1.005f) good.Add("+MENZİL");
            else if (a.VelocityMultiplier < 0.995f) bad.Add("-MENZİL");
            if (a.MagazineMultiplier > 1.005f) good.Add("+ŞARJÖR");
            if (a.AdsTimeMultiplier > 1.005f) bad.Add("-NİŞAN");
            else if (a.AdsTimeMultiplier < 0.995f) good.Add("+NİŞAN");
            if (a.WeightKg > 0.25f) bad.Add("-HAREKET");
            good.AddRange(bad);
            return string.Join(" ", good);
        }

        /// <summary>Silah sınıfı için kısa Türkçe açıklama (saf mantık).</summary>
        public static string Describe(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.Pistol: return "Hafif ve hızlı çekilen yan silah. Yakın mesafede güvenilir yedek.";
                case WeaponCategory.Smg: return "Yüksek atış hızı, düşük sekme. Dar alanlarda ve sokak çatışmasında üstün.";
                case WeaponCategory.AssaultRifle: return "Dengeli hasar, menzil ve kontrol. Her mesafede görev yapan ana silah.";
                case WeaponCategory.Sniper: return "Tek atışta yüksek hasar, çok uzun menzil. Yavaş nişan, sabit pozisyon ister.";
                case WeaponCategory.Shotgun: return "Yakın mesafede yıkıcı saçma hasarı. Menzil arttıkça etkisi hızla düşer.";
                case WeaponCategory.Dmr: return "Seçilmiş nişancı tüfeği: yarı otomatik atış, uzun menzil ve güçlü mermi.";
                case WeaponCategory.Lmg: return "Geniş şarjör ve bastırıcı ateş. Ağırdır; sabit durup ateş ederken en iyisi.";
                case WeaponCategory.Melee: return "Sessiz yakın dövüş silahı.";
                default: return string.Empty;
            }
        }

        /// <summary>İki çubuk değeri arası fark, 0..100 puan olarak yuvarlanmış (saf mantık; +/- gösterimi için).</summary>
        public static int DeltaPoints(float before, float after) => Mathf.RoundToInt(after * 100f) - Mathf.RoundToInt(before * 100f);

        /// <summary>Fark metni: "+5", "-3" ya da boş (0).</summary>
        public static string DeltaText(int points) => points > 0 ? "+" + points : (points < 0 ? points.ToString() : string.Empty);

        /// <summary>Çubuk indeksine göre (0..4) değer.</summary>
        public static float Value(in LoadoutStatSet set, int index)
        {
            switch (index)
            {
                case 0: return set.Damage;
                case 1: return set.FireRate;
                case 2: return set.Range;
                case 3: return set.Control;
                case 4: return set.Mobility;
                case 5: return set.AdsSpeed;
                default: return 0f;
            }
        }
    }
}
