using System.Collections.Generic;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.AI;
using Project.Infrastructure.Combat;
using UnityEngine;

namespace Project.Infrastructure.Vehicles
{
    /// <summary>
    /// Kirpi mürettebatı: oyuncu (komutan) sürerken F1 Takip emriyle yakın tim botları yolcu olarak biner;
    /// araç durunca ya da Takip dışı emirde (F4 Toplan vb.) iner. Sürücü inerse hepsi iner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KirpiCrew : MonoBehaviour
    {
        public const float BoardRange = 40f;

        private readonly List<BotController> _crew = new(9);
        private DrivableVehicle _vehicle;
        private SquadOrderService _orders;
        private Combatant _lastDriver;
        private int _seenRevision;
        private bool _hasMoved;
        private float _stoppedFor;
        private string _message;

        public int CrewCount => _crew.Count;

        /// <summary>HUD için bekleyen mesaj (bir kez okunur).</summary>
        public string ConsumeMessage()
        {
            var m = _message;
            _message = null;
            return m;
        }

        private void Awake() => _vehicle = GetComponent<DrivableVehicle>();

        private void OnDisable() => ReleaseAll(false);

        private void Update()
        {
            if (_vehicle == null)
                return;

            Prune();

            var driver = _vehicle.Driver;
            if (driver == null || !driver.IsLocalPlayer || !driver.IsAlive || _vehicle.Health <= 0f)
            {
                _lastDriver = null;
                if (_crew.Count > 0)
                    ReleaseAll(false);
                return;
            }

            if (_orders == null)
                GameContext.TryGet(out _orders);
            if (_orders == null)
                return;

            var team = driver.Team;
            var revision = _orders.GetRevision(team);
            if (_lastDriver != driver)
            {
                _lastDriver = driver;
                _seenRevision = revision;   // biniş anındaki eski emir yeniden uygulanmaz
                _hasMoved = false;
                _stoppedFor = 0f;
            }

            if (revision != _seenRevision)
            {
                _seenRevision = revision;
                _orders.TryGetOrder(team, out var order, out _);
                var isFollow = order == SquadOrder.Follow;
                if (isFollow)
                    BoardNearby(driver);
                else if (_crew.Count > 0 && KirpiCrewRules.ShouldDisembarkOnOrder(false))
                {
                    ReleaseAll(false);
                    _message = "Takım Kirpi'den indi";
                }
            }

            if (_crew.Count == 0)
                return;

            var speed = _vehicle.SpeedKmh;
            if (speed > KirpiCrewRules.MovedKmh)
                _hasMoved = true;
            _stoppedFor = speed < KirpiCrewRules.StoppedKmh ? _stoppedFor + Time.deltaTime : 0f;
            if (KirpiCrewRules.ShouldDisembarkOnStop(_hasMoved, _stoppedFor))
            {
                ReleaseAll(false);
                _hasMoved = false;
                _message = "Araç durdu — takım indi";
            }
        }

        private void BoardNearby(Combatant driver)
        {
            var director = BotDirector.Instance;
            if (director == null)
                return;

            var bots = director.Bots;
            var boarded = 0;
            for (var i = 0; i < bots.Count; i++)
            {
                var bot = bots[i];
                if (bot == null || bot.Combatant == null || !bot.Combatant.IsAlive)
                    continue;
                if (bot.SquadLeader != driver)
                    continue;
                if ((bot.transform.position - transform.position).sqrMagnitude > BoardRange * BoardRange)
                    continue;
                if (!BotVehicleBoarding.TryBoard(bot, _vehicle, out _))
                    continue;
                _crew.Add(bot);
                boarded++;
            }

            if (boarded > 0)
            {
                _hasMoved = false;
                _stoppedFor = 0f;
                _message = boarded + " asker Kirpi'ye bindi";
            }
        }

        private void Prune()
        {
            for (var i = _crew.Count - 1; i >= 0; i--)
            {
                var bot = _crew[i];
                if (bot == null)
                {
                    _crew.RemoveAt(i);
                    continue;
                }

                var tag = bot.GetComponent<BotVehicleSeatTag>();
                var dead = bot.Combatant == null || !bot.Combatant.IsAlive;
                var downed = !dead && bot.Combatant.IsDowned;
                var slotLost = tag == null || tag.Vehicle != _vehicle || _vehicle.GetPassenger(tag.Seat) != bot.Combatant;
                if (DownedRules.ShouldUnboardPassenger(dead, downed, slotLost))
                {
                    BotVehicleBoarding.Unboard(bot, slotLost);
                    _crew.RemoveAt(i);
                }
            }
        }

        private void ReleaseAll(bool slotFreed)
        {
            for (var i = 0; i < _crew.Count; i++)
                BotVehicleBoarding.Unboard(_crew[i], slotFreed);
            _crew.Clear();
        }
    }
}
