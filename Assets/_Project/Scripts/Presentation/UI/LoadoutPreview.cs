using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Infrastructure.Weapons;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Donanım ekranının dönen 3B silah önizlemesi: sahneden çok uzakta (4000 m) küçük bir stüdyo kurar
    /// (kamera + anahtar/ışık/kontur noktası ışıkları), RenderTexture'ı RawImage'a basar. Silah eksenden döner;
    /// sürükleyince elle çevrilir, bırakınca 1,5 sn sonra yeniden döner. Yok edilince her şeyi temizler.
    /// </summary>
    public sealed class LoadoutPreview : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private static readonly Vector3 StudioOrigin = new Vector3(4000f, 4000f, 4000f);
        private const float DegreesPerSecond = 16f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private RenderTexture _texture;
        private Camera _camera;
        private Transform _studio;
        private Transform _pivot;
        private GameObject _model;
        private float _yaw = 62f;
        private float _resumeAt;
        private bool _dragging;
        private float _distance = 1.6f;
        private WeaponModel _weaponModel;
        private Bounds _localBounds;
        private bool _hasBounds;
        private Transform _pedestal;
        private Material _pedestalMat;
        private MaterialPropertyBlock _block;
        private readonly List<Renderer> _tintRenderers = new List<Renderer>(16);
        private readonly List<Color> _tintOriginal = new List<Color>(16);

        /// <summary>Önizleme kamerası var mı (batch modda yok).</summary>
        public bool IsLive => _camera != null;

        /// <summary>Yaw (derece), yalnızca test/okuma.</summary>
        public float Yaw => _yaw;

        /// <summary>Hedef RawImage ile önizlemeyi kurar.</summary>
        public static LoadoutPreview Create(RawImage target, int width = 960, int height = 540)
        {
            var preview = target.gameObject.AddComponent<LoadoutPreview>();
            preview.Build(target, width, height);
            return preview;
        }

        private void Build(RawImage target, int width, int height)
        {
            if (UnityEngine.Application.isBatchMode)
                return;

            _texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "HK_LoadoutPreview",
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear
            };
            _texture.Create();
            target.texture = _texture;
            target.color = Color.white;

            var root = new GameObject("[Donanım Önizleme Stüdyosu]");
            _studio = root.transform;
            _studio.position = StudioOrigin;

            _pivot = new GameObject("Pivot").transform;
            _pivot.SetParent(_studio, false);

            var camGo = new GameObject("Kamera");
            camGo.transform.SetParent(_studio, false);
            _camera = camGo.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.fieldOfView = 24f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 20f;
            _camera.targetTexture = _texture;
            _camera.allowHDR = false;
            _camera.allowMSAA = true;
            _camera.depth = -50f;
            _camera.enabled = true;

            // Üç noktalı aydınlatma: sıcak anahtar (sol-üst-ön), soğuk dolgu (sağ-ön), turuncu kontur (arka-üst) + zemin sekmesi.
            AddLight("Anahtar", new Vector3(-1.3f, 1.5f, -1.5f), new Color(1f, 0.94f, 0.82f), 8f, 8f);
            AddLight("Dolgu", new Vector3(1.8f, 0.3f, -1.1f), new Color(0.65f, 0.78f, 1f), 2.4f, 8f);
            AddLight("Kontur", new Vector3(0.6f, 1.1f, 1.8f), new Color(1f, 0.62f, 0.38f), 7f, 8f);
            AddLight("Alt", new Vector3(0f, -0.9f, -0.6f), new Color(0.45f, 0.5f, 0.55f), 1.0f, 6f);

            BuildPedestal();
        }

        private void BuildPedestal()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Kaide";
            var col = go.GetComponent<Collider>();
            if (col != null)
                Destroy(col);
            go.transform.SetParent(_studio, false);
            _pedestal = go.transform;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _pedestalMat = new Material(shader) { name = "HK_Kaide" };
                var dark = new Color(0.045f, 0.05f, 0.048f, 1f);
                if (_pedestalMat.HasProperty(BaseColorId)) _pedestalMat.SetColor(BaseColorId, dark);
                if (_pedestalMat.HasProperty(ColorId)) _pedestalMat.SetColor(ColorId, dark);
                if (_pedestalMat.HasProperty("_Smoothness")) _pedestalMat.SetFloat("_Smoothness", 0.55f);
                if (_pedestalMat.HasProperty("_Metallic")) _pedestalMat.SetFloat("_Metallic", 0.35f);
                go.GetComponent<Renderer>().sharedMaterial = _pedestalMat;
            }

            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _pedestal.localScale = new Vector3(1.2f, 0.02f, 1.2f);
            _pedestal.localPosition = new Vector3(0f, -0.2f, 0f);
        }

        private void AddLight(string name, Vector3 local, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_studio, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
        }

        /// <summary>Gösterilen silahı değiştirir (null = boş).</summary>
        public void SetWeapon(WeaponDefinitionData weapon)
        {
            if (_pivot == null)
                return;
            if (_model != null)
                Destroy(_model);
            _model = null;
            _weaponModel = null;
            _hasBounds = false;
            _tintRenderers.Clear();
            _tintOriginal.Clear();
            if (weapon == null)
                return;

            WeaponModel built = null;
            try
            {
                built = WeaponModelFactory.BuildModel(weapon, _pivot, 0, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LoadoutPreview] Silah modeli kurulamadı: " + e.Message);
            }

            if (built == null)
                return;
            _model = built.gameObject;
            _weaponModel = built;
            _model.transform.localPosition = Vector3.zero;
            _model.transform.localRotation = Quaternion.identity;
            _pivot.localRotation = Quaternion.identity; // sınırlar eksen hizalı hesaplansın

            // Sınırlara göre merkezle ve kamera mesafesini ayarla.
            var renderers = _model.GetComponentsInChildren<Renderer>(true);
            var has = false;
            var bounds = new Bounds();
            for (var i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled)
                    continue;
                if (!has)
                {
                    bounds = renderers[i].bounds;
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            if (has)
            {
                var offset = _pivot.InverseTransformPoint(bounds.center);
                _model.transform.localPosition = -offset;
                var radius = Mathf.Max(0.2f, bounds.extents.magnitude);
                _distance = radius / Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.95f;
                _model.transform.localPosition = -offset;
                // Yerel (pivot uzayı) sınırlar: eklenti bağlantı noktaları için yedek konumlar.
                _localBounds = new Bounds(-offset + Vector3.zero, bounds.size);
                _hasBounds = true;
                if (_pedestal != null)
                {
                    var floor = _localBounds.min.y - 0.07f;
                    _pedestal.localPosition = new Vector3(0f, floor, 0f);
                    var d = Mathf.Max(0.6f, Mathf.Max(bounds.size.x, bounds.size.z) * 1.15f);
                    _pedestal.localScale = new Vector3(d, 0.02f, d);
                }
            }

            CacheTintTargets();
        }

        private void CacheTintTargets()
        {
            if (_model == null)
                return;
            var rs = _model.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < rs.Length; i++)
            {
                var m = rs[i].sharedMaterial;
                if (m == null)
                    continue;
                Color c;
                if (m.HasProperty(BaseColorId)) c = m.GetColor(BaseColorId);
                else if (m.HasProperty(ColorId)) c = m.GetColor(ColorId);
                else continue;
                _tintRenderers.Add(rs[i]);
                _tintOriginal.Add(c);
            }
        }

        /// <summary>
        /// Kamuflaj/kaplama rengini canlı uygular (parça rengi ile karışır; çok koyu parçalar (namlu/optik) korunur).
        /// tint null ise özgün renkler geri gelir.
        /// </summary>
        public void SetTint(Color? tint)
        {
            if (_tintRenderers.Count == 0)
                return;
            if (_block == null)
                _block = new MaterialPropertyBlock();
            for (var i = 0; i < _tintRenderers.Count; i++)
            {
                var r = _tintRenderers[i];
                if (r == null)
                    continue;
                if (!tint.HasValue)
                {
                    r.SetPropertyBlock(null);
                    continue;
                }

                var o = _tintOriginal[i];
                r.GetPropertyBlock(_block);
                var c = TintColor(o, tint.Value);
                _block.SetColor(BaseColorId, c);
                _block.SetColor(ColorId, c);
                r.SetPropertyBlock(_block);
            }
        }

        /// <summary>Parça renginin kamuflaj rengiyle karışımı (saf mantık): koyu parçalar (parlaklık &lt; 0,08) neredeyse değişmez.</summary>
        public static Color TintColor(Color original, Color tint)
        {
            var lum = original.r * 0.3f + original.g * 0.59f + original.b * 0.11f;
            var k = Mathf.Clamp01(lum / 0.08f) * 0.65f;
            var mixed = Color.Lerp(original, new Color(original.r * tint.r * 1.6f, original.g * tint.g * 1.6f, original.b * tint.b * 1.6f, original.a), k);
            mixed.a = original.a;
            return mixed;
        }

        /// <summary>
        /// Eklenti yuvasının bağlantı noktasını önizleme görüntüsünde 0..1 görünüm koordinatı olarak verir
        /// (RawImage içinde normalleştirilmiş; kameranın arkasındaysa false).
        /// </summary>
        public bool TryGetMountViewport(AttachmentSlot slot, out Vector2 viewport)
        {
            viewport = default;
            if (_camera == null || _weaponModel == null || !_hasBounds)
                return false;
            var world = MountWorld(slot);
            var v = _camera.WorldToViewportPoint(world);
            if (v.z <= 0f)
                return false;
            viewport = new Vector2(v.x, v.y);
            return true;
        }

        private Vector3 MountWorld(AttachmentSlot slot)
        {
            Transform t = null;
            switch (slot)
            {
                case AttachmentSlot.Sight: t = _weaponModel.SightPoint; break;
                case AttachmentSlot.Muzzle: t = _weaponModel.Muzzle; break;
                case AttachmentSlot.Magazine: t = _weaponModel.Magazine; break;
                case AttachmentSlot.Grip: t = _weaponModel.LeftHandGrip; break;
            }

            if (t != null && t != _weaponModel.transform)
                return t.position;

            // Yedek: sınır kutusu üzerinde yaklaşık nokta (pivot uzayı → dünya).
            var b = _localBounds;
            Vector3 local;
            switch (slot)
            {
                case AttachmentSlot.Sight: local = new Vector3(b.center.x, b.max.y, b.center.z + b.extents.z * 0.1f); break;
                case AttachmentSlot.Muzzle: local = new Vector3(b.center.x, b.center.y + b.extents.y * 0.3f, b.max.z); break;
                case AttachmentSlot.Grip: local = new Vector3(b.center.x, b.center.y - b.extents.y * 0.3f, b.center.z + b.extents.z * 0.45f); break;
                case AttachmentSlot.Magazine: local = new Vector3(b.center.x, b.min.y, b.center.z - b.extents.z * 0.1f); break;
                default: local = new Vector3(b.center.x, b.center.y, b.min.z); break;
            }

            if (slot == AttachmentSlot.Stock)
                local = new Vector3(b.center.x, b.center.y, b.min.z);
            return _pivot.TransformPoint(local);
        }

        private void LateUpdate()
        {
            if (_camera == null)
                return;
            _yaw = MainMenuMotion.PreviewYaw(_yaw, Time.unscaledTime >= _resumeAt ? DegreesPerSecond : 0f, Time.unscaledDeltaTime, _dragging);
            _pivot.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            _camera.transform.localPosition = new Vector3(0f, _distance * 0.1f, -_distance);
            _camera.transform.localRotation = Quaternion.Euler(Mathf.Rad2Deg * Mathf.Atan2(_distance * 0.1f, _distance), 0f, 0f);
        }

        public void OnBeginDrag(PointerEventData eventData) => _dragging = true;

        public void OnDrag(PointerEventData eventData) => _yaw = Mathf.Repeat(_yaw - eventData.delta.x * 0.45f, 360f);

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
            _resumeAt = Time.unscaledTime + 1.5f;
        }

        private void OnDestroy()
        {
            if (_camera != null)
                _camera.targetTexture = null;
            if (_studio != null)
                Destroy(_studio.gameObject);
            if (_pedestalMat != null)
                Destroy(_pedestalMat);
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }
    }
}
