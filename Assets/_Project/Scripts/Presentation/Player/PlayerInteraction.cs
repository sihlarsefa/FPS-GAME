using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Loot;
using Project.Infrastructure.Vehicles;
using UnityEngine;

namespace Project.Presentation.Player
{
    /// <summary>
    /// F etkileşimi ve istem metni. Öncelik: intikal aracından iniş → sürülen araçtan iniş → bakılan/yakındaki eşyayı alma
    /// → yakındaki boş araca binme. Hedef taraması 10 Hz; istem metni yalnızca hedef değişince yeniden kurulur.
    /// </summary>
    public sealed class PlayerInteraction
    {
        public const float LootLookDistance = 3f;
        public const float LootNearRadius = 1.4f;
        public const float VehicleEnterRadius = 4.5f;

        private const float ScanInterval = 0.1f;
        private static string _tag = string.Empty;
        private static string _reviveHint, _reviveHintTag, _reviveHintName;
        private static bool _reviveHintMedic;
        private static string KeyPrefix = "[F] ";
        private static string DisembarkPrompt = "[F] Araçtan in";
        private static string ExitVehiclePrompt = "[F] Araçtan in";
        private static string EnterVehiclePrompt = "[F] Kirpi'yi kullan";
        private const int MaxCountdownSeconds = 9;

        private static readonly Func<LootPickupComponent, bool> PickableFilter = IsPickable;

        /// <summary>"[F] Araçtan in (n)" — otomatik iniş geri sayımı; kare başına string üretmemek için önceden kurulur.</summary>
        private static string[] DisembarkCountdownPrompts = BuildCountdownPrompts();

        /// <summary>Etkileşim tuşu değiştiyse istem metinlerini güncel atamayla yeniden kurar.</summary>
        private static void RefreshTag()
        {
            var tag = Project.Infrastructure.Input.InputBindings.Bracket(BindAction.Interact);
            if (tag == _tag)
                return;
            _tag = tag;
            KeyPrefix = tag + " ";
            DisembarkPrompt = tag + " Araçtan in";
            ExitVehiclePrompt = DisembarkPrompt;
            EnterVehiclePrompt = tag + " Kirpi'yi kullan";
            EnterHeliPrompt = tag + " T-70'e bin";
            DisembarkCountdownPrompts = BuildCountdownPrompts();
        }

        private readonly PlayerController _owner;

        private float _nextScan;
        private LootPickupComponent _lootTarget;
        private DrivableVehicle _vehicleTarget;
        private Project.Infrastructure.Transport.FlyableHelicopter _heliTarget;
        private static string EnterHeliPrompt = "[F] T-70'e bin";

        private Infrastructure.World.WoodenDoor _doorTarget, _doorPress, _doorPeek;
        private float _doorPressAt;
        private const float DoorReach = 2.4f;
        private const float DoorPeekHoldSeconds = 0.3f;

        private LootPickupComponent _promptLoot;
        private string _promptLootText;
        private string _prompt = string.Empty;

        public PlayerInteraction(PlayerController owner)
        {
            _owner = owner;
        }

        /// <summary>Gösterilecek istem ("[F] ..."), yoksa boş.</summary>
        public string Prompt => _prompt;

        public LootPickupComponent LootTarget => _lootTarget;
        public DrivableVehicle VehicleTarget => _vehicleTarget;

        public void Tick(CombatInputState input, float dt)
        {
            RefreshTag();
            if (_owner.IsInTransport)
            {
                _lootTarget = null;
                _vehicleTarget = null;
                _promptLoot = null;
                _promptLootText = null;
                _prompt = _owner.CanDisembark ? DisembarkPromptFor(_owner.AutoDisembarkRemaining) : string.Empty;
                if (input.Interact && _owner.CanDisembark)
                    _owner.Disembark();
                return;
            }

            if (_owner.IsDriving)
            {
                _lootTarget = null;
                _vehicleTarget = null;
                _heliTarget = null;
                _prompt = ExitVehiclePrompt;
                if (input.Interact)
                    _owner.ExitVehicle();
                return;
            }

            if (TickRevive())
                return;

            if (input.Interact || Time.time >= _nextScan)
            {
                _nextScan = Time.time + ScanInterval;
                Scan();
            }

            if (TickDoor(input))
            {
                RefreshPrompt();
                return;
            }

            if (input.Interact)
                Interact();

            RefreshPrompt();
        }

        /// <summary>F kısa basış kapıyı açar/kapatır; basılı tutma (0,3 sn) aralık bakışı (peek), bırakınca kapanır.</summary>
        private bool TickDoor(CombatInputState input)
        {
            var held = _owner.GameplayInputActive && Infrastructure.Input.InputBindings.Held(BindAction.Interact);
            var pos = _owner.transform.position;
            try
            {
                if (_doorPeek != null)
                {
                    if (!held || !_doorPeek.IsIntact)
                    {
                        _doorPeek.EndPeek();
                        _doorPeek = null;
                    }

                    return true;
                }

                if (_doorPress != null)
                {
                    var door = _doorPress;
                    if (!held)
                    {
                        _doorPress = null;
                        door.Toggle(pos);
                    }
                    else if (Time.time - _doorPressAt >= DoorPeekHoldSeconds)
                    {
                        _doorPress = null;
                        if (door.BeginPeek(pos))
                            _doorPeek = door;
                    }

                    return true;
                }

                if (input.Interact && _doorTarget != null && _lootTarget == null && _vehicleTarget == null && _heliTarget == null)
                {
                    _doorPress = _doorTarget;
                    _doorPressAt = Time.time;
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _doorPress = null;
                _doorPeek = null;
            }

            return false;
        }

        private void ScanDoor()
        {
            _doorTarget = null;
            if (_lootTarget != null || _vehicleTarget != null || _heliTarget != null)
                return;
            if (Physics.Raycast(_owner.AimOrigin, _owner.AimForward, out var hit, DoorReach, Project.Infrastructure.GameLayers.LineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                var door = Infrastructure.World.WoodenDoor.FindOn(hit.collider);
                if (door != null && door.IsIntact)
                    _doorTarget = door;
            }
        }

        private Infrastructure.Combat.Combatant _revivee;

        /// <summary>Yakındaki yaralı müttefiki F basılı tutarak kaldırma. True ise bu kare başka etkileşim yapılmaz.</summary>
        private bool TickRevive()
        {
            var me = _owner.Combatant;
            if (me == null || !me.IsAlive || me.IsDowned)
            {
                StopRevive();
                return false;
            }

            if (_revivee != null && (!_revivee.IsAlive || !_revivee.IsDowned))
                StopRevive();

            var target = _revivee;
            if (target == null && Time.time >= _nextReviveScan)
            {
                _nextReviveScan = Time.time + 0.15f;
                target = FindDownedAlly(me);
            }
            else if (target != null && !InRange(me, target))
            {
                StopRevive();
                target = null;
            }

            if (target == null)
                return false;

            // Oyun girdisi kapalıyken (harita/envanter/konsol/çark) F basılı tutma kurtarmayı sürdürmez.
            var held = _owner.GameplayInputActive && Infrastructure.Input.InputBindings.Held(BindAction.Interact);
            if (held)
            {
                var service = Infrastructure.Combat.ReviveRuntime.Service;
                if (service.BeginRevive(me.Id, me.Role, target.Id))
                {
                    _revivee = target;
                    _prompt = ReviveText.Reviving(target.DisplayName, service.ReviveProgress(target.Id));
                    return true;
                }

                _prompt = string.Empty;
                return false;
            }

            StopRevive();
            RefreshTag();
            var medicRole = me.Role == TeamRole.Medic;
            if (_reviveHintTag != _tag || _reviveHintName != target.DisplayName || _reviveHintMedic != medicRole || _reviveHint == null)
            {
                _reviveHintTag = _tag;
                _reviveHintName = target.DisplayName;
                _reviveHintMedic = medicRole;
                _reviveHint = ReviveText.ReviveHint(_reviveHintName, medicRole, _tag);
            }
            _prompt = _reviveHint;
            _lootTarget = null;
            _vehicleTarget = null;
            _promptLoot = null;
            _promptLootText = null;
            return true;
        }

        private float _nextReviveScan;

        private static bool InRange(Infrastructure.Combat.Combatant a, Infrastructure.Combat.Combatant b)
        {
            var d = a.transform.position - b.transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= ReviveService.ReviveRange * ReviveService.ReviveRange;
        }

        private static Infrastructure.Combat.Combatant FindDownedAlly(Infrastructure.Combat.Combatant me)
        {
            Infrastructure.Combat.Combatant best = null;
            var bestSqr = ReviveService.ReviveRange * ReviveService.ReviveRange;
            var all = Infrastructure.Combat.CombatantRegistry.All;
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c == me || c.Team != me.Team || !c.IsAlive || !c.IsDowned)
                    continue;

                var d = c.transform.position - me.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude < bestSqr)
                {
                    bestSqr = d.sqrMagnitude;
                    best = c;
                }
            }

            return best;
        }

        private void StopRevive()
        {
            if (_revivee != null && _owner.Combatant != null)
                Infrastructure.Combat.ReviveRuntime.Service.CancelRevive(_owner.Combatant.Id);

            _revivee = null;
        }

        public void Clear()
        {
            StopRevive();
            _lootTarget = null;
            _vehicleTarget = null;
            _heliTarget = null;
            if (_doorPeek != null)
                _doorPeek.EndPeek();
            _doorTarget = _doorPress = _doorPeek = null;
            _promptLoot = null;
            _promptLootText = null;
            _prompt = string.Empty;
        }

        private void Scan()
        {
            _lootTarget = null;
            _vehicleTarget = null;
            _heliTarget = null;
            _doorTarget = null;

            var combatant = _owner.Combatant;
            if (combatant == null || !combatant.IsAlive)
                return;

            try
            {
                var origin = _owner.AimOrigin;
                var forward = _owner.AimForward;
                var loot = LootRegistry.FindLookTarget(origin, forward, LootLookDistance);
                if (loot == null)
                    loot = LootRegistry.FindNearest(_owner.transform.position, LootNearRadius, PickableFilter);

                if (loot != null && loot.IsAvailable)
                {
                    _lootTarget = loot;
                    return;
                }

                var vehicle = VehicleRegistry.FindNearest(_owner.transform.position, VehicleEnterRadius);
                if (vehicle != null && !vehicle.HasDriver && vehicle.Health > 0f)
                    _vehicleTarget = vehicle;

                var heli = Project.Infrastructure.Transport.FlyableHelicopter.FindNearest(_owner.transform.position,
                    Project.Infrastructure.Transport.FlyableHelicopter.EnterRadius);
                if (heli != null && (_vehicleTarget == null
                        || (heli.transform.position - _owner.transform.position).sqrMagnitude
                        < (_vehicleTarget.transform.position - _owner.transform.position).sqrMagnitude))
                {
                    _heliTarget = heli;
                    _vehicleTarget = null;
                }

                ScanDoor();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static bool IsPickable(LootPickupComponent pickup) => pickup != null && pickup.IsAvailable;

        private void Interact()
        {
            var combatant = _owner.Combatant;
            if (combatant == null || !combatant.IsAlive)
                return;

            if (_lootTarget != null && _lootTarget.IsAvailable)
            {
                PickUp(_lootTarget);
                return;
            }

            if (_heliTarget != null)
            {
                if (!_owner.TryEnterHeli(_heliTarget))
                    _owner.Notify("T-70'e binilemiyor", 1.5f);

                _heliTarget = null;
                return;
            }

            if (_vehicleTarget != null)
            {
                if (!_owner.TryEnterVehicle(_vehicleTarget))
                    _owner.Notify("Araca binilemiyor", 1.5f);

                _vehicleTarget = null;
            }
        }

        private void PickUp(LootPickupComponent pickup)
        {
            if (!Infrastructure.GameContext.HasAuthority)
                return;

            PickupResult result;
            try
            {
                result = pickup.PickupBy(_owner.Combatant);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return;
            }

            if (result.Accepted)
            {
                // Alma sesini LootPickupComponent.PickupBy zaten çalıyor (yerel oyuncu için 2D).
                if (!result.FullyTaken)
                    _owner.Notify("Envanterde yer kalmadı (bir kısmı alındı)", 1.8f);
            }
            else
            {
                _owner.Notify("Envanterde yer yok", 1.5f);
            }

            // Hedef değişmiş olabilir: hemen yeniden tara.
            _nextScan = 0f;
            Scan();
        }

        private void RefreshPrompt()
        {
            if (_lootTarget != null)
            {
                var text = _lootTarget.PromptText;
                if (!ReferenceEquals(_lootTarget, _promptLoot) || !ReferenceEquals(text, _promptLootText))
                {
                    _promptLoot = _lootTarget;
                    _promptLootText = text;
                    _prompt = FormatLootPrompt(text);
                }

                return;
            }

            _promptLoot = null;
            _promptLootText = null;
            _prompt = _heliTarget != null ? EnterHeliPrompt : (_vehicleTarget != null ? EnterVehiclePrompt : string.Empty);
            if (_prompt.Length == 0 && _doorTarget != null)
                _prompt = KeyPrefix + (_doorTarget.State == Infrastructure.World.DoorState.Open ? "Kapıyı kapat" : "Kapıyı aç (basılı tut: aralık)");
        }

        /// <summary>Eşya istemi: LootPickupComponent metni zaten "[F] ... al" biçimindedir; değilse tuş ön eki eklenir.</summary>
        private static string FormatLootPrompt(string text)
        {
            if (string.IsNullOrEmpty(text))
                return KeyPrefix + "Al";

            if (text.StartsWith("[F]", StringComparison.Ordinal))
                return _tag == "[F]" ? text : _tag + text.Substring(3);
            return text.StartsWith("[", StringComparison.Ordinal) ? text : KeyPrefix + text;
        }

        private static string DisembarkPromptFor(float remainingSeconds)
        {
            if (remainingSeconds < 0f)
                return DisembarkPrompt;

            var whole = Mathf.CeilToInt(remainingSeconds);
            if (whole <= 0)
                return DisembarkPrompt;

            return DisembarkCountdownPrompts[Mathf.Min(whole, MaxCountdownSeconds)];
        }

        private static string[] BuildCountdownPrompts()
        {
            var prompts = new string[MaxCountdownSeconds + 1];
            prompts[0] = DisembarkPrompt;
            for (var i = 1; i <= MaxCountdownSeconds; i++)
                prompts[i] = DisembarkPrompt + " (" + i + ")";

            return prompts;
        }
    }
}
