using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.Characters
{
    /// <summary>Yüz örtüsü türü (tim/palet başına çeşitlilik).</summary>
    public enum FaceCoverKind
    {
        None = 0,
        Balaclava = 1,
        Shemagh = 2
    }

    /// <summary>
    /// Asker ayrıntı kuralları (saf mantık, test edilebilir): yüz örtüsü seçimi, sırt çantası arka yüz konumu,
    /// yürüyüş kalça sallanması ve anten gecikmesi.
    /// </summary>
    public static class SoldierDetailRules
    {
        /// <summary>
        /// Palet (0 Orman, 1 Dağ, 2 Çöl, 3 Şehir) ve kişisel rastgelelik (0..1) ile yüz örtüsü:
        /// Dağ → balaklava, Çöl → şemagh, Şehir → balaklava (%60), Orman → çıplak yüz (%20 balaklava).
        /// </summary>
        public static FaceCoverKind FaceCoverFor(int paletteIndex, float roll01)
        {
            switch (paletteIndex)
            {
                case 1:
                    return FaceCoverKind.Balaclava;
                case 2:
                    return roll01 < 0.85f ? FaceCoverKind.Shemagh : FaceCoverKind.None;
                case 3:
                    return roll01 < 0.6f ? FaceCoverKind.Balaclava : FaceCoverKind.None;
                default:
                    return roll01 < 0.2f ? FaceCoverKind.Balaclava : FaceCoverKind.None;
            }
        }

        /// <summary>Çanta seviyesine (1..3) göre arka yüzün z konumu (sırt çantası kökünde yerel).</summary>
        public static float BackpackBackZ(int level)
        {
            switch (level)
            {
                case 1: return -0.105f;
                case 2: return -0.145f;
                case 3: return -0.175f;
                default: return -0.09f;
            }
        }

        /// <summary>Yürürken kalçanın yana kayması (metre): bacak fazına bağlı, duruşa göre sönümlü.</summary>
        public static float HipSway(float phaseSin, float moveWeight, float prone, float seat)
        {
            return phaseSin * 0.014f * Mathf.Clamp01(moveWeight) * (1f - Mathf.Clamp01(prone)) * (1f - Mathf.Clamp01(seat));
        }

        /// <summary>Anten/aksesuar ikincil hareketi: hız arttıkça geriye yatar, adım ritmiyle salınır (derece).</summary>
        public static float AntennaTilt(float speed, float phase)
        {
            return -Mathf.Clamp(speed, 0f, 7f) * 2.2f + Mathf.Sin(phase * 2f) * Mathf.Clamp01(speed / 3f) * 3.5f;
        }

        /// <summary>Gövde karşı burulması (derece): silahlıyken azaltılır.</summary>
        public static float ChestCounterTwist(float phaseSin, float moveWeight, bool hasWeapon)
        {
            return phaseSin * 3f * Mathf.Clamp01(moveWeight) * (hasWeapon ? 0.4f : 1f);
        }

        // ------------------------------------------------------------------ Humanoid override kuralları (C9)

        /// <summary>Humanoid animasyon kademesi sınırları (metre): 0 tam hız, 1 ~30 Hz, 2 ~15 Hz, 3 ~8 Hz.</summary>
        public static int AnimTier(float distance)
        {
            if (distance < 15f) return 0;
            if (distance < 35f) return 1;
            if (distance < 70f) return 2;
            return 3;
        }

        /// <summary>Kademeye göre Animator güncelleme aralığı (sn). 0 = her kare (Animator kendi çalışır).</summary>
        public static float AnimInterval(int tier)
        {
            switch (tier)
            {
                case 0: return 0f;
                case 1: return 1f / 30f;
                case 2: return 1f / 15f;
                default: return 1f / 8f;
            }
        }

        /// <summary>Kademeye göre skin kemik sayısı (4/2/1).</summary>
        public static int SkinBonesFor(int tier)
        {
            return tier <= 0 ? 4 : (tier == 1 ? 2 : 1);
        }

        /// <summary>Ölü/ragdoll askerde animasyon kademesi 0'a zorlanır (ölüm animasyonu tam hızda oynar).</summary>
        public static int EffectiveTier(float distance, bool dead)
        {
            return dead ? 0 : AnimTier(distance);
        }

        /// <summary>Görünmez ve gölgesizse Animator tamamen kesilir; aksi halde yalnız dönüşümler güncellenir.</summary>
        public static bool CullCompletely(bool visible, bool shadowsOnly)
        {
            return !visible && !shadowsOnly;
        }

        /// <summary>Palet indeksine karşılık gelen kamuflaj malzeme kimliği.</summary>
        public static MaterialId CamoMaterialFor(int paletteIndex)
        {
            switch (paletteIndex)
            {
                case 1: return MaterialId.CamoMountain;
                case 2: return MaterialId.CamoDesert;
                case 3: return MaterialId.CamoUrban;
                default: return MaterialId.CamoWoodland;
            }
        }

        /// <summary>Hazır modelin malzeme adı kamuflaj yuvası mı? ("camo", "kamuflaj", "uniform" içerir).</summary>
        public static bool IsCamoSlotName(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
                return false;
            var n = materialName.ToLowerInvariant();
            return n.Contains("camo") || n.Contains("kamuflaj") || n.Contains("uniform");
        }

        /// <summary>Aksesuar soket rolleri: 0 Baş (kask/bere), 1 Göğüs (yelek/anten), -1 bilinmiyor.</summary>
        public static int AccessorySocketFor(string partName)
        {
            if (string.IsNullOrEmpty(partName))
                return -1;
            var n = partName.ToLowerInvariant();
            if (n.StartsWith("helmet") || n.StartsWith("beret") || n.StartsWith("balaclava") || n.StartsWith("shemagh"))
                return 0;
            if (n.StartsWith("vest") || n.StartsWith("antenna") || n.StartsWith("backpack") || n.StartsWith("radio"))
                return 1;
            return -1;
        }

        // ------------------------------------------------------------------ Yorgunluk (battle-weary idle) kuralları

        /// <summary>Derin nefes frekansı (Hz) - normal rölantiden belirgin yavaş.</summary>
        public const float WearyBreathHz = 0.22f;
        /// <summary>Baş düşürme olayı toplam süresi (sn): düşme + bekleme + kalkış + küçük sallama.</summary>
        public const float WearyHeadEventDuration = 4.2f;
        /// <summary>Alın silme süresi (sn).</summary>
        public const float WearyBrowWipeDuration = 1.2f;
        /// <summary>Titreme eşiği (Yorgunluk bunun üzerinde başlar).</summary>
        public const float WearyTremorThreshold = 0.7f;

        /// <summary>0..1 tohum+indeks için kararlı sözde-rastgele değer.</summary>
        public static float WearyHash01(int seed, int index)
        {
            unchecked
            {
                uint h = (uint)seed * 747796405u + (uint)index * 2891336453u + 12345u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
                return (h & 0xFFFFFFu) / 16777216f;
            }
        }

        /// <summary>Omuz düşmesi (metre): Yorgunluk 1'de 4 cm, 0,5'te ~1,5 cm, 0'da 0.</summary>
        public static float WearySlumpDrop(float weary)
        {
            var w = Mathf.Clamp01(weary);
            return 0.02f * w + 0.02f * w * w;
        }

        /// <summary>Omuzların öne yuvarlanması (derece, gövde öne eğimi).</summary>
        public static float WearySlumpRoll(float weary)
        {
            return 4f * Mathf.Clamp01(weary);
        }

        /// <summary>Nefes genliği çarpanı: 1 (dinç) ile 1,2 (tam yorgun) arası.</summary>
        public static float WearyBreathAmplitude(float weary)
        {
            return 1f + 0.2f * Mathf.Clamp01(weary);
        }

        /// <summary>Derin nefes dalgası -1..1 (0,22 Hz). Faz = time * Hz.</summary>
        public static float WearyBreathWave(float time)
        {
            return Mathf.Sin(time * WearyBreathHz * Mathf.PI * 2f);
        }

        /// <summary>Yeni nefes alışı (dalga negatiften pozitife geçiş) bu aralıkta mı: sesli nefes kancası için.</summary>
        public static bool WearyInhaleCrossed(float prevTime, float time)
        {
            var a = Mathf.Floor(prevTime * WearyBreathHz);
            var b = Mathf.Floor(time * WearyBreathHz);
            return b > a;
        }

        /// <summary>İki baş düşürme olayı arası (sn): tohumlu, 9..14.</summary>
        public static float WearyHeadInterval(int seed, int index)
        {
            return 9f + 5f * WearyHash01(seed, index);
        }

        /// <summary>İki alın silme arası (sn): tohumlu, 16..30.</summary>
        public static float WearyBrowInterval(int seed, int index)
        {
            return 16f + 14f * WearyHash01(seed ^ 0x5bd1e995, index);
        }

        /// <summary>Baş eğimi (derece, + = öne/aşağı): 0..1,5 s yavaş düşer, 1,5..2,3 bekler, 2,3..3,5 kalkar.</summary>
        public static float WearyHeadDroopPitch(float t, float weary)
        {
            var w = Mathf.Clamp01(weary);
            float k;
            if (t <= 0f || t >= 3.5f) k = 0f;
            else if (t < 1.5f) k = Smooth01(t / 1.5f);
            else if (t < 2.3f) k = 1f;
            else k = 1f - Smooth01((t - 2.3f) / 1.2f);
            return k * (10f + 8f * w);
        }

        /// <summary>Kalkış sonrası küçük baş sallama (derece yaw): 3,3..4,2 s aralığında sönen salınım.</summary>
        public static float WearyHeadShakeYaw(float t, int seed, int index)
        {
            const float start = 3.3f;
            if (t < start || t > WearyHeadEventDuration) return 0f;
            var u = (t - start) / (WearyHeadEventDuration - start);
            var side = WearyHash01(seed, index + 977) < 0.5f ? -1f : 1f;
            return side * Mathf.Sin(u * Mathf.PI * 3f) * (1f - u) * 5f;
        }

        /// <summary>Alın silme ağırlığı 0..1 (el alına): 0,3 s kalkar, 0,6 s kalır, 0,3 s iner.</summary>
        public static float WearyBrowWipeWeight(float t)
        {
            if (t <= 0f || t >= WearyBrowWipeDuration) return 0f;
            if (t < 0.3f) return Smooth01(t / 0.3f);
            if (t < 0.9f) return 1f;
            return 1f - Smooth01((t - 0.9f) / 0.3f);
        }

        /// <summary>Ağırlık aktarma kalça kayması (metre): yavaş (0,11 Hz) ve daha ağır (Yorgunlukla 1,0..1,7 cm).</summary>
        public static float WearyHipSway(float time, float weary)
        {
            var w = Mathf.Clamp01(weary);
            return Mathf.Sin(time * 0.11f * Mathf.PI * 2f) * (0.010f + 0.007f * w) * w;
        }

        /// <summary>Hafif yorgunluk titremesi genliği (derece): yalnız Yorgunluk > 0,7.</summary>
        public static float WearyTremorAmplitude(float weary)
        {
            var w = Mathf.Clamp01(weary);
            if (w <= WearyTremorThreshold) return 0f;
            return (w - WearyTremorThreshold) / (1f - WearyTremorThreshold) * 0.6f;
        }

        /// <summary>İnce titreme dalgası -1..1 (~7-11 Hz karışım).</summary>
        public static float WearyTremorWave(float time, float phase)
        {
            return 0.6f * Mathf.Sin(time * 47f + phase) + 0.4f * Mathf.Sin(time * 71f + phase * 1.7f);
        }

        /// <summary>
        /// Etkin yorgunluk 0..1: hareket, nişan, çömelme/yatma, oturma, jest, tepme ve ölümde sıfıra iner
        /// (mevcut poz katmanlarını bozmaz).
        /// </summary>
        public static float WearyEffective(float weary, float speed, bool aiming, float prone, float seat, bool gestureActive, float recoil, bool dead)
        {
            if (dead || aiming || gestureActive || recoil > 0.02f)
                return 0f;
            var still = 1f - Mathf.Clamp01((speed - 0.05f) / 0.35f);
            return Mathf.Clamp01(weary) * still * (1f - Mathf.Clamp01(prone * 2f)) * (1f - Mathf.Clamp01(seat * 2f));
        }

        /// <summary>Serbest el: -1 yok (tüfek iki elde), 0 sol (tabanca), 1 sağ (silahsız, tohuma göre sol/sağ).</summary>
        public static int WearyFreeHand(bool hasRifle, bool hasPistol, int seed)
        {
            if (hasRifle) return -1;
            if (hasPistol) return 0;
            return WearyHash01(seed, 31) < 0.5f ? 0 : 1;
        }

        private static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
