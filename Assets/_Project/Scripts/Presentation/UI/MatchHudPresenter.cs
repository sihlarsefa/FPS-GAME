using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure.Player;
using Project.Presentation.Player;
using UnityEngine;

namespace Project.Presentation.UI
{
    public sealed class MatchHudPresenter : MonoBehaviour
    {
        [SerializeField] private PlayerHealthComponent healthComponent;
        [SerializeField] private WeaponPresenter weaponPresenter;

        private IEventBus _eventBus;
        private IMatchService _matchService;
        private string _statusText = "Lobby";
        private string _lastHitText = string.Empty;
        private float _hitTextTimer;

        private void Start()
        {
            if (Bootstrap.GameBootstrap.Services == null)
                return;

            _eventBus = Bootstrap.GameBootstrap.Services.Resolve<IEventBus>();
            _matchService = Bootstrap.GameBootstrap.Services.Resolve<IMatchService>();

            _eventBus.Subscribe<MatchPhaseChangedEvent>(OnPhaseChanged);
            _eventBus.Subscribe<PlayerDamagedEvent>(OnDamaged);
            _eventBus.Subscribe<PlayerDiedEvent>(OnDied);
            _eventBus.Subscribe<WeaponFiredEvent>(OnWeaponFired);
            _eventBus.Subscribe<LootPickedUpEvent>(OnLootPickedUp);
        }

        private void Update()
        {
            if (_hitTextTimer > 0f)
                _hitTextTimer -= Time.deltaTime;
        }

        private void OnDestroy()
        {
            if (_eventBus == null)
                return;

            _eventBus.Unsubscribe<MatchPhaseChangedEvent>(OnPhaseChanged);
            _eventBus.Unsubscribe<PlayerDamagedEvent>(OnDamaged);
            _eventBus.Unsubscribe<PlayerDiedEvent>(OnDied);
            _eventBus.Unsubscribe<WeaponFiredEvent>(OnWeaponFired);
            _eventBus.Unsubscribe<LootPickedUpEvent>(OnLootPickedUp);
        }

        private void OnPhaseChanged(MatchPhaseChangedEvent evt) => _statusText = evt.CurrentPhase.ToString();

        private void OnDamaged(PlayerDamagedEvent evt)
        {
            Debug.Log($"[HUD] Player {evt.VictimId} took {evt.DamageAmount:F1} damage. HP: {evt.RemainingHealth:F1}");
        }

        private void OnDied(PlayerDiedEvent evt) => Debug.Log($"[HUD] Player {evt.VictimId} eliminated.");

        private void OnWeaponFired(WeaponFiredEvent evt)
        {
            if (!evt.Hit.HasHit)
                return;

            _lastHitText = evt.Hit.IsHeadshot ? "HEADSHOT!" : "HIT";
            _hitTextTimer = 1.25f;
        }

        private void OnLootPickedUp(LootPickedUpEvent evt)
        {
            _lastHitText = $"Picked up: {evt.Item.DisplayName}";
            _hitTextTimer = 2f;
        }

        private void OnGUI()
        {
            var health = healthComponent != null ? healthComponent.State.Current : 0f;
            var phase = _matchService != null ? _matchService.CurrentPhase.ToString() : _statusText;
            var alive = _matchService != null ? _matchService.AlivePlayerCount : 0;

            var weapon = weaponPresenter != null ? weaponPresenter.Weapon : null;
            var ammoText = weapon != null
                ? $"Ammo: {weapon.CurrentAmmo}/{weapon.MagazineSize}"
                : "Ammo: -";

            var reloadText = weapon != null && weapon.IsReloading ? " [RELOADING]" : string.Empty;

            GUI.Label(new Rect(16, 16, 480, 24), $"Phase: {phase}");
            GUI.Label(new Rect(16, 40, 480, 24), $"Health: {health:F0}");
            GUI.Label(new Rect(16, 64, 480, 24), $"Alive: {alive}");
            GUI.Label(new Rect(16, 88, 480, 24), ammoText + reloadText);
            GUI.Label(new Rect(16, Screen.height - 40, 480, 24), "F — Loot al | R — Reload | LMB — Ateş");

            if (_hitTextTimer > 0f)
            {
                var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
                GUI.Label(new Rect(Screen.width * 0.5f - 80f, Screen.height * 0.5f + 32f, 160, 30), _lastHitText, style);
            }

            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            GUI.Box(new Rect(center.x - 2, center.y - 2, 4, 4), GUIContent.none);
        }
    }
}
