using System;
using UnityEngine;

namespace Project.Infrastructure.Audio.Foley
{
    /// <summary>
    /// Askerin teçhizat sesleri: koşu tıkırtısı, plaka taşıyıcı, iniş (gövde). Konum farkından hız türetir; bir
    /// <see cref="CharacterController"/> varsa zemin durumunu ondan alır, yoksa kısa aşağı ışın kullanır.
    /// Başka koda bağlı değildir: <see cref="Attach"/> ile oyuncu/bot nesnesine eklenir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GearFoleyEmitter : MonoBehaviour
    {
        public bool IsLocal;
        public Func<string> WeaponIdProvider;

        private readonly GearRattleModel _model = new GearRattleModel();
        private CharacterController _cc;
        private Vector3 _last;
        private bool _hasLast;
        private bool _wasGrounded = true;
        private float _fallSpeed;
        private float _nextGroundCheck;
        private bool _grounded = true;
        private Project.Infrastructure.Combat.Combatant _combatant;
        private bool _wasProne;

        public static GearFoleyEmitter Attach(GameObject go, bool isLocal, Func<string> weaponId = null)
        {
            if (go == null)
                return null;
            var e = go.GetComponent<GearFoleyEmitter>();
            if (e == null)
                e = go.AddComponent<GearFoleyEmitter>();
            e.IsLocal = isLocal;
            e.WeaponIdProvider = weaponId;
            WeaponFoleyDriver.Attach(go); // Combatant varsa silah foley'i (şarjör/çek/kılıfla/seçici/boş tetik) de bağlanır
            return e;
        }

        /// <summary>Yüzüstü yatma geçişinde çağrılır.</summary>
        public void NotifyProne() => WeaponFoley.PlayProne(transform.position, IsLocal);

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _combatant = GetComponent<Project.Infrastructure.Combat.Combatant>();
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (!(dt > 0f) || !WeaponFoley.Enabled)
                return;
            if (_combatant != null)
            {
                var prone = _combatant.Stance == Project.Core.Domain.Stance.Prone;
                if (prone && !_wasProne)
                    NotifyProne();
                _wasProne = prone;
            }

            var pos = transform.position;
            if (!_hasLast)
            {
                _last = pos;
                _hasLast = true;
                return;
            }

            var delta = pos - _last;
            _last = pos;
            var speed = new Vector2(delta.x, delta.z).magnitude / dt;
            var vy = delta.y / dt;
            var grounded = CheckGrounded();

            if (!grounded && vy < 0f)
                _fallSpeed = Mathf.Max(_fallSpeed, -vy);
            if (grounded && !_wasGrounded)
            {
                WeaponFoley.PlayLanding(_fallSpeed, pos, IsLocal);
                _fallSpeed = 0f;
            }
            else if (grounded)
            {
                _fallSpeed = 0f;
            }

            _wasGrounded = grounded;

            if (_model.Tick(speed, dt, grounded, out var intensity, out var plate))
                WeaponFoley.Play(FoleyStep.SprintRattle, WeaponIdProvider != null ? WeaponIdProvider() : null, pos, IsLocal, intensity);
            if (plate)
                WeaponFoley.Play(FoleyStep.PlateCarrier, null, pos, IsLocal);
        }

        private bool CheckGrounded()
        {
            if (_cc != null)
                return _cc.isGrounded;
            if (Time.unscaledTime < _nextGroundCheck)
                return _grounded;
            _nextGroundCheck = Time.unscaledTime + (IsLocal ? 0.06f : 0.15f);
            _grounded = Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, 0.6f, ~0, QueryTriggerInteraction.Ignore);
            return _grounded;
        }

        private void OnDisable()
        {
            _model.Reset();
            _hasLast = false;
        }
    }
}
