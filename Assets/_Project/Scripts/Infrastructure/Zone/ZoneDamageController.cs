using System;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using UnityEngine;

namespace Project.Infrastructure.Zone
{
    /// <summary>
    /// Harekât alanı (mavi bölge) dışındaki savaşanlara saniyede bir çevresel hasar uygular (yalnızca otoritede, maç
    /// Insertion/InMatch iken). Hedefler: canlı, hedef alınabilir ve yere inmiş (DropState.Landed) savaşanlar. Hasar
    /// <see cref="CombatService.ApplyEnvironmentalDamage"/> ile (zırh yok sayılır) verilir; yerel oyuncu için 2B bölge
    /// hasarı sesi çalınır.
    /// Eski kullanım: <see cref="Initialize"/> ile tek bir IDamageable'a (bu nesnenin konumunda) hasar verir.
    /// </summary>
    public sealed class ZoneDamageController : MonoBehaviour
    {
        /// <summary>Hasar aralığı (sn).</summary>
        public const float TickInterval = 1f;

        private const float MaxCatchUpSeconds = 3f;

        private IZoneService _zone;
        private CombatService _combat;
        private IMatchService _match;
        private IDamageable _legacyTarget;
        private float _accumulator;
        private bool _loggedError;

        public static ZoneDamageController Instance { get; private set; }

        /// <summary>Son tick'te yerel oyuncu bölge dışında mıydı?</summary>
        public bool LocalPlayerOutside { get; private set; }

        public static ZoneDamageController Create(IZoneService zone, CombatService combat, IMatchService match)
        {
            var controller = Instance;
            if (controller == null)
            {
                var go = new GameObject("ZoneDamageController");
                controller = go.AddComponent<ZoneDamageController>();
            }

            controller._zone = zone;
            controller._combat = combat;
            controller._match = match;
            controller._legacyTarget = null;
            controller._accumulator = 0f;
            return controller;
        }

        /// <summary>Eski API: bu bileşenin konumundaki tek bir hedefe bölge hasarı uygular.</summary>
        public void Initialize(IZoneService zoneService, IDamageable damageable, IMatchService matchService)
        {
            _zone = zoneService;
            _legacyTarget = damageable;
            _match = matchService;
            _accumulator = 0f;
        }

        private void Awake()
        {
            // Yalnızca global (Create ile) denetleyici tekildir; eski bileşen başına kullanım Instance'ı ezmez.
            if (Instance == null)
                Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!GameContext.HasAuthority)
                return;

            if (_zone == null)
            {
                if (!GameContext.TryGet(out _zone))
                    return;
            }

            _accumulator += Time.deltaTime;
            if (_accumulator < TickInterval)
                return;

            if (_accumulator > MaxCatchUpSeconds)
                _accumulator = MaxCatchUpSeconds;

            while (_accumulator >= TickInterval)
            {
                _accumulator -= TickInterval;
                try
                {
                    Tick(TickInterval);
                }
                catch (Exception e)
                {
                    if (!_loggedError)
                    {
                        _loggedError = true;
                        Debug.LogException(e, this);
                    }

                    _accumulator = 0f;
                    return;
                }
            }
        }

        private void Tick(float seconds)
        {
            LocalPlayerOutside = false;

            if (_match == null)
                GameContext.TryGet(out _match);

            // İntikal sürerken inmiş timler de bölge dışındaysa hasar alır; lobi/bitiş aşamalarında hasar yok.
            if (_match != null && _match.CurrentPhase != MatchPhase.InMatch && _match.CurrentPhase != MatchPhase.Insertion)
                return;

            if (!_zone.IsActive)
                return;

            if (_legacyTarget != null)
            {
                TickLegacy(seconds);
                return;
            }

            var combat = _combat ?? CombatContext.Combat;
            if (combat == null)
                return;

            var all = CombatantRegistry.All;
            for (var i = all.Count - 1; i >= 0; i--)
            {
                if (i >= all.Count)
                    continue;

                var c = all[i];
                if (c == null || !c.IsAlive || !c.IsTargetable || c.DropState != DropState.Landed)
                    continue;

                var position = c.transform.position;
                var dps = _zone.GetDamagePerSecond(position.x, position.z);
                if (dps <= 0f)
                    continue;

                if (c.IsLocalPlayer)
                {
                    LocalPlayerOutside = true;
                    PlayLocalSound();
                }

                combat.ApplyEnvironmentalDamage(c.Id, DamageSourceIds.Zone, dps * seconds);
            }
        }

        private void TickLegacy(float seconds)
        {
            if (!_legacyTarget.IsAlive)
                return;

            var position = transform.position;
            var dps = _zone.GetDamagePerSecond(position.x, position.z);
            if (dps <= 0f)
                return;

            LocalPlayerOutside = true;
            PlayLocalSound();
            _legacyTarget.ApplyDamage(new DamageInfo(dps * seconds, PlayerId.Invalid, DamageSourceIds.Zone));
        }

        private static void PlayLocalSound()
        {
            try
            {
                GameAudio.Play2D(SoundId.ZoneDamage, 0.7f, UnityEngine.Random.Range(0.95f, 1.05f));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
