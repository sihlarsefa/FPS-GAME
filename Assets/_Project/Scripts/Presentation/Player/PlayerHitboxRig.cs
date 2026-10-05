using Project.Core.Domain;
using Project.Infrastructure.Combat;
using UnityEngine;

namespace Project.Presentation.Player
{
    /// <summary>
    /// Yerel oyuncunun vuruş kutuları (baş, gövde, kollar, bacaklar). 1.8 m'lik ayakta duruş düzeninde kurulur;
    /// eğilince dikeyde ölçeklenir, yüzüstünde gövde yere yatırılır (baş önde), araçta oturur.
    /// Dönüşümler yalnızca duruş/boy değişince yazılır.
    /// </summary>
    public sealed class PlayerHitboxRig
    {
        private const float ReferenceHeight = PlayerController.ReferenceStandingHeight;
        private const float SeatedScale = 0.72f;
        private const float HeightEpsilon = 0.01f;

        private readonly Transform _root;
        private Stance _appliedStance = (Stance)(-1);
        private float _appliedHeight = -1f;
        private bool _appliedSeated;
        private bool _active = true;

        private PlayerHitboxRig(Transform root, Transform aimPoint)
        {
            _root = root;
            AimPoint = aimPoint;
        }

        /// <summary>Gövde merkezi (botların nişan noktası).</summary>
        public Transform AimPoint { get; }

        public Transform Root => _root;

        public static PlayerHitboxRig Build(Transform player, Combatant owner)
        {
            var rootGo = new GameObject("Hitboxes");
            rootGo.layer = Infrastructure.GameLayers.Hitbox;
            var root = rootGo.transform;
            root.SetParent(player, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            Hitbox.CreateSphere(root, owner, BodyPart.Head, new Vector3(0f, 1.63f, 0.03f), 0.125f);
            var chest = Hitbox.CreateBox(root, owner, BodyPart.Torso, new Vector3(0f, 1.27f, 0f), new Vector3(0.46f, 0.52f, 0.28f));
            Hitbox.CreateBox(root, owner, BodyPart.Torso, new Vector3(0f, 0.93f, 0f), new Vector3(0.38f, 0.22f, 0.25f));
            Hitbox.CreateCapsule(root, owner, BodyPart.Arm, new Vector3(-0.3f, 1.2f, 0.04f), 0.065f, 0.62f, 1);
            Hitbox.CreateCapsule(root, owner, BodyPart.Arm, new Vector3(0.3f, 1.2f, 0.04f), 0.065f, 0.62f, 1);
            Hitbox.CreateCapsule(root, owner, BodyPart.Leg, new Vector3(-0.11f, 0.42f, 0f), 0.085f, 0.86f, 1);
            Hitbox.CreateCapsule(root, owner, BodyPart.Leg, new Vector3(0.11f, 0.42f, 0f), 0.085f, 0.86f, 1);

            return new PlayerHitboxRig(root, chest != null ? chest.transform : root);
        }

        /// <summary>Duruşa ve kapsül boyuna göre vuruş kutularını günceller (değişiklik yoksa hiçbir şey yazmaz).</summary>
        public void Follow(Stance stance, float controllerHeight, bool seated)
        {
            if (_root == null)
                return;

            if (float.IsNaN(controllerHeight) || controllerHeight <= 0.1f)
                controllerHeight = ReferenceHeight;

            if (stance == _appliedStance && seated == _appliedSeated && Mathf.Abs(controllerHeight - _appliedHeight) < HeightEpsilon)
                return;

            _appliedStance = stance;
            _appliedSeated = seated;
            _appliedHeight = controllerHeight;

            if (seated)
            {
                _root.localRotation = Quaternion.identity;
                _root.localPosition = Vector3.zero;
                _root.localScale = new Vector3(1f, SeatedScale, 1f);
                return;
            }

            if (stance == Stance.Prone)
            {
                // +Y (baş yönü) ileriye bakar; gövde oyuncunun arkasında yerde yatar.
                _root.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _root.localPosition = new Vector3(0f, 0.2f, -1.45f);
                _root.localScale = Vector3.one;
                return;
            }

            var scaleY = Mathf.Clamp(controllerHeight / ReferenceHeight, 0.45f, 1.25f);
            _root.localRotation = Quaternion.identity;
            _root.localPosition = Vector3.zero;
            _root.localScale = new Vector3(1f, scaleY, 1f);
        }

        public void SetActive(bool active)
        {
            if (_root == null || _active == active)
                return;

            _active = active;
            _root.gameObject.SetActive(active);
        }
    }
}
