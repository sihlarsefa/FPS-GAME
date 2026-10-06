using Project.Application.Services;
using System.Collections.Generic;
using Project.Infrastructure.Audio.Weapons;
using Project.Infrastructure.Audio.HdrMix;
using Project.Infrastructure.Combat;
using UnityEngine;

namespace Project.Infrastructure.Audio.Foley
{
    /// <summary>
    /// Bir savaşanın (oyuncu ya da bot) etkin silahını izleyip foley'i sürer: şarjör doldurma dizisi (başla/iptal), silah
    /// çek/kılıfla, ateş modu tıkı, boş tetik. <see cref="GearFoleyEmitter.Attach"/> tarafından Combatant olan nesnelere eklenir;
    /// oyuncu ve botlar aynı yolu kullanır, böylece tek tek çağrı noktası gerekmez. Ses kapalıyken (sunucu) hiçbir şey yapmaz.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponFoleyDriver : MonoBehaviour
    {
        private Combatant _combatant;
        private WeaponRuntimeService _weapon;
        private bool _seen;
        private bool _wasReloading;
        private int _ammoAtReload;
        private Project.Core.Domain.FireMode _mode;
        private AudioSource _chainSource;
        private bool _chainActive;

        // Prosedürel şarjör zinciri klip önbelleği (tactical + yarım-adım hız anahtarı). Hazır klip varsa hiç üretilmez.
        private static readonly Dictionary<int, AudioClip> ChainClips = new Dictionary<int, AudioClip>();

        /// <summary>Hazır şarjör klibi (magout/magin) yoksa prosedürel zincir (WeaponShotSynth) devreye girer.</summary>
        internal static bool NeedsProceduralChain(string weaponId)
        {
            var profile = WeaponFoley.ProfileFor(weaponId);
            return !WeaponClipLibrary.HasLayer(weaponId, profile.CaliberFolder, "magout")
                && !WeaponClipLibrary.HasLayer(weaponId, profile.CaliberFolder, "magin");
        }

        private static AudioClip ChainClip(bool tactical, float duration)
        {
            var baseLen = tactical ? 1.35f : 1.97f;
            var speed = Mathf.Clamp(baseLen / Mathf.Max(0.5f, duration), 0.5f, 2.5f);
            var key = (tactical ? 1 : 0) | (Mathf.RoundToInt(speed * 10f) << 1);
            if (ChainClips.TryGetValue(key, out var c) && c != null)
                return c;
            var data = WeaponShotSynth.RenderReloadChain(tactical, key >> 1 == 0 ? 1f : (key >> 1) / 10f);
            if (data == null || data.Length == 0)
                return null;
            c = AudioClip.Create("reloadchain_" + key, data.Length, 1, SynthDsp.SampleRate, false);
            c.SetData(data, 0);
            c.hideFlags = HideFlags.DontUnloadUnusedAsset;
            ChainClips[key] = c;
            return c;
        }

        private bool TryPlayChain(WeaponRuntimeService weapon)
        {
            try
            {
                if (!NeedsProceduralChain(weapon.WeaponId))
                    return false;
                var clip = ChainClip(weapon.CurrentAmmo > 0, weapon.ReloadDuration);
                if (clip == null)
                    return false;
                if (_chainSource == null)
                {
                    _chainSource = gameObject.AddComponent<AudioSource>();
                    _chainSource.playOnAwake = false;
                    _chainSource.dopplerLevel = 0f;
                    _chainSource.minDistance = 1.5f;
                    _chainSource.maxDistance = 25f;
                    MixerRouting.Route(_chainSource, MixChannel.Silah);
                }

                _chainSource.Stop();
                _chainSource.clip = clip;
                _chainSource.spatialBlend = IsLocal ? 0f : 1f;
                _chainSource.volume = IsLocal ? 0.8f : 0.65f;
                _chainSource.Play();
                _chainActive = true;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        private void StopChain()
        {
            if (_chainActive && _chainSource != null)
                _chainSource.Stop();
            _chainActive = false;
        }

        private bool IsLocal => _combatant != null && _combatant.IsLocalPlayer;
        private Vector3 Pos => transform.position + Vector3.up * 1.1f;

        public static WeaponFoleyDriver Attach(GameObject go)
        {
            if (go == null || go.GetComponent<Combatant>() == null)
                return null;
            var d = go.GetComponent<WeaponFoleyDriver>();
            return d != null ? d : go.AddComponent<WeaponFoleyDriver>();
        }

        private void Awake() => _combatant = GetComponent<Combatant>();

        private void OnDisable() => Detach(true);

        private void OnDestroy() => Detach(true);

        private void Detach(bool cancel)
        {
            if (_weapon != null)
                _weapon.DryFired -= OnDryFired;
            if (cancel)
            {
                WeaponFoley.CancelReload(transform);
                StopChain();
            }
            _wasReloading = false;
            _weapon = null;
            _seen = false; // yeniden etkinleşince ilk gözlem sayılır (çek sesi çalmaz)
        }

        private void Update()
        {
            if (_combatant == null || !WeaponFoley.Enabled)
                return;

            if (!_combatant.IsAlive || _combatant.IsDowned)
            {
                if (_wasReloading)
                {
                    WeaponFoley.CancelReload(transform);
                    StopChain();
                    _wasReloading = false;
                }

                return;
            }

            var inventory = _combatant.Inventory;
            var weapon = inventory != null ? inventory.ActiveWeapon : null;
            if (!ReferenceEquals(weapon, _weapon) || !_seen)
                OnWeaponChanged(weapon);

            if (weapon == null)
                return;

            var reloading = weapon.IsReloading;
            if (reloading && !_wasReloading)
            {
                _ammoAtReload = weapon.CurrentAmmo;
                if (!TryPlayChain(weapon))
                    WeaponFoley.BeginReload(weapon.WeaponId, weapon.ReloadDuration, weapon.CurrentAmmo <= 0, transform, IsLocal, Pos);
            }
            else if (!reloading && _wasReloading && weapon.CurrentAmmo <= _ammoAtReload)
            {
                // Mermi artmadan bitti: iptal (silah değişimi, koşu, tetik kesmesi).
                WeaponFoley.CancelReload(transform);
                StopChain();
            }

            _wasReloading = reloading;

            var mode = weapon.CurrentFireMode;
            if (mode != _mode)
            {
                _mode = mode;
                WeaponFoley.PlaySelector(weapon.WeaponId, Pos, IsLocal);
            }
        }

        private void OnWeaponChanged(WeaponRuntimeService weapon)
        {
            var first = !_seen;
            _seen = true;
            var previous = _weapon;
            if (previous != null)
                previous.DryFired -= OnDryFired;
            if (_wasReloading)
            {
                WeaponFoley.CancelReload(transform);
                StopChain();
            }

            _wasReloading = false;

            if (!first)
            {
                if (previous != null)
                    WeaponFoley.PlayHolster(previous.WeaponId, Pos, IsLocal);
                if (weapon != null)
                    WeaponFoley.PlayEquip(weapon.WeaponId, Pos, IsLocal);
            }

            _weapon = weapon;
            if (weapon != null)
            {
                weapon.DryFired += OnDryFired;
                _mode = weapon.CurrentFireMode;
            }
        }

        private void OnDryFired(WeaponRuntimeService w)
        {
            if (w != null && WeaponFoley.Enabled)
                WeaponFoley.PlayDryFire(w.WeaponId, Pos, IsLocal);
        }
    }
}
