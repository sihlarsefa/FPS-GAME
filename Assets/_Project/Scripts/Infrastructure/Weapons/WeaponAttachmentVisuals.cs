using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Weapons
{
    /// <summary>
    /// Silah modeline prosedürel eklenti parçaları takar (susturucu, alev gizleyici, kompansatör, dikey/açılı kabza, 8x dürbün, kırmızı nokta/holo, dürbün,
    /// el feneri gövdesi, şarjör büyütücü, dipçik). Parçalar AttachmentMeshes'ten gelir ve slot soketine (Muzzle/Sight/
    /// Grip_L/Magazine) takılır. Dünya/TP modelinde 30 m sonrası basit mesh'e düşer. Önceki parçaları temizleyip yeniden kurar.
    /// Nişan hizasına (SightAlignment) dokunmaz; yalnız SightPoint konumunu okur.
    /// </summary>
    public static class WeaponAttachmentVisuals
    {
        public const string RootName = "Attachments";
        public const string MagExtName = "ExtMagPart";

        /// <summary>Dünya / TP / yerdeki silah için (gölge açık, 30 m LOD).</summary>
        public static void ApplyWorld(WeaponModel model, IReadOnlyList<string> attachmentIds, int layer)
        {
            Apply(model, attachmentIds, layer, false);
        }

        public static void Apply(WeaponModel model, IReadOnlyList<string> attachmentIds, int layer, bool forViewmodel)
        {
            if (model == null)
                return;

            var old = model.transform.Find(RootName);
            if (old != null)
            {
                old.gameObject.SetActive(false);
                Object.Destroy(old.gameObject);
            }

            if (model.Magazine != null)
            {
                var oldMag = model.Magazine.Find(MagExtName);
                if (oldMag != null)
                {
                    oldMag.gameObject.SetActive(false);
                    Object.Destroy(oldMag.gameObject);
                }
            }

            if (attachmentIds == null || attachmentIds.Count == 0)
                return;

            var root = new GameObject(RootName) { layer = layer };
            root.transform.SetParent(model.transform, false);
            var lod = forViewmodel ? null : root.AddComponent<AttachmentLod>();

            for (var i = 0; i < attachmentIds.Count; i++)
            {
                var kind = AttachmentMeshes.KindOf(attachmentIds[i]);
                if (kind == AttachmentKind.None)
                    continue;

                if (kind == AttachmentKind.ExtMag)
                {
                    BuildMagExtension(model, layer, forViewmodel, lod);
                    continue;
                }

                var parts = AttachmentMeshes.Parts(kind);
                if (parts == null)
                    continue;

                var holder = new GameObject(kind.ToString()) { layer = layer };
                holder.transform.SetParent(root.transform, false);
                Place(model, kind, holder.transform);
                for (var p = 0; p < parts.Count; p++)
                    AddPart(model, holder.transform, parts[p], layer, forViewmodel, lod);
            }

            if (lod != null)
                lod.Bind();
        }

        private static void BuildMagExtension(WeaponModel model, int layer, bool forViewmodel, AttachmentLod lod)
        {
            var mag = model.Magazine;
            if (mag == null)
                return;

            var filter = mag.GetComponent<MeshFilter>();
            var bounds = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds : new Bounds(new Vector3(0f, -0.06f, 0f), new Vector3(0.03f, 0.12f, 0.05f));
            var parts = AttachmentMeshes.MagExtension(bounds);
            var holder = new GameObject(MagExtName) { layer = layer };
            holder.transform.SetParent(mag, false);
            for (var p = 0; p < parts.Count; p++)
                AddPart(model, holder.transform, parts[p], layer, forViewmodel, lod);
        }

        private static void AddPart(WeaponModel model, Transform parent, AttachmentPartMesh part, int layer, bool forViewmodel, AttachmentLod lod)
        {
            var go = new GameObject(part.Name) { layer = layer };
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = part.Detail;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GunMaterials.ForId(part.Material) ?? MaterialLibrary.Get(part.Material);
            renderer.shadowCastingMode = forViewmodel ? ShadowCastingMode.Off : ShadowCastingMode.On;
            renderer.receiveShadows = !forViewmodel;
            model.AddRenderer(renderer);
            if (lod != null)
                lod.Register(filter, part.Detail, part.Simple);
        }

        // ---------------------------------------------------------------- soketler

        private static void Place(WeaponModel model, AttachmentKind kind, Transform holder)
        {
            switch (AttachmentMeshes.SocketOf(kind))
            {
                case AttachmentSocket.Muzzle:
                    holder.localPosition = Local(model, Anchor(model, WeaponModel.MuzzleAnchor, model.Muzzle), new Vector3(0f, 0.035f, 0.6f));
                    break;
                case AttachmentSocket.Sight:
                    holder.localPosition = Local(model, Anchor(model, WeaponModel.SightAnchor, model.SightPoint), new Vector3(0f, 0.08f, 0f));
                    break;
                case AttachmentSocket.GripLeft:
                {
                    var t = Anchor(model, "Grip_L", model.LeftHandGrip);
                    if (t != null)
                    {
                        var p = model.transform.InverseTransformPoint(t.position);
                        holder.localPosition = new Vector3(0f, p.y + 0.01f, p.z);
                    }
                    else
                    {
                        holder.localPosition = new Vector3(0f, -0.005f, 0.32f);
                    }

                    break;
                }
                case AttachmentSocket.Rail:
                {
                    var m = Local(model, Anchor(model, WeaponModel.MuzzleAnchor, model.Muzzle), new Vector3(0f, 0.035f, 0.6f));
                    holder.localPosition = m + new Vector3(0f, -0.04f, -0.14f);
                    break;
                }
                default:
                    holder.localPosition = new Vector3(0f, 0.045f, -0.18f);
                    break;
            }
        }

        private static Vector3 Local(WeaponModel model, Transform t, Vector3 fallback)
        {
            return t != null ? model.transform.InverseTransformPoint(t.position) : fallback;
        }

        private static Transform Anchor(WeaponModel model, string name, Transform known)
        {
            if (known != null && known != model.transform)
                return known;
            return FindDeep(model.transform, name);
        }

        private static Transform FindDeep(Transform t, string name)
        {
            for (var i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (c.name == name || c.name == name + "_Anchor")
                    return c;
                var r = FindDeep(c, name);
                if (r != null)
                    return r;
            }

            return null;
        }
    }

    /// <summary>
    /// Dünya/TP eklentilerinde 30 m sonrası basit mesh'e geçer (yaklaşınca ayrıntılı). Yarım saniyede bir kamera
    /// uzaklığına bakar (kare başı iş yok), histerezisli (27/33 m), tahsissiz.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttachmentLod : MonoBehaviour
    {
        private readonly List<MeshFilter> _filters = new List<MeshFilter>(6);
        private readonly List<Mesh> _detail = new List<Mesh>(6);
        private readonly List<Mesh> _simple = new List<Mesh>(6);
        private bool _far;
        private float _next;

        internal void Register(MeshFilter filter, Mesh detail, Mesh simple)
        {
            _filters.Add(filter);
            _detail.Add(detail);
            _simple.Add(simple != null ? simple : detail);
        }

        internal void Bind()
        {
            _next = Time.time + Random.value * 0.5f;
        }

        /// <summary>Saf karar: uzak mı (histerezis).</summary>
        public static bool IsFar(bool wasFar, float distance)
        {
            return wasFar ? distance > AttachmentMeshes.LodDistance * 0.9f : distance > AttachmentMeshes.LodDistance * 1.1f;
        }

        private void Update()
        {
            if (Time.time < _next)
                return;

            _next = Time.time + 0.5f;
            var cam = Camera.main;
            if (cam == null)
                return;

            var far = IsFar(_far, Vector3.Distance(cam.transform.position, transform.position));
            if (far == _far)
                return;

            _far = far;
            for (var i = 0; i < _filters.Count; i++)
            {
                if (_filters[i] != null)
                    _filters[i].sharedMesh = far ? _simple[i] : _detail[i];
            }
        }
    }
}
