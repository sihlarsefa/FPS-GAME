using System;
using System.Collections.Generic;
using Project.Application.Services;
using Project.Infrastructure.Input;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// Atış yayı önizlemesi: bomba tuşu (G / sis tuşu) basılı tutulurken noktalı yörünge + iniş işareti.
    /// Girdi salt okunur (InputBindings.Held); atışı PlayerWeaponHandler yapar. Diğer çeşitler için
    /// <see cref="ForcedKind"/> atanabilir (flaş/molotof/aldatma tuşu bunu atar).
    /// </summary>
    public sealed class ThrowablePreview : MonoBehaviour
    {
        /// <summary>Başka bir sistem (ör. bomba tekerleği) önizlenecek türü doğrudan verebilir; null → tuşlara bakar.</summary>
        public static ThrowableKind? ForcedKind;

        private const int DotCount = 28;
        private const float Step = 0.045f;
        private const int MaxSteps = 90;
        private const float DotEvery = 2f;

        private static ThrowablePreview _instance;

        private readonly List<Vector3> _points = new(MaxSteps + 1);
        private readonly Transform[] _dots = new Transform[DotCount];
        private Transform _marker;
        private Material _material;
        private bool _shown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_instance != null)
                return;
            var go = new GameObject("[ThrowablePreview]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ThrowablePreview>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            ForcedKind = null;
        }

        private void Update()
        {
            if (!TryGetKind(out var kind) || !TryAim(out var start, out var velocity))
            {
                SetShown(false);
                return;
            }

            ThrowableRules.SimulateArc(start, velocity, Step, MaxSteps, _points);
            var endIndex = TrimAtHit(out var endPoint, out var normal);
            Ensure();
            SetShown(true);

            var used = 0;
            var spacing = DotEvery;
            var carry = 0f;
            for (var i = 1; i <= endIndex && used < DotCount; i++)
            {
                carry += 1f;
                if (carry < spacing)
                    continue;
                carry = 0f;
                _dots[used].position = _points[i];
                _dots[used].gameObject.SetActive(true);
                used++;
            }

            for (var i = used; i < DotCount; i++)
                _dots[i].gameObject.SetActive(false);

            _marker.position = endPoint + normal * 0.03f;
            _marker.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            var size = kind == ThrowableKind.Molotov ? ThrowableRules.FireRadius * 2f
                : kind == ThrowableKind.Smoke ? 1.2f
                : 0.7f;
            _marker.localScale = new Vector3(size, 0.01f, size);
            _marker.gameObject.SetActive(true);
        }

        private static bool TryGetKind(out ThrowableKind kind)
        {
            kind = ThrowableKind.Frag;
            var local = CombatantRegistry.LocalPlayer;
            if (local == null || !local.IsAlive || local.Inventory == null)
                return false;

            if (ForcedKind.HasValue)
                kind = ForcedKind.Value;
            else if (InputBindings.Held(BindAction.Grenade))
                kind = ThrowableKind.Frag;
            else if (InputBindings.Held(BindAction.Smoke))
                kind = ThrowableKind.Smoke;
            else
                return false;

            return local.Inventory.GetCount(ThrowableRules.ItemIdFor(kind)) > 0;
        }

        private static bool TryAim(out Vector3 start, out Vector3 velocity)
        {
            start = default;
            velocity = default;
            var cam = Camera.main;
            if (cam == null)
                return false;

            var t = cam.transform;
            var forward = t.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            right = right.sqrMagnitude > 1e-6f ? right.normalized : t.right;
            start = t.position + forward * 0.55f + right * 0.18f - Vector3.up * 0.12f;
            velocity = forward * ThrowableRules.PlayerThrowSpeed + Vector3.up * ThrowableRules.PlayerThrowUpBoost;
            return true;
        }

        /// <summary>Yayı ilk çarpmada keser; çarpma noktasını ve yüzey normalini döndürür. Dönüş: son nokta indeksi.</summary>
        private int TrimAtHit(out Vector3 endPoint, out Vector3 normal)
        {
            normal = Vector3.up;
            endPoint = _points[_points.Count - 1];
            for (var i = 1; i < _points.Count; i++)
            {
                var a = _points[i - 1];
                var b = _points[i];
                var delta = b - a;
                var len = delta.magnitude;
                if (len < 1e-4f)
                    continue;
                if (Physics.SphereCast(a, 0.05f, delta / len, out var hit, len, GameLayers.WorldMask | (1 << GameLayers.Vehicle),
                        QueryTriggerInteraction.Ignore))
                {
                    endPoint = hit.point;
                    normal = hit.normal;
                    _points[i] = hit.point;
                    return i;
                }
            }

            return _points.Count - 1;
        }

        private void Ensure()
        {
            if (_marker != null)
                return;

            Material mat = null;
            try
            {
                mat = MaterialLibrary.Unlit(new Color(1f, 0.85f, 0.35f));
            }
            catch (Exception)
            {
                // Malzeme kütüphanesi hazır değil.
            }

            _material = mat;
            for (var i = 0; i < DotCount; i++)
            {
                var d = MakePrimitive(PrimitiveType.Sphere, "Nokta" + i, mat);
                d.transform.localScale = Vector3.one * 0.045f;
                _dots[i] = d.transform;
            }

            var m = MakePrimitive(PrimitiveType.Cylinder, "Isaret", mat);
            _marker = m.transform;
        }

        private GameObject MakePrimitive(PrimitiveType type, string name, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var c = go.GetComponent<Collider>();
            if (c != null)
                Destroy(c);
            go.layer = GameLayers.IgnoreRaycast;
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<MeshRenderer>();
            if (r != null)
            {
                if (mat != null)
                    r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            go.SetActive(false);
            return go;
        }

        private void SetShown(bool on)
        {
            if (_shown == on)
                return;
            _shown = on;
            if (on || _marker == null)
                return;
            for (var i = 0; i < DotCount; i++)
            {
                if (_dots[i] != null)
                    _dots[i].gameObject.SetActive(false);
            }

            _marker.gameObject.SetActive(false);
        }
    }
}
