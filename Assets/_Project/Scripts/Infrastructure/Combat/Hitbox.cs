using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// Vücut bölgesi vuruş kutusu: <see cref="GameLayers.Hitbox"/> katmanında tetikleyici (trigger) collider.
    /// Mermiler (BallisticsSystem), yumruk ve tarayıcılar bunu bulup sahibine (Combatant) ve bölgeye (BodyPart) göre
    /// hasar uygular. Katman çarpışma matrisi vuruş kutularını fizik çarpışmalarından muaf tutar.
    /// Create* yardımcıları kemiğe (parent) bağlı bir alt nesne oluşturur; nesnenin konumu bölgenin merkezidir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Hitbox : MonoBehaviour
    {
        private Collider _collider;

        public Combatant Owner { get; private set; }
        public BodyPart Part { get; private set; }

        /// <summary>Bu vuruş kutusunun collider'ı (aynı nesnede).</summary>
        public Collider Collider
        {
            get
            {
                if (_collider == null)
                    _collider = GetComponent<Collider>();
                return _collider;
            }
        }

        /// <summary>Bölgenin dünya uzayındaki merkezi (nişan noktası).</summary>
        public Vector3 WorldCenter
        {
            get
            {
                var c = Collider;
                switch (c)
                {
                    case BoxCollider box:
                        return transform.TransformPoint(box.center);
                    case SphereCollider sphere:
                        return transform.TransformPoint(sphere.center);
                    case CapsuleCollider capsule:
                        return transform.TransformPoint(capsule.center);
                    default:
                        return c != null && c.enabled ? c.bounds.center : transform.position;
                }
            }
        }

        /// <summary>Kutuyu bir savaşana ve vücut bölgesine bağlar; collider'ı tetikleyici yapar, katmanı ayarlar.</summary>
        public void Bind(Combatant owner, BodyPart part)
        {
            if (Owner != null && Owner != owner)
                Owner.UnregisterHitbox(this);

            Owner = owner;
            Part = part;
            gameObject.layer = GameLayers.Hitbox;

            var c = Collider;
            if (c != null)
                c.isTrigger = true;

            if (owner != null)
                owner.RegisterHitbox(this);
        }

        public static Hitbox CreateBox(Transform parent, Combatant owner, BodyPart part, Vector3 localCenter, Vector3 size)
        {
            var go = CreateObject(parent, part, localCenter);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = new Vector3(Mathf.Max(0.01f, Mathf.Abs(size.x)), Mathf.Max(0.01f, Mathf.Abs(size.y)),
                Mathf.Max(0.01f, Mathf.Abs(size.z)));
            return Finish(go, box, owner, part);
        }

        public static Hitbox CreateSphere(Transform parent, Combatant owner, BodyPart part, Vector3 localCenter, float radius)
        {
            var go = CreateObject(parent, part, localCenter);
            var sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.center = Vector3.zero;
            sphere.radius = Mathf.Max(0.01f, radius);
            return Finish(go, sphere, owner, part);
        }

        /// <param name="direction">Kapsül ekseni: 0 = X, 1 = Y, 2 = Z (CapsuleCollider.direction).</param>
        public static Hitbox CreateCapsule(Transform parent, Combatant owner, BodyPart part, Vector3 localCenter, float radius,
            float height, int direction)
        {
            var go = CreateObject(parent, part, localCenter);
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.center = Vector3.zero;
            capsule.radius = Mathf.Max(0.01f, radius);
            capsule.height = Mathf.Max(capsule.radius * 2f, height);
            capsule.direction = Mathf.Clamp(direction, 0, 2);
            return Finish(go, capsule, owner, part);
        }

        private static GameObject CreateObject(Transform parent, BodyPart part, Vector3 localCenter)
        {
            var go = new GameObject("Hitbox_" + PartName(part));
            go.layer = GameLayers.Hitbox;
            var t = go.transform;
            if (parent != null)
                t.SetParent(parent, false);

            t.localPosition = localCenter;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            return go;
        }

        private static Hitbox Finish(GameObject go, Collider collider, Combatant owner, BodyPart part)
        {
            var hitbox = go.AddComponent<Hitbox>();
            hitbox._collider = collider;
            hitbox.Bind(owner, part);
            return hitbox;
        }

        private static string PartName(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Head: return "Head";
                case BodyPart.Arm: return "Arm";
                case BodyPart.Leg: return "Leg";
                default: return "Torso";
            }
        }

        private void OnDestroy()
        {
            if (Owner != null)
                Owner.UnregisterHitbox(this);
        }
    }
}
