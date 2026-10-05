using System;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>
    /// Bir silah örneğinin çalışma zamanı durumu: şarjör, ateş modu, soğuma, şarjör değiştirme ve sekme (bloom).
    /// Otorite tarafında çalışır; sunucu atış isteklerini bu sınıfla doğrular.
    /// TryTrigger her karede çağrılmalıdır (burst devamı ve tampon atışlar bu çağrılarda üretilir).
    /// WeaponFiredEvent'i balistik sistemi (atış konumu ile) yayınlar; bu sınıf yalnızca şarjör olaylarını yayınlar.
    /// </summary>
    public sealed class WeaponRuntimeService : IWeaponRuntime
    {
        /// <summary>Saniyede sönen sekme (derece).</summary>
        public const float BloomDecayPerSecond = 4f;

        /// <summary>Nişan alırken birikmiş sekmenin etkisi.</summary>
        public const float AimBloomFactor = 0.5f;

        public const float CrouchSpreadFactor = 0.8f;
        public const float ProneSpreadFactor = 0.6f;
        public const float MoveSpreadFactor = 1.5f;
        public const float AirborneSpreadFactor = 2.5f;

        /// <summary>Soğuma sırasında basılan tek atış/burst tetiği bu süre kadar tamponlanır.</summary>
        public const float TriggerBufferSeconds = 0.12f;

        /// <summary>Burst bitiminde atış aralığına eklenen bekleme (aralık katı).</summary>
        public const float BurstRecoveryIntervals = 2f;

        private const float ReadyEpsilon = 1e-4f;

        private readonly IEventBus _eventBus;
        private readonly FireMode[] _fireModes;
        private readonly float _fireInterval;
        private readonly float _bloomRecoveryDelay;

        private int _ammo;
        private int _fireModeIndex;
        private float _cooldown;
        private float _bloom;
        private float _bloomHold;
        private bool _reloading;
        private float _reloadElapsed;
        private float _reloadDuration;
        private int _burstRemaining;
        private float _bufferedTrigger;
        private bool _equipping;

        public WeaponRuntimeService(WeaponDefinitionData definition, IEventBus eventBus, int loadedAmmo = -1)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _eventBus = eventBus;

            _fireModes = definition.FireModes != null && definition.FireModes.Length > 0
                ? (FireMode[])definition.FireModes.Clone()
                : new[] { FireMode.Single };
            if (definition.IsBoltAction)
                _fireModes = new[] { FireMode.Single };

            _fireInterval = definition.FireIntervalSeconds > 0f ? definition.FireIntervalSeconds : 0.1f;
            _bloomRecoveryDelay = Clamp(_fireInterval * 1.2f, 0.1f, 0.35f);
            _fireModeIndex = IndexOfDefaultMode();

            var magazine = MagazineSize;
            _ammo = loadedAmmo < 0 ? magazine : Math.Min(loadedAmmo, magazine);
            _reloadDuration = definition.ReloadDurationSeconds > 0f ? definition.ReloadDurationSeconds : 0f;
        }

        public WeaponDefinitionData Definition { get; }
        public string WeaponId => Definition.WeaponId;
        public WeaponCategory Category => Definition.Category;
        public int CurrentAmmo => _ammo;
        public int MagazineSize => Definition.MagazineSize > 0 ? Definition.MagazineSize : 0;
        public bool IsReloading => _reloading;
        public float ReloadProgress => _reloading && _reloadDuration > 0f ? Clamp(_reloadElapsed / _reloadDuration, 0f, 1f) : 0f;
        public bool CanFire => !_reloading && _ammo > 0 && IsCooldownReady;
        public FireMode CurrentFireMode => _fireModes[_fireModeIndex];

        /// <summary>
        /// AmmoSource'taki yedek mermi (IWeaponRuntime sözleşmesi: AmmoSource yoksa 0).
        /// Sınırsız kaynakta da 0 döner — HUD "∞" için <see cref="HasInfiniteReserve"/>, şarjör kararı için
        /// <see cref="CanReload"/> kullanmalıdır.
        /// </summary>
        public int ReserveAmmo
        {
            get
            {
                if (HasInfiniteReserve)
                    return 0;

                var reserve = AmmoSource.GetAmmo(Definition.AmmoType);
                return reserve < 0 ? 0 : reserve;
            }
        }

        /// <summary>Anlık birikmiş sekme (derece), atış başına artar, zamanla söner.</summary>
        public float CurrentBloom => _bloom;

        public PlayerId OwnerId { get; set; } = PlayerId.Invalid;

        /// <summary>Şarjör değiştirmede mermi çekilen kaynak (envanter). Null = sınırsız yedek.</summary>
        public IAmmoSource AmmoSource { get; set; }

        /// <summary>Desteklenen ateş modları (kopya değil; değiştirmeyin).</summary>
        public FireMode[] FireModes => _fireModes;

        /// <summary>Etkin atış aralığı (saniye).</summary>
        public float FireIntervalSeconds => _fireInterval;

        /// <summary>Sonraki atışa kalan süre (saniye, 0 = hazır).</summary>
        public float TimeUntilReady => _cooldown > 0f ? _cooldown : 0f;

        /// <summary>Sürgülü silahta mekanizma çekiliyor (atış sonrası soğuma).</summary>
        public bool IsCyclingBolt => Definition.IsBoltAction && !_equipping && _cooldown > ReadyEpsilon;

        /// <summary>BeginEquip sonrasında silah henüz hazır değil.</summary>
        public bool IsEquipping => _equipping;

        /// <summary>Devam eden burst var mı?</summary>
        public bool IsBurstActive => _burstRemaining > 0;

        /// <summary>Etkin şarjör değiştirme süresi (saniye).</summary>
        public float ReloadDuration => _reloadDuration;

        /// <summary>
        /// true (varsayılan): şarjörde mermi varken tetiğe yeni basış şarjör değiştirmeyi iptal edip ateş eder.
        /// Her karede "basıldı" gönderen yapay zekâ çağıranları false yapmalıdır.
        /// </summary>
        public bool TriggerInterruptsReload { get; set; } = true;

        /// <summary>Bu silahla yapılmış toplam atış.</summary>
        public int ShotsFired { get; private set; }

        /// <summary>Şarjör dolu değil ve yedek (ya da sınırsız kaynak) var.</summary>
        public bool CanReload => !_reloading && _ammo < MagazineSize && (HasInfiniteReserve || ReserveAmmo > 0);

        /// <summary>Her atış üretildiğinde tetiklenir.</summary>
        public event Action<WeaponRuntimeService> Fired;

        /// <summary>Boş şarjörle tetik çekildiğinde tetiklenir (klik sesi / otomatik şarjör değiştirme).</summary>
        public event Action<WeaponRuntimeService> DryFired;

        private bool IsCooldownReady => _cooldown <= ReadyEpsilon;

        /// <summary>Yedek mermi sınırsız (AmmoSource yok — antrenman/sınırsız mühimmat — ya da mühimmatsız silah).</summary>
        public bool HasInfiniteReserve => AmmoSource == null || Definition.AmmoType == AmmoType.None;

        public bool TryFire(out DamageInfo damage)
        {
            if (!CanFire)
            {
                damage = default;
                return false;
            }

            _burstRemaining = 0;
            _bufferedTrigger = 0f;
            ConsumeShot();
            damage = new DamageInfo(Definition.Damage, OwnerId, WeaponId);
            return true;
        }

        /// <summary>Anında şarjör doldurur (IWeapon uyumluluğu); kaynaktan mermi çeker.</summary>
        public void Reload()
        {
            _reloading = false;
            _reloadElapsed = 0f;
            _burstRemaining = 0;
            if (_ammo >= MagazineSize)
                return;

            FillMagazine();
        }

        public bool TryBeginReload()
        {
            if (_reloading || _ammo >= MagazineSize)
                return false;

            if (!HasInfiniteReserve && ReserveAmmo <= 0)
                return false;

            _reloading = true;
            _reloadElapsed = 0f;
            _burstRemaining = 0;
            _bufferedTrigger = 0f;
            _reloadDuration = Definition.ReloadDurationSeconds > 0f ? Definition.ReloadDurationSeconds : 0f;

            _eventBus?.Publish(new WeaponReloadStartedEvent(OwnerId, WeaponId, _reloadDuration));

            if (_reloadDuration <= 0f)
                CompleteReload();

            return true;
        }

        public void CancelReload()
        {
            if (!_reloading)
                return;

            _reloading = false;
            _reloadElapsed = 0f;
        }

        public void CycleFireMode()
        {
            if (_fireModes.Length <= 1)
                return;

            _fireModeIndex = (_fireModeIndex + 1) % _fireModes.Length;
            _burstRemaining = 0;
            _bufferedTrigger = 0f;
        }

        /// <summary>Belirli bir ateş modunu seçer (destekleniyorsa).</summary>
        public bool TrySetFireMode(FireMode mode)
        {
            for (var i = 0; i < _fireModes.Length; i++)
            {
                if (_fireModes[i] != mode)
                    continue;

                if (i != _fireModeIndex)
                {
                    _fireModeIndex = i;
                    _burstRemaining = 0;
                    _bufferedTrigger = 0f;
                }

                return true;
            }

            return false;
        }

        public bool TryTrigger(bool triggerHeld, bool triggerPressedThisFrame)
        {
            if (_reloading)
            {
                // Şarjörde mermi varken tetiğe basmak şarjör değiştirmeyi keser (TriggerInterruptsReload).
                if (!TriggerInterruptsReload || !triggerPressedThisFrame || _ammo <= 0)
                    return false;

                CancelReload();
            }

            if (_ammo <= 0)
            {
                _burstRemaining = 0;
                _bufferedTrigger = 0f;
                if (triggerPressedThisFrame)
                    DryFired?.Invoke(this);
                return false;
            }

            var mode = CurrentFireMode;

            // Devam eden burst tetikten bağımsız tamamlanır.
            if (_burstRemaining > 0)
            {
                if (!IsCooldownReady)
                    return false;

                FireBurstShot();
                return true;
            }

            var pressed = triggerPressedThisFrame || _bufferedTrigger > 0f;

            switch (mode)
            {
                case FireMode.Auto:
                    if (!(triggerHeld || triggerPressedThisFrame))
                        return false;
                    if (!IsCooldownReady)
                        return false;
                    _bufferedTrigger = 0f;
                    ConsumeShot();
                    return true;

                case FireMode.Burst:
                    if (!pressed)
                        return false;
                    if (!IsCooldownReady)
                    {
                        if (triggerPressedThisFrame)
                            _bufferedTrigger = TriggerBufferSeconds;
                        return false;
                    }

                    _bufferedTrigger = 0f;
                    _burstRemaining = Definition.BurstCount > 0 ? Definition.BurstCount : 1;
                    FireBurstShot();
                    return true;

                default:
                    if (!pressed)
                        return false;
                    if (!IsCooldownReady)
                    {
                        if (triggerPressedThisFrame)
                            _bufferedTrigger = TriggerBufferSeconds;
                        return false;
                    }

                    _bufferedTrigger = 0f;
                    ConsumeShot();
                    return true;
            }
        }

        public float GetSpreadAngle(bool aiming, float moveSpeedNormalized, bool grounded, Stance stance)
        {
            var baseSpread = aiming ? Definition.AdsSpread : Definition.HipSpread;
            if (float.IsNaN(baseSpread) || baseSpread < 0f)
                baseSpread = 0f;

            float stanceFactor;
            switch (stance)
            {
                case Stance.Crouching:
                    stanceFactor = CrouchSpreadFactor;
                    break;
                case Stance.Prone:
                    stanceFactor = ProneSpreadFactor;
                    break;
                default:
                    stanceFactor = 1f;
                    break;
            }

            var speed = float.IsNaN(moveSpeedNormalized) ? 0f : Clamp(moveSpeedNormalized, 0f, 1f);
            var moveFactor = 1f + MoveSpreadFactor * speed;
            var airFactor = grounded ? 1f : AirborneSpreadFactor;
            var bloom = aiming ? _bloom * AimBloomFactor : _bloom;

            return baseSpread * stanceFactor * moveFactor * airFactor + bloom;
        }

        /// <summary>
        /// Atış başına kamera sekmesi (derece). random01 ∈ [0,1] yatay yönü belirler.
        /// Nişan almak ve eğilmek/yatmak sekmeyi azaltır.
        /// </summary>
        public void GetRecoilKick(bool aiming, Stance stance, float random01, out float pitchDegrees, out float yawDegrees)
        {
            var factor = aiming ? 0.8f : 1f;
            if (stance == Stance.Crouching)
                factor *= 0.85f;
            else if (stance == Stance.Prone)
                factor *= 0.6f;

            var r = float.IsNaN(random01) ? 0.5f : Clamp(random01, 0f, 1f);
            pitchDegrees = Definition.RecoilVertical * factor;
            yawDegrees = (r * 2f - 1f) * Definition.RecoilHorizontal * factor;
        }

        public void SetLoadedAmmo(int ammo)
        {
            var magazine = MagazineSize;
            _ammo = ammo < 0 ? 0 : ammo > magazine ? magazine : ammo;
            if (_reloading && _ammo >= magazine)
                CancelReload();
            if (_ammo <= 0)
                _burstRemaining = 0;
        }

        /// <summary>
        /// Silahı ele alma: şarjör değiştirmeyi ve burst'ü iptal eder, EquipSeconds süresince ateşi engeller.
        /// </summary>
        public void BeginEquip()
        {
            Holster();
            var equip = Definition.EquipSeconds;
            if (float.IsNaN(equip) || equip <= 0f)
                return;

            _equipping = true;
            if (_cooldown < equip)
                _cooldown = equip;
        }

        /// <summary>Silah kılıfa/sırta alındı: şarjör değiştirme, burst ve tampon iptal edilir.</summary>
        public void Holster()
        {
            CancelReload();
            _burstRemaining = 0;
            _bufferedTrigger = 0f;
            _equipping = false;
        }

        /// <summary>Soğuma, sekme ve şarjör değiştirmeyi sıfırlar (yeniden doğma / antrenman).</summary>
        public void ResetState(bool refillMagazine)
        {
            Holster();
            _cooldown = 0f;
            _bloom = 0f;
            _bloomHold = 0f;
            if (refillMagazine)
                _ammo = MagazineSize;
        }

        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || deltaTime <= 0f)
                return;

            if (_cooldown > 0f)
            {
                // Bu karede hazır hale geldiyse taşan süre (negatif) korunur: sürekli atışta kare
                // kuantalamasından doğan gecikme bir sonraki atışta telafi edilir (RPM kare hızından bağımsız).
                _cooldown -= deltaTime;
            }
            else
            {
                // Bir karedir hazır bekliyor: boşta geçen süre biriktirilmez.
                _cooldown = 0f;
            }

            if (_equipping && _cooldown <= ReadyEpsilon)
                _equipping = false;

            if (_bufferedTrigger > 0f)
            {
                _bufferedTrigger -= deltaTime;
                if (_bufferedTrigger < 0f)
                    _bufferedTrigger = 0f;
            }

            if (_bloomHold > 0f)
            {
                var remaining = deltaTime - _bloomHold;
                _bloomHold -= deltaTime;
                if (_bloomHold < 0f)
                    _bloomHold = 0f;
                if (remaining > 0f)
                    DecayBloom(remaining);
            }
            else
            {
                DecayBloom(deltaTime);
            }

            if (_reloading)
            {
                _reloadElapsed += deltaTime;
                if (_reloadElapsed >= _reloadDuration)
                    CompleteReload();
            }
        }

        private void DecayBloom(float seconds)
        {
            if (_bloom <= 0f)
                return;

            _bloom -= BloomDecayPerSecond * seconds;
            if (_bloom < 0f)
                _bloom = 0f;
        }

        private void FireBurstShot()
        {
            _burstRemaining--;
            ConsumeShot();
            if (_burstRemaining <= 0 || _ammo <= 0)
            {
                _burstRemaining = 0;
                _cooldown += _fireInterval * BurstRecoveryIntervals;
            }
        }

        private void ConsumeShot()
        {
            _ammo--;
            ShotsFired++;
            _equipping = false;

            var carry = _cooldown < 0f ? _cooldown : 0f;
            var maxCarry = -_fireInterval * 0.5f;
            if (carry < maxCarry)
                carry = maxCarry;
            _cooldown = _fireInterval + carry;
            if (_cooldown < 0f)
                _cooldown = 0f;

            var bloomPerShot = Definition.BloomPerShot > 0f ? Definition.BloomPerShot : 0f;
            var maxBloom = Definition.MaxBloom > 0f ? Definition.MaxBloom : 0f;
            _bloom += bloomPerShot;
            if (_bloom > maxBloom)
                _bloom = maxBloom;
            _bloomHold = _bloomRecoveryDelay;

            Fired?.Invoke(this);
        }

        private void CompleteReload()
        {
            _reloading = false;
            _reloadElapsed = 0f;
            FillMagazine();
        }

        private void FillMagazine()
        {
            var needed = MagazineSize - _ammo;
            if (needed > 0)
            {
                int taken;
                if (HasInfiniteReserve)
                {
                    taken = needed;
                }
                else
                {
                    taken = AmmoSource.TakeAmmo(Definition.AmmoType, needed);
                    if (taken < 0)
                        taken = 0;
                    else if (taken > needed)
                        taken = needed;
                }

                _ammo += taken;
            }

            _eventBus?.Publish(new WeaponReloadedEvent(OwnerId, WeaponId, _ammo));
        }

        private int IndexOfDefaultMode()
        {
            for (var i = 0; i < _fireModes.Length; i++)
            {
                if (_fireModes[i] == FireMode.Auto)
                    return i;
            }

            return 0;
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
