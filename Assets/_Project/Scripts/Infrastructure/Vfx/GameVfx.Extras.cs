using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// GameVfx kalite eklentileri: silah sınıfına göre namlu alevi, kovan fırlatma, kan sisi + yer lekesi, patlama
    /// ekleri (enkaz, şok halkası, duman sütunu, kamera tozu), yoğun sis hacmi, rotor halkası, adım tozu.
    /// Hepsi null-güvenli, kalite kademesine göre kısılır ve havuz üst sınırlarına uyar.
    /// </summary>
    public static partial class GameVfx
    {
        private const float ShellCullDistance = 40f;
        private const float RotorRingInterval = 0.5f;
        private const float FootDustInterval = 0.08f;
        private const float CameraDustRange = 22f;

        private static ShellCasingPool _casings;
        private static float _nextRotorRing;
        private static float _nextFootDust;
        private static float _nextMuzzleSmoke;
        private static int _casingCapacityBuilt;
        private static FpGunfireVfx _fp;

        /// <summary>Silah sınıfına ve susturucuya göre namlu alevi (tabanca/ağır/susturuculu varyantlar).</summary>
        public static void MuzzleFlash(Vector3 position, Vector3 direction, float scale, WeaponCategory category, bool suppressed)
        {
            MuzzleFlash(position, direction, scale, category, suppressed ? MuzzleDevice.Suppressor : MuzzleDevice.None);
        }

        /// <summary>
        /// Cihaza göre şekilli alev: flash hider yıldız (ağır kart), susturucu küçük üfürme + duman. Alev 1-2 kare yaşar;
        /// FP ısı sisi, duman lifi ve duvar ışık sızması da burada beslenir.
        /// </summary>
        public static void MuzzleFlash(Vector3 position, Vector3 direction, float scale, WeaponCategory category, MuzzleDevice device)
        {
            var suppressed = device == MuzzleDevice.Suppressor;
            if (!IsFinite(position) || !EnsureReady() || IsBeyond(position, MuzzleCullDistance))
                return;

            scale = Sanitize(scale * Project.Infrastructure.Rendering.Atmosphere.MuzzleFlashBoost, 1f, 0.1f, 5f);
            var forward = SafeDirection(direction, Vector3.forward);
            var kind = MuzzleKindFor(category, suppressed);
            if (device == MuzzleDevice.FlashHider && kind == EffectKind.MuzzlePistol)
                kind = EffectKind.MuzzleFlash;
            Spawn(kind, position, SurfaceRotation(forward), suppressed ? 1f : scale * FpGunfireRules.FlashScale(device));
            if (suppressed)
            {
                var puff = FpGunfireRules.SuppressorPuffScale(device);
                if (puff > 0f && VfxQuality.AllowOptional(VfxQuality.Tier))
                    Spawn(EffectKind.MuzzleSmoke, position + forward * 0.1f, Quaternion.identity, puff);
            }
            _fp?.OnShot(position, forward);

            var smokeGap = VfxQuality.MuzzleSmokeInterval(VfxQuality.Tier);
            var nowSmoke = Time.unscaledTime;
            if (nowSmoke >= _nextMuzzleSmoke && smokeGap < 1000f && !IsBeyond(position, 120f))
            {
                _nextMuzzleSmoke = nowSmoke + smokeGap;
                Spawn(EffectKind.MuzzleSmoke, position + forward * 0.15f, Quaternion.identity, suppressed ? 0.6f : Mathf.Clamp(scale, 0.6f, 1.5f));
            }

            if (!suppressed)
                NotifySmokeFlash(position, 0.6f);

            if (suppressed)
                return;
            if (IsBeyond(position, MuzzleLightDistance))
            {
                // Uzak çatışma (bot LOD'undan bağımsız, izleyici mesafesine göre): gece ufukta şimşek gibi okunur.
                FireFarNightFlash(position, forward, scale, FpGunfireRules.FlashDuration(Time.unscaledDeltaTime, device));
                return;
            }

            // 1 karelik ışık: kısa süre, sınıfa göre şiddet; gece güçlü, kademe sınırlı.
            FireMuzzleLight(position, forward, scale, FpGunfireRules.FlashDuration(Time.unscaledDeltaTime, device));
        }

        private const float FarFlashMaxDistance = 450f;
        private const float FarFlashMinNight = 0.3f;

        /// <summary>Gece, ışık mesafesinin ötesindeki atışlar için menzili mesafeyle büyüyen tek karelik parlama (kısılmaz).</summary>
        private static void FireFarNightFlash(Vector3 position, Vector3 forward, float scale, float duration)
        {
            if (_muzzleLights == null || !_hasCamera)
                return;
            var night = NightNow();
            if (night < FarFlashMinNight)
                return;
            var dist = (position - _cameraPosition).magnitude;
            if (dist > FarFlashMaxDistance)
                return;
            var k = VfxNightRules.MuzzleLightScale(night, _rng.Value());
            var range = (4f + 3f * scale) * Mathf.Lerp(1f, 1.35f, night) + dist * 0.12f;
            var intensity = (2.2f + 1.4f * scale) * k * (1f + dist / 90f);
            _muzzleLights.Flash(position + forward * (0.12f * scale), MuzzleLightColor, intensity, range, duration,
                VfxNightRules.MuzzleLightCap(VfxQuality.Tier));
        }

        private static int _shotCounter;
        private static SmokeGlowPool _smokeGlow;
        private static ExplosionFx _explosionFx;

        /// <summary>Sinematik patlama katmanları (ExplosionFx) şu an oynatılabilir mi (gölgelendirici bulundu).</summary>
        public static bool ExplosionFxAvailable => _explosionFx != null && _explosionFx.Available;

        /// <summary>Etkin sinematik patlama örneği sayısı.</summary>
        public static int ActiveExplosionFx => _explosionFx != null ? _explosionFx.ActiveCount : 0;

        /// <summary>
        /// Sinematik patlamayı zemin normaliyle oynatır; gölgelendirici yoksa/yedek gerekirse false (eski parçacık efekti oynar).
        /// </summary>
        private static bool PlayExplosionFx(Vector3 position, float radius)
        {
            if (_explosionFx == null)
                return false;

            var normal = Vector3.up;
            try
            {
                if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out var hit, 0.5f + radius * 0.6f,
                        GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
                    normal = SafeDirection(hit.normal, Vector3.up);
            }
            catch { }

            return _explosionFx.TryPlay(position, radius, normal, NightNow(), VfxQuality.Tier);
        }

        /// <summary>Gece oranı 0..1 (Atmosphere'den; hata olursa 0).</summary>
        private static float NightNow()
        {
            try { return VfxNightRules.NightFactor(Project.Infrastructure.Rendering.Atmosphere.CurrentTime); }
            catch { return 0f; }
        }

        /// <summary>Namlu ışığı: titreşimli şiddet, gece çarpanı, kademe başına eşzamanlı sınır.</summary>
        private static void FireMuzzleLight(Vector3 position, Vector3 forward, float scale, float duration)
        {
            if (_muzzleLights == null)
                return;

            var night = NightNow();
            var k = VfxNightRules.MuzzleLightScale(night, _rng.Value());
            var intensity = (2.2f + 1.4f * scale) * k;
            var range = (4f + 3f * scale) * Mathf.Lerp(1f, 1.35f, night);
            var spill = WallSpillNow(position, forward);
            intensity *= 1f + 0.6f * spill;
            range *= 1f + 0.25f * spill;
            _muzzleLights.Flash(position + forward * (0.12f * scale), MuzzleLightColor, intensity, range, duration,
                VfxNightRules.MuzzleLightCap(VfxQuality.Tier));
        }

        /// <summary>Namlu önündeki duvara yakınlığa göre ışık sızması 0..1 (yalnız kameraya yakın atışlarda raycast).</summary>
        private static float WallSpillNow(Vector3 position, Vector3 forward)
        {
            if (!_hasCamera || !VfxQuality.AllowOptional(VfxQuality.Tier))
                return 0f;
            if ((position - _cameraPosition).sqrMagnitude > 9f)
                return 0f;
            try
            {
                if (Physics.Raycast(position, forward, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                    return FpGunfireRules.WallSpill(hit.distance);
            }
            catch { }
            return 0f;
        }

        private static void RegisterSmokeCloud(Vector3 position, float radius, float duration)
        {
            _smokeGlow?.RegisterCloud(position, radius, duration, Time.unscaledTime);
        }

        private static void NotifySmokeFlash(Vector3 position, float strength)
        {
            if (_smokeGlow == null || _smokeGlow.CloudCount == 0 || !VfxQuality.AllowOptional(VfxQuality.Tier))
                return;
            _smokeGlow.NotifyFlash(position, strength, NightNow(), Time.unscaledTime);
        }

        internal static EffectKind MuzzleKindFor(WeaponCategory category, bool suppressed)
        {
            if (suppressed)
                return EffectKind.MuzzleSuppressed;

            switch (category)
            {
                case WeaponCategory.Pistol:
                case WeaponCategory.Smg:
                    return EffectKind.MuzzlePistol;
                case WeaponCategory.Sniper:
                case WeaponCategory.Dmr:
                case WeaponCategory.Lmg:
                case WeaponCategory.Shotgun:
                    return EffectKind.MuzzleHeavy;
                default:
                    return EffectKind.MuzzleFlash;
            }
        }

        /// <summary>
        /// Havuzlu fiziksel kovan: atış yönünün sağ-yukarısına fırlar. Kovan sesi atış sesinde zaten gecikmeli çalar;
        /// yalnızca pompalı (sesi orada yok) fiziksel ilk zıplamada tınlar.
        /// </summary>
        public static void EjectShell(Vector3 muzzle, Vector3 aim, WeaponCategory category)
        {
            if (category == WeaponCategory.Melee || category == WeaponCategory.None)
                return;
            if (!IsFinite(muzzle) || !EnsureReady() || _casings == null || IsBeyond(muzzle, ShellCullDistance))
                return;

            var forward = SafeDirection(aim, Vector3.forward);
            var right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 1e-4f)
                right = Vector3.right;
            right.Normalize();
            var up = Vector3.Cross(forward, right).normalized;

            var velocity = right * _rng.Range(1.6f, 3f) + up * _rng.Range(1.2f, 2.4f) - forward * _rng.Range(0f, 0.6f);
            var size = category == WeaponCategory.Pistol || category == WeaponCategory.Smg ? 0.8f
                : category == WeaponCategory.Shotgun ? 1.8f
                : category == WeaponCategory.Sniper || category == WeaponCategory.Lmg || category == WeaponCategory.Dmr ? 1.4f
                : 1f;
            // FP: kameraya yakın kovan biraz daha yüksek ve belirgin bir yay çizer.
            if (_hasCamera && (muzzle - _cameraPosition).sqrMagnitude < 4f)
                velocity += up * 0.5f + right * 0.3f;
            _casings.Eject(muzzle - forward * 0.1f, velocity, size, category == WeaponCategory.Shotgun);
            if (_hasCamera && (muzzle - _cameraPosition).sqrMagnitude < 25f)
                _fp?.SpawnGlint(muzzle - forward * 0.1f, velocity, VfxQuality.Tier);
        }

        /// <summary>Kan sisi + (olasılıkla) yere kan lekesi.</summary>
        private static void BloodExtras(Vector3 point, Vector3 normal)
        {
            Spawn(EffectKind.BloodMist, point, SurfaceRotation(normal), 1f);

            if (IsBeyond(point, DecalCullDistance))
                return;

            var chance = VfxQuality.Tier == VfxTier.Low ? 0.2f : 0.45f;
            if (_rng.Value() > chance)
                return;

            if (Physics.Raycast(point, Vector3.down, out var hit, 2.6f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
            {
                var n = SafeDirection(hit.normal, Vector3.up);
                var size = _rng.Range(0.3f, 0.65f);
                _decals.Place(hit.point + n * DecalOffset, n, size, _rng.Range(0f, 360f), VfxMaterials.DecalBlood, AttachTarget(hit.collider));
            }
        }

        /// <summary>Patlama ekleri: enkaz (alt yayıcı duman izi), yatay şok halkası, kalıcı duman sütunu, kamera tozu.</summary>
        private static void ExplosionExtras(Vector3 position, float radius, float scale)
        {
            Spawn(EffectKind.ExplosionDebris, position + Vector3.up * 0.3f, Quaternion.identity, scale);
            Spawn(EffectKind.ShockRing, position + Vector3.up * 0.2f, Quaternion.identity, scale);

            if (VfxQuality.AllowOptional(VfxQuality.Tier) && !IsBeyond(position, 250f))
                Spawn(EffectKind.ExplosionColumn, position + Vector3.up * 0.4f, Quaternion.identity, scale);

            if (_hasCamera && _cameraTransform != null)
            {
                var range = CameraDustRange + radius * 1.5f;
                if ((_cameraPosition - position).sqrMagnitude < range * range)
                    Spawn(EffectKind.CameraDust, _cameraPosition + _cameraTransform.forward * 1.5f, Quaternion.identity, 3f);
            }
        }

        /// <summary>Sis bombasına yoğun hacim: ilk patlama + şişen gövde (SmokeCloud ile aynı süre/ömür).</summary>
        private static void SmokeBillowExtras(Vector3 position, float scale, float emitSeconds, float lifeMin, float lifeMax, float busy)
        {
            var pool = Pool(EffectKind.SmokeBillow);
            var ps = pool != null ? pool.Prepare(position + Vector3.up * (0.4f * scale), Quaternion.identity, scale, busy) : null;
            if (ps == null)
                return;

            var main = ps.main;
            main.duration = emitSeconds;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            var emission = ps.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(70f / ((lifeMin + lifeMax) * 0.5f) * VfxQuality.CountFactor);
            ps.Play(true);
        }

        /// <summary>Rotor akışı: yere paralel genişleyen toz halkası + toz perdesi. <paramref name="radius"/> m.</summary>
        public static void RotorWash(Vector3 position, float radius)
        {
            if (!IsFinite(position) || !EnsureReady() || IsBeyond(position, DustCullDistance))
                return;

            radius = Sanitize(radius, 5f, 1f, 30f);
            if (!VfxQuality.RotorDustAllowed(HeightAboveGround(position)))
                return;
            if (WaterLevelSource.TryGet(out var level) && position.y < level + 0.3f && position.y > level - 3f)
            {
                WaterSplash(new Vector3(position.x, level, position.z), radius * 0.2f);
                return;
            }

            if (GpuVfx.TryPlay(GpuVfxEffect.RotorDust, position, Vector3.up, radius))
                return;

            if (VfxQuality.AllowOptional(VfxQuality.Tier))
                Spawn(EffectKind.RotorRing, position + Vector3.up * 0.1f, Quaternion.identity, radius / 5f);
        }

        /// <summary>Zemine (veya su yüzeyine) dikey mesafe; zemin bulunamazsa sonsuz (toz çıkmaz).</summary>
        private static float HeightAboveGround(Vector3 position)
        {
            try
            {
                if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out var hit, VfxQuality.RotorDustMaxHeight + 1f,
                        ~0, QueryTriggerInteraction.Ignore))
                    return Mathf.Max(0f, position.y - hit.point.y);
            }
            catch { }
            return WaterLevelSource.TryGet(out var level) ? Mathf.Max(0f, position.y - level) : float.PositiveInfinity;
        }

        /// <summary>Koşu/yürüyüş adım tozu: toprakta toz, karda kar tozu; diğer yüzeylerde yok.</summary>
        public static void FootstepDust(Vector3 position, SurfaceKind surface, float intensity)
        {
            if (!IsFinite(position) || (surface != SurfaceKind.Dirt && surface != SurfaceKind.Snow))
                return;
            if (!EnsureReady() || IsBeyond(position, 60f))
                return;

            var now = Time.unscaledTime;
            if (now < _nextFootDust)
                return;
            _nextFootDust = now + FootDustInterval;

            Spawn(surface == SurfaceKind.Snow ? EffectKind.FootSnow : EffectKind.FootDust,
                position + Vector3.up * 0.04f, Quaternion.identity, Sanitize(intensity, 1f, 0.3f, 2f));
        }

        private static void BuildExtras(Transform root)
        {
            var container = CreateChild(root, "Casings", true);
            _casingCapacityBuilt = VfxQuality.CasingCap(VfxQuality.Tier);
            _casings = new ShellCasingPool(container, _casingCapacityBuilt);
            _smokeGlow = new SmokeGlowPool(CreateChild(root, "SmokeGlow", true));
            _fp = new FpGunfireVfx(CreateChild(root, "FpGunfire", true));
            _explosionFx = new ExplosionFx(CreateChild(root, "ExplosionFx", true));
        }

        private static void ResetExtras()
        {
            _casings = null;
            _smokeGlow = null;
            _fp = null;
            _explosionFx = null;
            FpGunfireVfx.ResetCache();
            _nextRotorRing = 0f;
            _nextFootDust = 0f;
            ShellCasingPool.ResetCache();
            VfxDecalVariants.ResetCache();
        }

        private static void TickExtras(float deltaTime)
        {
            _casings?.Tick(deltaTime);
            _smokeGlow?.Tick(deltaTime, _hasCamera, _cameraPosition, Time.unscaledTime);
            _fp?.Tick(deltaTime, _hasCamera, _cameraPosition, VfxQuality.Tier);
            _explosionFx?.Tick(deltaTime, _hasCamera, _cameraPosition,
                _cameraTransform != null ? _cameraTransform.rotation : Quaternion.identity);
        }

        private static void ClearExtras()
        {
            _casings?.Clear();
            _smokeGlow?.Clear();
            _fp?.Clear();
            _explosionFx?.Clear();
        }

        /// <summary>Aynı kovan (kovan sayısı) test/ayar için.</summary>
        public static int ActiveCasings => _casings != null ? _casings.ActiveCount : 0;

        private static bool RotorRingDue(float scale)
        {
            if (scale < 1.4f)
                return false;
            var now = Time.unscaledTime;
            if (now < _nextRotorRing)
                return false;
            _nextRotorRing = now + RotorRingInterval;
            return true;
        }
    }
}
