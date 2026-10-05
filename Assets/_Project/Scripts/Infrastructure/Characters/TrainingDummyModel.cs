using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Characters
{
    /// <summary>
    /// Atış poligonu mankeni: çelik taban + kum torbaları üzerinde menteşeli, insan boyutunda (~1,8 m) kontrplak manken;
    /// göğsünde kırmızı-beyaz hedef halkaları. Menteşe (Hinge) devrilme ve isabet sallanması için döner; vuruş kutuları
    /// menteşeye bağlı olduğu için mankenle birlikte hareket eder. Kök ayak tabanındadır, +Z ön yüzdür.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrainingDummyModel : MonoBehaviour
    {
        private const float HingeHeight = 0.06f;

        private readonly List<Hitbox> _hitboxes = new List<Hitbox>(10);
        private readonly List<Renderer> _renderers = new List<Renderer>(32);
        private int _layer;
        private float _downAngle;
        private float _wobbleX;
        private float _wobbleZ;

        /// <summary>Devrilme menteşesi (taban üstü).</summary>
        public Transform Hinge { get; private set; }

        public Transform Head { get; private set; }
        public Transform EyePoint { get; private set; }
        public Transform AimPoint { get; private set; }
        public IReadOnlyList<Hitbox> Hitboxes => _hitboxes;

        /// <summary>Mankeni kurar (parent altında, yerel sıfır). owner verilirse vuruş kutuları ona bağlanır.</summary>
        public static TrainingDummyModel Build(Transform parent, Combatant owner, bool createHitboxes, int visualLayer)
        {
            var go = new GameObject("DummyModel");
            var layer = Mathf.Clamp(visualLayer, 0, 31);
            go.layer = layer;
            var t = go.transform;
            if (parent != null)
                t.SetParent(parent, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            var model = go.AddComponent<TrainingDummyModel>();
            model._layer = layer;
            model.Construct(owner, createHitboxes);
            return model;
        }

        private void Construct(Combatant owner, bool createHitboxes)
        {
            var plywood = MaterialLibrary.Lit(new Color(0.66f, 0.56f, 0.4f), 0.15f);
            var plywoodDark = MaterialLibrary.Lit(new Color(0.48f, 0.4f, 0.28f), 0.15f);
            var olive = MaterialLibrary.Lit(new Color(0.3f, 0.33f, 0.2f), 0.15f);
            var steel = MaterialLibrary.Get(MaterialId.MetalDark);
            var sandbag = MaterialLibrary.Get(MaterialId.Sandbag);
            var white = MaterialLibrary.Lit(new Color(0.92f, 0.9f, 0.85f), 0.1f);
            var red = MaterialLibrary.Lit(new Color(0.78f, 0.1f, 0.08f), 0.15f);

            // Taban (sabit)
            var root = transform;
            Part("BasePlate", root, CharacterMeshes.Box("dummyBase", Vector3.zero, new Vector3(0.62f, 0.05f, 0.5f)), steel, new Vector3(0f, 0.025f, 0f));
            Part("SandbagL", root, CharacterMeshes.Box("dummySandbag", Vector3.zero, new Vector3(0.36f, 0.12f, 0.2f)), sandbag,
                new Vector3(-0.2f, 0.09f, -0.28f), Quaternion.Euler(0f, 12f, 0f));
            Part("SandbagR", root, CharacterMeshes.Box("dummySandbag", Vector3.zero, new Vector3(0.36f, 0.12f, 0.2f)), sandbag,
                new Vector3(0.22f, 0.09f, -0.27f), Quaternion.Euler(0f, -8f, 0f));
            Part("HingeBlock", root, CharacterMeshes.Box("dummyHingeBlock", Vector3.zero, new Vector3(0.3f, 0.05f, 0.08f)), steel,
                new Vector3(0f, 0.07f, 0f), Quaternion.identity, false);

            // Menteşeli manken
            Hinge = Bone("Hinge", root, new Vector3(0f, HingeHeight, 0f));
            var h = Hinge;
            Part("Post", h, CharacterMeshes.Cylinder("dummyPost", 0f, 0.36f, 0.035f, 0.03f, 6, false), steel, Vector3.zero);
            Part("LegL", h, CharacterMeshes.Frustum("dummyLeg", 0.02f, 0.86f, new Vector2(0.11f, 0.1f), new Vector2(0.15f, 0.14f)), plywood,
                new Vector3(-0.1f, 0f, 0f));
            Part("LegR", h, CharacterMeshes.Frustum("dummyLeg", 0.02f, 0.86f, new Vector2(0.11f, 0.1f), new Vector2(0.15f, 0.14f)), plywood,
                new Vector3(0.1f, 0f, 0f));
            Part("Pelvis", h, CharacterMeshes.Frustum("dummyPelvis", 0.82f, 1.0f, new Vector2(0.32f, 0.18f), new Vector2(0.34f, 0.19f)), olive,
                Vector3.zero);
            Part("Torso", h, CharacterMeshes.Frustum("dummyTorso", 0.98f, 1.5f, new Vector2(0.34f, 0.2f), new Vector2(0.44f, 0.22f)), olive,
                Vector3.zero);
            Part("ArmL", h, CharacterMeshes.Frustum("dummyArm", -0.56f, 0f, new Vector2(0.08f, 0.08f), new Vector2(0.11f, 0.11f)), plywoodDark,
                new Vector3(-0.27f, 1.46f, 0f), Quaternion.Euler(0f, 0f, -6f));
            Part("ArmR", h, CharacterMeshes.Frustum("dummyArm", -0.56f, 0f, new Vector2(0.08f, 0.08f), new Vector2(0.11f, 0.11f)), plywoodDark,
                new Vector3(0.27f, 1.46f, 0f), Quaternion.Euler(0f, 0f, 6f));
            Part("Neck", h, CharacterMeshes.Cylinder("dummyNeck", 1.48f, 1.58f, 0.055f, 0.05f, 6, false), plywood, Vector3.zero);

            Head = Bone("Head", h, new Vector3(0f, 1.58f, 0f));
            Part("HeadShape", Head, CharacterMeshes.Ellipsoid("dummyHead", new Vector3(0f, 0.11f, 0f), new Vector3(0.1f, 0.12f, 0.11f), 8, 6), plywood,
                Vector3.zero);
            Part("HeadMark", Head, CharacterMeshes.Box("dummyHeadMark", Vector3.zero, new Vector3(0.08f, 0.02f, 0.01f)), red,
                new Vector3(0f, 0.12f, 0.106f), Quaternion.identity, false);
            EyePoint = Bone("Eye", Head, new Vector3(0f, 0.11f, 0.1f));

            // Göğüs hedef halkaları (öne bakan ince diskler).
            var face = Quaternion.Euler(90f, 0f, 0f);
            Part("Ring0", h, CharacterMeshes.Cylinder("dummyRing0", 0f, 0.008f, 0.16f, 0.16f, 16, true), white, new Vector3(0f, 1.26f, 0.112f), face, false);
            Part("Ring1", h, CharacterMeshes.Cylinder("dummyRing1", 0f, 0.008f, 0.12f, 0.12f, 16, true), red, new Vector3(0f, 1.26f, 0.116f), face, false);
            Part("Ring2", h, CharacterMeshes.Cylinder("dummyRing2", 0f, 0.008f, 0.08f, 0.08f, 14, true), white, new Vector3(0f, 1.26f, 0.12f), face, false);
            Part("Ring3", h, CharacterMeshes.Cylinder("dummyRing3", 0f, 0.008f, 0.035f, 0.035f, 10, true), red, new Vector3(0f, 1.26f, 0.124f), face, false);

            AimPoint = Bone("AimPoint", h, new Vector3(0f, 1.25f, 0f));

            if (createHitboxes)
            {
                Add(Hitbox.CreateSphere(Head, owner, BodyPart.Head, new Vector3(0f, 0.11f, 0f), 0.13f));
                Add(Hitbox.CreateBox(h, owner, BodyPart.Torso, new Vector3(0f, 1.24f, 0f), new Vector3(0.46f, 0.54f, 0.26f)));
                Add(Hitbox.CreateBox(h, owner, BodyPart.Torso, new Vector3(0f, 0.91f, 0f), new Vector3(0.36f, 0.2f, 0.22f)));
                Add(Hitbox.CreateCapsule(h, owner, BodyPart.Arm, new Vector3(-0.3f, 1.18f, 0f), 0.065f, 0.58f, 1));
                Add(Hitbox.CreateCapsule(h, owner, BodyPart.Arm, new Vector3(0.3f, 1.18f, 0f), 0.065f, 0.58f, 1));
                Add(Hitbox.CreateCapsule(h, owner, BodyPart.Leg, new Vector3(-0.1f, 0.44f, 0f), 0.08f, 0.86f, 1));
                Add(Hitbox.CreateCapsule(h, owner, BodyPart.Leg, new Vector3(0.1f, 0.44f, 0f), 0.08f, 0.86f, 1));

                var body = gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
            }
        }

        /// <summary>Devrilme açısı (0 = dik, 1 = yerde) ve isabet sallanması (derece) uygular.</summary>
        public void SetPose(float down01, float wobbleX, float wobbleZ)
        {
            _downAngle = -86f * Mathf.Clamp01(down01);
            _wobbleX = wobbleX;
            _wobbleZ = wobbleZ;
            if (Hinge != null)
                Hinge.localRotation = Quaternion.Euler(_downAngle + _wobbleX, 0f, _wobbleZ);
        }

        public void SetHitboxesEnabled(bool enabled)
        {
            for (var i = 0; i < _hitboxes.Count; i++)
            {
                var hb = _hitboxes[i];
                if (hb == null)
                    continue;

                if (hb.gameObject.layer != GameLayers.Hitbox)
                    hb.gameObject.layer = GameLayers.Hitbox;

                var c = hb.Collider;
                if (c != null && c.enabled != enabled)
                    c.enabled = enabled;
            }
        }

        public void SetVisible(bool visible)
        {
            for (var i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].enabled = visible;
            }
        }

        private void Add(Hitbox hitbox)
        {
            if (hitbox != null)
                _hitboxes.Add(hitbox);
        }

        private Transform Bone(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.layer = _layer;
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = Quaternion.identity;
            return t;
        }

        private void Part(string name, Transform parent, Mesh mesh, Material material, Vector3 localPosition)
        {
            Part(name, parent, mesh, material, localPosition, Quaternion.identity);
        }

        private void Part(string name, Transform parent, Mesh mesh, Material material, Vector3 localPosition, Quaternion localRotation,
            bool castShadows = true)
        {
            var go = new GameObject(name);
            go.layer = _layer;
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderers.Add(r);
        }
    }
}
