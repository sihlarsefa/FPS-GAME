using System;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Presentation.Bootstrap
{
    /// <summary>
    /// Yerel oyuncunun olaylarını (emir, eşya, topçu, öldürme vb.) başarım metriklerine çevirir.
    /// Maç sonu metrikleri (kills, wins...) AchievementService.RecordMatch ile ayrıca işlenir; burada tekrarlanmaz.
    /// </summary>
    public sealed class AchievementTracker : IDisposable
    {
        public const float MultiKillWindowSeconds = 10f;

        private readonly IEventBus _bus;
        private readonly AchievementService _service;
        private readonly PlayerId _local;
        private readonly int _localTeam;
        private readonly Func<float> _clock;
        private readonly CareerService _career = GameSession.Progress; // madalya beslemesi (null-güvenli)
        private int _recentKills;
        private float _lastKillTime = -999f;

        public AchievementTracker(IEventBus bus, AchievementService service, PlayerId local, int localTeam, Func<float> clock = null)
        {
            _bus = bus;
            _service = service;
            _local = local;
            _localTeam = localTeam;
            _clock = clock ?? (() => UnityEngine.Time.time);
            if (_bus == null || _service == null)
                return;
            _bus.Subscribe<SquadOrderIssuedEvent>(OnOrder);
            _bus.Subscribe<ItemUsedEvent>(OnItem);
            _bus.Subscribe<ArtilleryStrikeEvent>(OnArtillery);
            _bus.Subscribe<PlayerDiedEvent>(OnDied);
            _bus.Subscribe<HitConfirmedEvent>(OnHit);
            _bus.Subscribe<LootPickedUpEvent>(OnLoot);
            _bus.Subscribe<RevivedEvent>(OnRevived);
        }

        public static AchievementTracker Create(IEventBus bus, PlayerId local, int localTeam)
            => new AchievementTracker(bus, GameSession.Achievements, local, localTeam);

        public void Dispose()
        {
            if (_bus == null || _service == null)
                return;
            _bus.Unsubscribe<SquadOrderIssuedEvent>(OnOrder);
            _bus.Unsubscribe<ItemUsedEvent>(OnItem);
            _bus.Unsubscribe<ArtilleryStrikeEvent>(OnArtillery);
            _bus.Unsubscribe<PlayerDiedEvent>(OnDied);
            _bus.Unsubscribe<HitConfirmedEvent>(OnHit);
            _bus.Unsubscribe<LootPickedUpEvent>(OnLoot);
            _bus.Unsubscribe<RevivedEvent>(OnRevived);
        }

        /// <summary>Silah kimliğinden "kills_*" metriği (yoksa null).</summary>
        public static string WeaponKillMetric(string weaponId)
        {
            switch (weaponId)
            {
                case WeaponIds.Mpt76: return "kills_ar_mpt76";
                case WeaponIds.Mpt55: return "kills_ar_mpt55";
                case WeaponIds.G3: return "kills_ar_g3a7";
                case WeaponIds.Knt76: return "kills_dmr_knt76";
                case WeaponIds.Jng90: return "kills_sr_jng90";
                case WeaponIds.Pmt76: return "kills_lmg_pmt76";
                case WeaponIds.Sar109: return "kills_smg_sar109t";
                case WeaponIds.Escort: return "kills_sg_escort";
                case WeaponIds.Sar9:
                case WeaponIds.Tp9: return "kills_pistol";
                default: return null;
            }
        }

        private void OnOrder(SquadOrderIssuedEvent e)
        {
            if (e.Team != _localTeam)
                return;
            _service.AddProgress("orders_any", 1);
            switch (e.Order)
            {
                case Project.Core.Domain.SquadOrder.Follow: _service.AddProgress("orders_follow", 1); break;
                case Project.Core.Domain.SquadOrder.HoldPosition: _service.AddProgress("orders_hold", 1); break;
                case Project.Core.Domain.SquadOrder.Attack: _service.AddProgress("orders_attack", 1); break;
                case Project.Core.Domain.SquadOrder.Regroup: _service.AddProgress("orders_rally", 1); break;
            }
        }

        private void OnItem(ItemUsedEvent e)
        {
            if (!e.UserId.Equals(_local) || !e.Completed)
                return;
            switch (e.ItemId)
            {
                case ItemIds.MedKit:
                case ItemIds.FirstAid:
                case ItemIds.Bandage:
                    _service.AddProgress("medkits_used", 1);
                    Career("heals", 1);
                    break;
                case ItemIds.SmokeGrenade:
                    _service.AddProgress("smoke_uses", 1);
                    break;
            }
        }

        private void OnArtillery(ArtilleryStrikeEvent e)
        {
            if (!e.IsImpact && e.CallerId.Equals(_local))
                _service.AddProgress("artillery_calls", 1);
        }

        private void OnHit(HitConfirmedEvent e)
        {
            // Yerel oyuncuya gelen darbeyi zırh emdiyse "zırh kullanımı" say.
            if (e.VictimId.Equals(_local) && e.ArmorAbsorbed)
                _service.AddProgress("armor_uses", 1);
        }

        /// <summary>Yerel oyuncunun müttefiki ayağa kaldırması ("revives"); kaldıransız (antrenman sıfırlaması) ve kendini sayılmaz.</summary>
        private void OnRevived(RevivedEvent e)
        {
            if (e.ReviverId.IsValid && e.ReviverId.Equals(_local) && !e.VictimId.Equals(_local))
            {
                _service.AddProgress("revives", 1);
                Career("revives", 1);
            }
        }

        private void Career(string metric, int amount)
        {
            try { _career?.AddProgress(metric, amount); }
            catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
        }

        private void OnLoot(LootPickedUpEvent e)
        {
            if (e.PlayerId.Equals(_local))
            {
                _service.AddProgress("loot_pickups", 1);
                Career("loot_picked", 1);
            }
        }

        private void OnDied(PlayerDiedEvent e)
        {
            if (!e.KillerId.Equals(_local) || e.VictimId.Equals(_local))
                return;

            var metric = WeaponKillMetric(e.WeaponId);
            if (metric != null)
                _service.AddProgress(metric, 1);
            if (e.WeaponId == DamageSourceIds.FragGrenade)
            {
                _service.AddProgress("grenade_kills", 1);
                Career("grenade_kills", 1);
            }
            if (e.WeaponId == DamageSourceIds.Artillery)
            {
                _service.AddProgress("artillery_hits", 1);
                Career("support_kills", 1);
            }
            if (e.WeaponId == DamageSourceIds.Vehicle)
                Career("vehicle_kills", 1);
            if (e.WeaponId == DamageSourceIds.Fists)
                Career("melee_kills", 1);
            if (WeaponCatalog.TryGet(e.WeaponId ?? "", out var wdef))
            {
                if (wdef.Category == WeaponCategory.Pistol) Career("pistol_kills", 1);
                else if (wdef.Category == WeaponCategory.Shotgun) Career("shotgun_kills", 1);
            }

            var now = _clock();
            _recentKills = now - _lastKillTime <= MultiKillWindowSeconds ? _recentKills + 1 : 1;
            _lastKillTime = now;
            if (_recentKills == 3)
                _service.AddProgress("multi_kills_3", 1);
        }
    }
}
