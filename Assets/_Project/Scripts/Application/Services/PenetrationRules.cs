using System;
using Project.Core.Domain;

namespace Project.Application.Services
{
    /// <summary>Mermi geçirgenliği açısından yüzey sınıfı (Infrastructure SurfaceKind'inden eşlenir).</summary>
    public enum PenetrationMaterial
    {
        /// <summary>Geçilemez: arazi, bilinmeyen.</summary>
        Solid = 0,
        Foliage = 1,
        Wood = 2,
        Plaster = 3,
        ThinMetal = 4,
        /// <summary>Beton/taş: geçilemez, sekebilir.</summary>
        Concrete = 5,
        /// <summary>Tuğla/taş duvar (v2): tüfek durur, ağır kalibre bir kez delebilir.</summary>
        Brick = 6,
        /// <summary>Kum torbası (v2): tüm kalibreleri emer.</summary>
        Sandbag = 7
    }

    /// <summary>
    /// Mermi delme ve sekme kurallarının saf (Unity'siz) hesabı. Kalibre gücü: 7.62 &gt; 5.56 &gt; 9mm &gt; 12ga saçma.
    /// </summary>
    public static class PenetrationRules
    {
        /// <summary>Bir mermi en fazla bu kadar engel delebilir.</summary>
        public const int MaxPenetrationsPerBullet = 3;

        /// <summary>Sekme için en büyük sıyırma açısı (yüzeyle yol arasındaki açı, derece).</summary>
        public const float MaxRicochetAngle = 22f;

        /// <summary>Delinen/sekilen merminin hasarı bu değerin altına inerse mermi durur.</summary>
        public const float MinDamageScale = 0.12f;

        public static float CaliberPower(AmmoType ammo)
        {
            switch (ammo)
            {
                case AmmoType.Mm762: return 1f;
                case AmmoType.Mm556: return 0.7f;
                case AmmoType.Mm9: return 0.4f;
                case AmmoType.Gauge12: return 0.18f;
                default: return 0.5f;
            }
        }

        /// <summary>7.62 için metre cinsinden en kalın delinebilir kalınlık; kalibre gücüyle ölçeklenir.</summary>
        public static float BaseThickness(PenetrationMaterial material)
        {
            switch (material)
            {
                case PenetrationMaterial.Foliage: return 3f;
                case PenetrationMaterial.Wood: return 0.45f;
                case PenetrationMaterial.Plaster: return 0.35f;
                case PenetrationMaterial.ThinMetal: return 0.05f;
                default: return 0f;
            }
        }

        public static bool IsPenetrable(PenetrationMaterial material) => BaseThickness(material) > 0f;

        public static float MaxThickness(PenetrationMaterial material, AmmoType ammo) =>
            BaseThickness(material) * CaliberPower(ammo);

        public static bool CanPenetrate(PenetrationMaterial material, AmmoType ammo, float thickness)
        {
            var max = MaxThickness(material, ammo);
            return max > 0f && thickness >= 0f && thickness <= max;
        }

        /// <summary>Delme sonrası hasar çarpanı (0..1); kalın engel ve zayıf kalibre daha çok kaybettirir.</summary>
        public static float DamageFactor(PenetrationMaterial material, AmmoType ammo, float thickness)
        {
            var max = MaxThickness(material, ammo);
            if (max <= 0f)
                return 0f;

            var t = Clamp01(thickness / max);
            var power = CaliberPower(ammo);
            var baseKeep = material == PenetrationMaterial.Foliage ? 0.9f : 0.45f + 0.4f * power;
            return Clamp(baseKeep * (1f - 0.55f * t), 0f, 1f);
        }

        /// <summary>Delme sonrası hız çarpanı (0..1).</summary>
        public static float VelocityFactor(PenetrationMaterial material, AmmoType ammo, float thickness)
        {
            var max = MaxThickness(material, ammo);
            if (max <= 0f)
                return 0f;

            var t = Clamp01(thickness / max);
            var power = CaliberPower(ammo);
            var baseKeep = material == PenetrationMaterial.Foliage ? 0.95f : 0.6f + 0.3f * power;
            return Clamp(baseKeep * (1f - 0.4f * t), 0f, 1f);
        }

        public static bool CanRicochet(PenetrationMaterial material) =>
            material == PenetrationMaterial.ThinMetal || material == PenetrationMaterial.Concrete;

        /// <summary>
        /// Sekme olasılığı (0..1). <paramref name="grazingAngleDeg"/> = merminin yoluyla yüzey arasındaki açı
        /// (0 = yüzeye paralel). Saçma ve zayıf kalibre daha az sekerken sıyırma açısı küçüldükçe olasılık artar.
        /// </summary>
        public static float RicochetChance(PenetrationMaterial material, AmmoType ammo, float grazingAngleDeg)
        {
            if (!CanRicochet(material) || grazingAngleDeg < 0f || grazingAngleDeg >= MaxRicochetAngle)
                return 0f;

            var angleFactor = 1f - grazingAngleDeg / MaxRicochetAngle;
            var surface = material == PenetrationMaterial.ThinMetal ? 0.9f : 0.75f;
            var caliber = ammo == AmmoType.Gauge12 ? 0.35f : 0.7f + 0.3f * CaliberPower(ammo);
            return Clamp01(angleFactor * surface * caliber);
        }

        /// <summary>Sekmeden sonra kalan hız oranı.</summary>
        public const float RicochetSpeedKeep = 0.55f;

        /// <summary>Sekmeden sonra kalan hasar oranı.</summary>
        public const float RicochetDamageKeep = 0.4f;

        /// <summary>Bu hasar çarpanıyla mermi hâlâ yaralayıcı mı?</summary>
        public static bool StillLethal(float damageScale) => damageScale >= MinDamageScale;

        // ---------------------------------------------------------------- Zırh sınıfı vs kalibre

        /// <summary>Zırh seviyesinin (1..3) kalibre gücüne karşı direnci; kalibre gücüyle oranlanır.</summary>
        public static float ArmorClassRating(int level)
        {
            switch (level)
            {
                case 1: return 0.6f;
                case 2: return 0.85f;
                case 3: return 1.1f;
                default: return level > 3 ? 1.1f + 0.2f * (level - 3) : 0f;
            }
        }

        /// <summary>En zayıf zırh etkinliği: güçlü kalibre en düşük seviye zırhta bile DamageReduction'ın %40'ını bırakmaz değil, emdirir.</summary>
        public const float MinArmorEffectiveness = 0.4f;

        /// <summary>
        /// Zırhın DamageReduction değerine uygulanan etkinlik çarpanı (0.3..1). Düşük seviyeli zırh güçlü kalibreye (7.62)
        /// karşı zayıf, 9 mm ve saçmaya karşı güçlüdür; Sv.3 yelek 7.62'ye karşı %65, 9 mm'ye karşı ~%98 etkindir.
        /// Seviye ≤ 0 (bilinmeyen) = tam etkinlik (eski davranış).
        /// </summary>
        public static float ArmorEffectiveness(AmmoType ammo, int armorLevel)
        {
            if (armorLevel <= 0)
                return 1f;

            var ratio = CaliberPower(ammo) / ArmorClassRating(armorLevel);
            return Clamp(1.2f - 0.6f * ratio, MinArmorEffectiveness, 1f);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }

    /// <summary>Bir engel satırı: delme enerjisi maliyeti ve çıkış yayılımı (v2 tablo).</summary>
    public readonly struct BarrierRow
    {
        public readonly PenetrationMaterial Material;
        /// <summary>Metre başına enerji maliyeti (Joule).</summary>
        public readonly float JoulesPerMeter;
        /// <summary>Çıkışta yön yayılımı yarım koni açısı (derece).</summary>
        public readonly float ExitSpreadDeg;
        /// <summary>Kalınlıktan bağımsız tam emer (kum torbası).</summary>
        public readonly bool Absorbs;

        public BarrierRow(PenetrationMaterial m, float jpm, float spread, bool absorbs)
        {
            Material = m; JoulesPerMeter = jpm; ExitSpreadDeg = spread; Absorbs = absorbs;
        }
    }

    /// <summary>Delme sonucu: Unity'siz, VFX/ses tüketicileri için çıkış verisi.</summary>
    public readonly struct BarrierExit
    {
        public readonly bool Penetrated;
        public readonly float ExitEnergyJoules;
        public readonly float ExitSpeed;
        /// <summary>Çıkış hasar çarpanı (0..1) — giriş enerjisine oranın üstel eğrisi.</summary>
        public readonly float DamageScale;
        public readonly float SpreadDeg;
        /// <summary>Çıkış yönü (yayılım uygulanmış, normalize).</summary>
        public readonly float DirX, DirY, DirZ;

        public BarrierExit(bool pen, float e, float v, float dmg, float spread, float dx, float dy, float dz)
        {
            Penetrated = pen; ExitEnergyJoules = e; ExitSpeed = v; DamageScale = dmg; SpreadDeg = spread;
            DirX = dx; DirY = dy; DirZ = dz;
        }
    }

    /// <summary>
    /// Malzeme tablosu v2 ve enerji modeli: engel her zaman Joule cinsinden enerji çıkarır (tutarlı); kalan enerji
    /// sonraki engele taşınır, hız E = ½mv² ile türetilir. Eski <see cref="PenetrationRules"/> API'si korunur.
    /// </summary>
    public static class BarrierModel
    {
        /// <summary>Ağır (.338 sınıfı) kalibre için kalibre gücü; AmmoType enum'una dokunmadan kullanılır.</summary>
        public const float Heavy338Power = 1.7f;

        /// <summary>Çıkış hasar çarpanı üstel eğrisi (enerji oranı^0.75).</summary>
        public const float DamageExponent = 0.75f;

        public static BarrierRow Row(PenetrationMaterial m)
        {
            switch (m)
            {
                case PenetrationMaterial.Foliage: return new BarrierRow(m, 600f, 2f, false);
                case PenetrationMaterial.Wood: return new BarrierRow(m, 6000f, 6f, false);
                case PenetrationMaterial.Plaster: return new BarrierRow(m, 8000f, 9f, false);
                case PenetrationMaterial.ThinMetal: return new BarrierRow(m, 40000f, 12f, false);
                case PenetrationMaterial.Brick: return new BarrierRow(m, 45000f, 15f, false);
                case PenetrationMaterial.Concrete: return new BarrierRow(m, 120000f, 0f, false);
                case PenetrationMaterial.Sandbag: return new BarrierRow(m, 90000f, 0f, true);
                default: return new BarrierRow(PenetrationMaterial.Solid, float.PositiveInfinity, 0f, true);
            }
        }

        /// <summary>Kalibre başına namlu enerjisi (Joule), yaklaşık.</summary>
        public static float MuzzleEnergy(AmmoType ammo)
        {
            switch (ammo)
            {
                case AmmoType.Mm762: return 3400f;
                case AmmoType.Mm556: return 1800f;
                case AmmoType.Mm9: return 520f;
                case AmmoType.Gauge12: return 2000f;
                default: return 1000f;
            }
        }

        /// <summary>.338 sınıfı (anti-malzeme tüfeği) namlu enerjisi.</summary>
        public const float Muzzle338Joules = 6500f;

        /// <summary>Verilen enerji ve eğik kalınlıkla engeli delip delemeyeceği.</summary>
        public static bool Penetrates(PenetrationMaterial m, float energyJoules, float thickness)
        {
            var row = Row(m);
            if (row.Absorbs || thickness < 0f) return false;
            return energyJoules - row.JoulesPerMeter * thickness > 0f;
        }

        /// <summary>Engelden çıkan enerji (0 = durdu); tek tek çağrıldığında monoton azalan.</summary>
        public static float ExitEnergy(PenetrationMaterial m, float energyJoules, float thickness)
        {
            var row = Row(m);
            if (row.Absorbs || thickness < 0f || energyJoules <= 0f) return 0f;
            var left = energyJoules - row.JoulesPerMeter * thickness;
            return left > 0f ? left : 0f;
        }

        /// <summary>Hızı enerji azalmasına göre günceller: v' = v·sqrt(E'/E).</summary>
        public static float SpeedAfter(float speed, float entryEnergy, float exitEnergy)
        {
            if (entryEnergy <= 0f || exitEnergy <= 0f || speed <= 0f) return 0f;
            var r = exitEnergy / entryEnergy;
            return speed * (float)Math.Sqrt(r > 1f ? 1f : r);
        }

        /// <summary>
        /// Tam engel değerlendirmesi. <paramref name="u"/>,<paramref name="v"/> -1..1 arası rastgele girdilerdir
        /// (saf kalması için dışarıdan verilir); çıkış yönü bu girdilerle koni içinde saptırılır.
        /// </summary>
        public static BarrierExit Evaluate(PenetrationMaterial m, float entryEnergy, float speed, float thickness,
            float dirX, float dirY, float dirZ, float u, float v)
        {
            var exitE = ExitEnergy(m, entryEnergy, thickness);
            if (exitE <= 0f)
                return new BarrierExit(false, 0f, 0f, 0f, 0f, dirX, dirY, dirZ);

            var row = Row(m);
            // Yayılım: ince/zayıf enerji kaybı yüksekse daha çok saçılır.
            var lost = entryEnergy > 0f ? 1f - exitE / entryEnergy : 0f;
            var spread = row.ExitSpreadDeg * (0.5f + 0.5f * Clamp01(lost));
            Deflect(dirX, dirY, dirZ, spread, u, v, out var ox, out var oy, out var oz);
            var ratio = entryEnergy > 0f ? Clamp01(exitE / entryEnergy) : 0f;
            var dmg = (float)Math.Pow(ratio, DamageExponent);
            return new BarrierExit(true, exitE, SpeedAfter(speed, entryEnergy, exitE), dmg, spread, ox, oy, oz);
        }

        /// <summary>Yönü, normalleştirilmiş dik tabanda koni içinde saptırır.</summary>
        public static void Deflect(float dx, float dy, float dz, float spreadDeg, float u, float v,
            out float ox, out float oy, out float oz)
        {
            var len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-6f) { ox = dx; oy = dy; oz = dz; return; }
            dx /= len; dy /= len; dz /= len;
            // Dik taban: yön ile en az hizalı eksenle çapraz çarpım.
            float ax = 0f, ay = 0f, az = 0f;
            if (Math.Abs(dy) < 0.9f) ay = 1f; else ax = 1f;
            float t1x = dy * az - dz * ay, t1y = dz * ax - dx * az, t1z = dx * ay - dy * ax;
            var t1l = (float)Math.Sqrt(t1x * t1x + t1y * t1y + t1z * t1z);
            t1x /= t1l; t1y /= t1l; t1z /= t1l;
            float t2x = dy * t1z - dz * t1y, t2y = dz * t1x - dx * t1z, t2z = dx * t1y - dy * t1x;
            var k = (float)Math.Tan(spreadDeg * 0.017453292f);
            u = Clamp(u, -1f, 1f); v = Clamp(v, -1f, 1f);
            ox = dx + (t1x * u + t2x * v) * k;
            oy = dy + (t1y * u + t2y * v) * k;
            oz = dz + (t1z * u + t2z * v) * k;
            var ol = (float)Math.Sqrt(ox * ox + oy * oy + oz * oz);
            ox /= ol; oy /= ol; oz /= ol;
        }

        /// <summary>Eğik geliş: efektif kalınlık = kalınlık / cos(geliş açısı); sıyırma açısı derece (yüzeyle yol).</summary>
        public static float EffectiveThickness(float normalThickness, float grazingAngleDeg)
        {
            var s = (float)Math.Sin(Clamp(grazingAngleDeg, 5f, 90f) * 0.017453292f);
            return normalThickness / s;
        }

        private static float Clamp01(float x) => x < 0f ? 0f : (x > 1f ? 1f : x);
        private static float Clamp(float x, float lo, float hi) => x < lo ? lo : (x > hi ? hi : x);
    }

    public enum ArmorCoverage { Bypass = 0, SoftArmor = 1, Plate = 2 }

    public enum HelmetHitZone { None = 0, Shell = 1, Rim = 2, Face = 3 }

    /// <summary>
    /// Vücut yerel koordinatlarında zırh bölgeleri (saf matematik). Gövde yerel: x sağ, y yukarı (kalça=0), z ileri.
    /// Plaka yalnız orta gövdeyi kaplar; omuz/yan vuruşlar plakayı atlar (yumuşak zırh veya çıplak).
    /// </summary>
    public static class ArmorZones
    {
        public const float PlateHalfWidth = 0.14f;
        public const float PlateMinY = 0.30f;
        public const float PlateMaxY = 0.62f;
        public const float SoftHalfWidth = 0.22f;
        /// <summary>Gövde ön/arka eksenine göre bu açıdan fazla yandan gelen atış plakayı atlar (derece).</summary>
        public const float PlateMaxAzimuthDeg = 50f;

        /// <summary>
        /// Gövde yerel vuruş noktası ve geliş yönünden (yatay düzlem) kapsama. <paramref name="azimuthDeg"/> =
        /// geliş yönünün gövde ön/arka ekseniyle yatay açısı (0 = tam önden/arkadan, 90 = yandan).
        /// </summary>
        public static ArmorCoverage Classify(float localX, float localY, float azimuthDeg)
        {
            var ax = Math.Abs(localX);
            var az = Math.Abs(azimuthDeg);
            if (ax <= PlateHalfWidth && localY >= PlateMinY && localY <= PlateMaxY && az <= PlateMaxAzimuthDeg)
                return ArmorCoverage.Plate;
            if (ax <= SoftHalfWidth && localY >= 0.15f && localY <= 0.72f)
                return ArmorCoverage.SoftArmor;
            return ArmorCoverage.Bypass;
        }

        /// <summary>Yatay geliş açısı (derece, 0..90): vuruş yönü ile gövde ileri ekseni (ön/arka simetrik).</summary>
        public static float AzimuthDeg(float dirX, float dirZ, float forwardX, float forwardZ)
        {
            var dl = (float)Math.Sqrt(dirX * dirX + dirZ * dirZ);
            var fl = (float)Math.Sqrt(forwardX * forwardX + forwardZ * forwardZ);
            if (dl < 1e-5f || fl < 1e-5f) return 0f;
            var c = Math.Abs((dirX * forwardX + dirZ * forwardZ) / (dl * fl));
            return (float)Math.Acos(c > 1f ? 1f : c) * 57.29578f;
        }

        /// <summary>Kapsama başına zırh etkinlik çarpanı (1 = tam, 0 = yok).</summary>
        public static float CoverageFactor(ArmorCoverage c, float azimuthDeg)
        {
            switch (c)
            {
                case ArmorCoverage.Plate:
                    // Plaka yandan eğik gelince etkin kalınlık artar ama kenara yaklaşınca azalır.
                    var t = Math.Abs(azimuthDeg) / PlateMaxAzimuthDeg;
                    return 1f - 0.25f * (t > 1f ? 1f : t);
                case ArmorCoverage.SoftArmor: return 0.45f;
                default: return 0f;
            }
        }

        /// <summary>
        /// Miğfer bölgesi. <paramref name="localY"/> baş merkezine göre yükseklik (m), <paramref name="azimuthDeg"/> =
        /// yüzün baktığı yönle işaretsiz yatay geliş açısı (0 = tam yüz önü). Kenar = siperlik bandı.
        /// </summary>
        public static HelmetHitZone ClassifyHelmet(float localY, float azimuthDeg, bool frontHit)
        {
            const float brimY = 0.04f, rimBand = 0.035f;
            if (localY > brimY + rimBand) return HelmetHitZone.Shell;
            if (localY >= brimY - rimBand) return HelmetHitZone.Rim;
            return frontHit && Math.Abs(azimuthDeg) <= 40f ? HelmetHitZone.Face : HelmetHitZone.Shell;
        }

        /// <summary>Gövde isabeti zırh çarpanı (0..1): yerel nokta + yatay geliş açısı → <see cref="Classify"/> → <see cref="CoverageFactor"/>.</summary>
        public static float TorsoFactor(float localX, float localY, float azimuthDeg)
        {
            return CoverageFactor(Classify(localX, localY, azimuthDeg), azimuthDeg);
        }

        /// <summary>Kafa isabeti miğfer çarpanı (0..1): <see cref="ClassifyHelmet"/> → <see cref="HelmetProtection"/>.</summary>
        public static float HelmetFactor(float localY, float azimuthDeg, bool frontHit)
        {
            return HelmetProtection(ClassifyHelmet(localY, azimuthDeg, frontHit));
        }

        /// <summary>Miğfer koruma çarpanı: kabuk tam, kenar kısmen, yüz koruması yok.</summary>
        public static float HelmetProtection(HelmetHitZone z)
        {
            switch (z)
            {
                case HelmetHitZone.Shell: return 1f;
                case HelmetHitZone.Rim: return 0.55f;
                default: return 0f;
            }
        }
    }

    /// <summary>
    /// Kalibre başına saf balistik: hız kaybı (üstel sürükleme), uçuş süresi ve düşüş. Oyun içi mermi (BallisticsSystem)
    /// yalnız yerçekimi uygular; sürükleme eklenirse aynı <see cref="DragPerMeter"/> değerini kullanmalıdır.
    /// </summary>
    public static class BallisticsMath
    {
        public const float Gravity = 9.81f;

        /// <summary>Metre başına üstel hız kaybı katsayısı: v(x) = v0 * exp(-k x).</summary>
        public static float DragPerMeter(AmmoType ammo)
        {
            switch (ammo)
            {
                case AmmoType.Mm762: return 0.00071f;  // 7.62x51: 500 m'de ~%70 hız
                case AmmoType.Mm556: return 0.00085f;  // 5.56x45: hafif mermi daha çabuk yavaşlar
                case AmmoType.Mm9: return 0.0022f;     // 9x19: 100 m'de ~%80 hız
                case AmmoType.Gauge12: return 0.0040f; // saçma
                default: return 0.001f;
            }
        }

        public static float SpeedAt(float muzzleVelocity, AmmoType ammo, float range)
        {
            if (muzzleVelocity <= 0f || range <= 0f)
                return muzzleVelocity > 0f ? muzzleVelocity : 0f;
            return muzzleVelocity * (float)Math.Exp(-DragPerMeter(ammo) * range);
        }

        /// <summary>Menzile kadar uçuş süresi (sn).</summary>
        public static float TimeOfFlight(float muzzleVelocity, AmmoType ammo, float range)
        {
            if (muzzleVelocity <= 1f || range <= 0f)
                return 0f;
            var k = DragPerMeter(ammo);
            return (float)((Math.Exp(k * range) - 1.0) / (k * muzzleVelocity));
        }

        /// <summary>Namlu hattına göre düşüş (m) — sıfırlama açısı yokken; 0.5 g t².</summary>
        public static float Drop(float muzzleVelocity, AmmoType ammo, float range)
        {
            var t = TimeOfFlight(muzzleVelocity, ammo, range);
            return 0.5f * Gravity * t * t;
        }
    }

    /// <summary>
    /// Silah elleme süreleri: ağırlıktan türeyen nişan (ADS) ve koşudan ateşe geçiş süreleri.
    /// Katalog değerleri bu formüle yakın tutulur (test eder); eklenti ağırlığı da buraya eklenir.
    /// </summary>
    public static class WeaponHandling
    {
        public const float AdsBase = 0.12f;
        public const float AdsPerKg = 0.032f;
        public const float ScopeAdsPenalty = 0.04f;
        public const float SprintToFireBase = 0.12f;
        public const float SprintToFirePerKg = 0.03f;

        /// <summary>Ağırlığa (kg) göre nişan alma süresi; dürbünlü silah biraz yavaş.</summary>
        public static float AdsTimeForWeight(float weightKg, bool scoped)
        {
            var w = float.IsNaN(weightKg) || weightKg < 0f ? 0f : weightKg;
            return AdsBase + AdsPerKg * w + (scoped ? ScopeAdsPenalty : 0f);
        }

        /// <summary>Koşuyu bırakıp ateş edebilmek için gereken süre (sn): ağır silah daha geç hazır olur.</summary>
        public static float SprintToFireSeconds(float weightKg)
        {
            var w = float.IsNaN(weightKg) || weightKg < 0f ? 0f : weightKg;
            return SprintToFireBase + SprintToFirePerKg * w;
        }
    }
}
