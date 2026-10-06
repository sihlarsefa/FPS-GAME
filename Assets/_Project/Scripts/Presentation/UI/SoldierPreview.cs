using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure;
using Project.Infrastructure.Characters;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>Askeri önizleme saf mantığı (test edilebilir): yakınlaştırma sınırı, kamera mesafesi, hızlı kamuflaj listesi.</summary>
    public static class SoldierPreviewLogic
    {
        public const float MinZoom = 0.8f;
        public const float MaxZoom = 2.5f;

        public static float ClampZoom(float zoom) => Mathf.Clamp(float.IsNaN(zoom) ? 1f : zoom, MinZoom, MaxZoom);

        /// <summary>Fare tekerleği değişimiyle (yukarı = yakınlaş) üstel yakınlaştırma; sonuç 0,8..2,5 arasında.</summary>
        public static float ApplyScroll(float zoom, float scrollY) => ClampZoom(zoom * Mathf.Exp(scrollY * 0.12f));

        /// <summary>Taban mesafenin yakınlaştırmaya bölünmüşü (zoom 2 → yarı mesafe).</summary>
        public static float CameraDistance(float baseDistance, float zoom) => baseDistance / ClampZoom(zoom);

        /// <summary>Kamuflaj yuvasından hızlı seçim kimlikleri (en çok max adet, sıralı, tekrarsız).</summary>
        public static List<string> QuickCamoIds(IEnumerable<CosmeticDefinition> items, int max)
        {
            var list = new List<string>();
            if (items == null)
                return list;
            foreach (var d in items)
            {
                if (d == null || d.slot != CosmeticsService.SlotCamo || string.IsNullOrEmpty(d.id) || list.Contains(d.id))
                    continue;
                list.Add(d.id);
                if (list.Count >= max)
                    break;
            }

            return list;
        }

        /// <summary>Beret/kask geçişinin etiketi.</summary>
        public static string HeadgearLabel(bool beretShown) => beretShown ? "BERE  ›  KASK" : "KASK  ›  BERE";
    }

    /// <summary>
    /// TİM ekranının etkileşimli asker önizlemesi: 1024x1024 RenderTexture, üç noktalı aydınlatma (LoadoutPreview örüntüsü),
    /// sürükle = döndür, tekerlek = 0,8-2,5x yakınlaş. Kozmetik değişince (CosmeticsRuntime.Service.Changed) model yeniden kurulur.
    /// Kurulum başarısız / batch modda RawImage gizlenir, yedek metin gösterilir.
    /// </summary>
    public sealed class SoldierPreview : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        private static readonly Vector3 StudioOrigin = new Vector3(-4000f, 4000f, 4000f);
        private const float DegreesPerSecond = 14f;
        private const float BaseDistance = 5.2f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private RenderTexture _texture;
        private Camera _camera;
        private Transform _studio;
        private Transform _pivot;
        private Material _pedestalMat;
        private SoldierModel _model;
        private RawImage _target;
        private Text _fallback;
        private float _yaw = 180f; // asker +Z'ye bakar; kamera -Z'de → 180° önünü gösterir
        private float _resumeAt;
        private bool _dragging;
        private float _zoom = 1f;
        private bool _beret = true;
        private bool _rebuildQueued;
        private CosmeticsService _subscribed;

        public bool IsLive => _camera != null && _model != null;
        public float Zoom => _zoom;
        public float Yaw => _yaw;
        public bool BeretShown => _beret;

        public static SoldierPreview Create(RawImage target, Text fallback, int size = 1024)
        {
            var p = target.gameObject.AddComponent<SoldierPreview>();
            p._target = target;
            p._fallback = fallback;
            p.Build(size);
            return p;
        }

        private void Build(int size)
        {
            SetFallback(true);
            if (UnityEngine.Application.isBatchMode)
                return;
            try
            {
                _texture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
                {
                    name = "HK_SoldierPreview",
                    antiAliasing = 4,
                    filterMode = FilterMode.Bilinear
                };
                _texture.Create();
                _target.texture = _texture;
                _target.color = Color.white;
                _target.raycastTarget = true; // sürükleme / tekerlek için

                var root = new GameObject("[Asker Önizleme Stüdyosu]");
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
                _camera.nearClipPlane = 0.1f;
                _camera.farClipPlane = 30f;
                _camera.targetTexture = _texture;
                _camera.allowHDR = false;
                _camera.allowMSAA = true;
                _camera.depth = -49f;

                // Üç noktalı aydınlatma + zemin sekmesi (LoadoutPreview ile aynı örüntü, insan ölçeğine göre).
                AddLight("Anahtar", new Vector3(-2.2f, 2.8f, -3.2f), new Color(1f, 0.94f, 0.82f), 14f, 14f);
                AddLight("Dolgu", new Vector3(3f, 1.0f, -2.6f), new Color(0.65f, 0.78f, 1f), 4f, 14f);
                AddLight("Kontur", new Vector3(1.2f, 2.4f, 3.2f), new Color(1f, 0.62f, 0.38f), 12f, 14f);
                AddLight("Alt", new Vector3(0f, -0.4f, -1.5f), new Color(0.45f, 0.5f, 0.55f), 1.5f, 8f);
                BuildPedestal();

                RebuildModel();
                Subscribe();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SoldierPreview] Kurulamadı: " + e.Message);
                Teardown();
                SetFallback(true);
            }
        }

        private void SetFallback(bool show)
        {
            if (_fallback != null)
                _fallback.gameObject.SetActive(show);
            if (_target != null)
                _target.enabled = !show;
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

        private void BuildPedestal()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Kaide";
            var col = go.GetComponent<Collider>();
            if (col != null)
                Destroy(col);
            go.transform.SetParent(_studio, false);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _pedestalMat = new Material(shader) { name = "HK_AskerKaide" };
                var dark = new Color(0.045f, 0.05f, 0.048f, 1f);
                if (_pedestalMat.HasProperty(BaseColorId)) _pedestalMat.SetColor(BaseColorId, dark);
                if (_pedestalMat.HasProperty(ColorId)) _pedestalMat.SetColor(ColorId, dark);
                if (_pedestalMat.HasProperty("_Smoothness")) _pedestalMat.SetFloat("_Smoothness", 0.5f);
                go.GetComponent<Renderer>().sharedMaterial = _pedestalMat;
            }

            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.localScale = new Vector3(1.3f, 0.02f, 1.3f);
            go.transform.localPosition = new Vector3(0f, -0.02f, 0f);
        }

        private void Subscribe()
        {
            var s = CosmeticsRuntime.Service;
            if (s == null || s == _subscribed)
                return;
            Unsubscribe();
            _subscribed = s;
            s.Changed += OnCosmeticsChanged;
        }

        private void Unsubscribe()
        {
            if (_subscribed != null)
                _subscribed.Changed -= OnCosmeticsChanged;
            _subscribed = null;
        }

        private void OnCosmeticsChanged() => _rebuildQueued = true;

        /// <summary>Askeri (kozmetik + rol teçhizatı ile) yeniden kurar; hata olursa yedek metin gösterilir.</summary>
        public void RebuildModel()
        {
            _rebuildQueued = false;
            if (_pivot == null)
                return;
            if (_model != null)
                Destroy(_model.gameObject);
            _model = null;
            try
            {
                var look = SoldierLook.ForTeam(0, new System.Random(7));
                look.Beret = true;
                CosmeticsRuntime.ApplyToLook(look); // kuşanılmış kamuflaj/bere/kolluk/yüz boyası/gili
                var model = SoldierModel.Build(_pivot, look, null, false, GameLayers.Default);
                if (model == null)
                    throw new InvalidOperationException("model null");
                model.AutoSyncEquipment = false;
                model.AutoPlayDeath = false;
                var role = LoadoutSelection.Shared.Role;
                var kit = LoadoutCatalog.For(role);
                model.SetEquipment(Mathf.Max(1, kit.HelmetLevel), Mathf.Max(1, kit.VestLevel), kit.BackpackLevel);
                model.SetLocomotion(Vector3.zero, Stance.Standing, true);
                model.ShowBeretOverHelmet = _beret;
                if (WeaponCatalog.TryGet(kit.PrimaryWeaponId, out var weapon) && weapon != null)
                {
                    try { model.HoldWeapon(weapon); }
                    catch (Exception e) { Debug.LogWarning("[SoldierPreview] Silah kurulamadı: " + e.Message); }
                }

                _model = model;
                SetFallback(false);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SoldierPreview] Asker kurulamadı: " + e.Message);
                SetFallback(true);
            }
        }

        /// <summary>Bere/kask önizlemesini değiştirir; yeni durumu (bere görünüyor mu) döndürür.</summary>
        public bool ToggleHeadgear()
        {
            _beret = !_beret;
            if (_model != null)
                _model.ShowBeretOverHelmet = _beret;
            return _beret;
        }

        public void SetZoom(float zoom) => _zoom = SoldierPreviewLogic.ClampZoom(zoom);

        private void LateUpdate()
        {
            if (_camera == null)
                return;
            if (_rebuildQueued)
                RebuildModel();
            _yaw = MainMenuMotion.PreviewYaw(_yaw, Time.unscaledTime >= _resumeAt ? DegreesPerSecond : 0f, Time.unscaledDeltaTime, _dragging);
            _pivot.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            var d = SoldierPreviewLogic.CameraDistance(BaseDistance, _zoom);
            // Yakınlaşınca bakış noktası göğüs/kafaya kayar (1,0 m → 1,35 m).
            var focusY = Mathf.Lerp(0.95f, 1.35f, Mathf.InverseLerp(1f, SoldierPreviewLogic.MaxZoom, _zoom));
            _camera.transform.localPosition = new Vector3(0f, focusY + 0.1f, -d);
            _camera.transform.localRotation = Quaternion.Euler(Mathf.Rad2Deg * Mathf.Atan2(0.1f, d), 0f, 0f);
        }

        public void OnBeginDrag(PointerEventData eventData) => _dragging = true;
        public void OnDrag(PointerEventData eventData) => _yaw = Mathf.Repeat(_yaw - eventData.delta.x * 0.5f, 360f);

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
            _resumeAt = Time.unscaledTime + 1.5f;
        }

        public void OnScroll(PointerEventData eventData) => _zoom = SoldierPreviewLogic.ApplyScroll(_zoom, eventData.scrollDelta.y);

        private void Teardown()
        {
            Unsubscribe();
            if (_camera != null)
                _camera.targetTexture = null;
            if (_studio != null)
                Destroy(_studio.gameObject);
            _studio = null;
            _camera = null;
            _model = null;
            if (_pedestalMat != null)
                Destroy(_pedestalMat);
            _pedestalMat = null;
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }

            _texture = null;
        }

        private void OnEnable()
        {
            if (_camera != null)
            {
                Subscribe();
                _rebuildQueued = true; // sayfa yeniden açılınca rol/kozmetik tazele
            }
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Teardown();
    }

    /// <summary>
    /// Önizleme kartı: asker görüntüsü + isim plakası/rütbe apoleti + bere/kask düğmesi + hızlı kamuflaj renkleri
    /// (kuşanma CosmeticsService.Equip ile; sahip olunmayanlar soluk, tıklanamaz).
    /// </summary>
    public sealed class SoldierPreviewCard : MonoBehaviour
    {
        private const int MaxSwatches = 7;
        private SoldierPreview _preview;
        private Text _plateName;
        private Text _plateRank;
        private RectTransform _insignia;
        private MilitaryRank _shownRank = (MilitaryRank)(-1);
        private Button _headgear;
        private readonly List<(string id, Image ring, Image fill)> _swatches = new List<(string, Image, Image)>();

        public SoldierPreview Preview => _preview;

        public static SoldierPreviewCard Create(RectTransform parent)
        {
            var card = UiFactory.Panel(parent, UiTheme.WithAlpha(UiTheme.PanelDark, 0.9f), UiSprites.ChamferRect);
            card.gameObject.name = "SoldierPreviewCard";
            var host = card.gameObject.AddComponent<SoldierPreviewCard>();
            host.Build(card);
            return host;
        }

        private void Build(RectTransform card)
        {
            // Görüntü: üstte, kare; alt 170 px plaka + kontroller için ayrılır.
            var raw = UiFactory.RawImage(card, null);
            raw.gameObject.name = "SoldierView";
            UiFactory.SetRect(raw, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 170f), new Vector2(-8f, -8f));
            raw.enabled = false;

            var fb = UiFactory.Label(card, "Asker önizlemesi kullanılamıyor", UiTheme.FontSmall, TextAnchor.MiddleCenter, UiTheme.TextMuted);
            UiFactory.SetRect(fb, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 170f), new Vector2(-8f, -8f));

            var hint = UiFactory.Label(card, "SÜRÜKLE: DÖNDÜR   ·   TEKERLEK: YAKINLAŞ", UiTheme.FontTiny, TextAnchor.LowerCenter, UiTheme.TextDim);
            hint.raycastTarget = false;
            UiFactory.SetRect(hint, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 172f), new Vector2(-8f, 192f));

            _preview = SoldierPreview.Create(raw, fb);

            // Plaka: rütbe apoleti + isim + rütbe satırı.
            var plate = UiFactory.Panel(card, UiTheme.WithAlpha(Color.black, 0.45f), UiSprites.ChamferRect);
            UiFactory.SetRect(plate, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 56f), new Vector2(-10f, 112f));
            var holder = UiFactory.CreateRect("Insignia", plate);
            UiFactory.Anchor(holder, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(110f, 44f));
            _insignia = MenuRankInsignia.Create(holder, MilitaryRank.Er, 40f);
            _plateName = UiFactory.Label(plate, string.Empty, UiTheme.FontNormal, TextAnchor.UpperLeft, UiTheme.Text, FontStyle.Bold);
            _plateName.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_plateName, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(126f, 24f), new Vector2(-8f, -4f));
            _plateRank = UiFactory.Label(plate, string.Empty, UiTheme.FontTiny, TextAnchor.LowerLeft, UiTheme.Khaki, FontStyle.Bold);
            _plateRank.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_plateRank, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(126f, 4f), new Vector2(-8f, -30f));

            // Kontroller: kamuflaj renkleri (sol) + bere/kask düğmesi (sağ).
            var svc = CosmeticsRuntime.Service;
            var ids = SoldierPreviewLogic.QuickCamoIds(svc != null ? svc.Items : null, MaxSwatches);
            for (var i = 0; i < ids.Count; i++)
                AddSwatch(card, ids[i], i);

            _headgear = UiFactory.Button(card, SoldierPreviewLogic.HeadgearLabel(_preview.BeretShown), OnHeadgear, UiButtonStyle.Default);
            UiFactory.Anchor(_headgear, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 8f), new Vector2(210f, 40f));
            var l = UiFactory.GetButtonLabel(_headgear);
            if (l != null)
                l.fontSize = UiTheme.FontTiny;

            if (svc != null)
                svc.Changed += RefreshSwatches;
            RefreshSwatches();
        }

        private void AddSwatch(RectTransform card, string id, int index)
        {
            var ring = UiFactory.Image(card, null, UiTheme.WithAlpha(Color.white, 0f));
            ring.gameObject.name = "Swatch_" + id;
            UiFactory.Anchor(ring, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12f + index * 40f, 12f), new Vector2(34f, 34f));
            var fill = UiFactory.Image(ring.transform, null, Color.gray);
            UiFactory.SetRect(fill, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(4f, 4f), new Vector2(-4f, -4f));
            fill.raycastTarget = false;
            var captured = id;
            var svc = CosmeticsRuntime.Service;
            if (svc != null && svc.TryGet(id, out var def))
                fill.color = CosmeticsRuntime.PreviewColor(def);
            var btn = ring.gameObject.AddComponent<Button>();
            btn.targetGraphic = ring;
            btn.onClick.AddListener(() => EquipCamo(captured));
            _swatches.Add((id, ring, fill));
        }

        private void EquipCamo(string id)
        {
            var svc = CosmeticsRuntime.Service;
            if (svc == null || !svc.IsOwned(id))
                return;
            svc.Equip(id); // Changed → önizleme yeniden kurulur, halkalar tazelenir
        }

        private void RefreshSwatches()
        {
            var svc = CosmeticsRuntime.Service;
            if (svc == null)
                return;
            var equipped = svc.GetEquipped(CosmeticsService.SlotCamo);
            for (var i = 0; i < _swatches.Count; i++)
            {
                var s = _swatches[i];
                if (s.ring == null)
                    continue;
                var owned = svc.IsOwned(s.id);
                var c = s.fill.color;
                c.a = owned ? 1f : 0.35f;
                s.fill.color = c;
                s.ring.color = s.id == equipped ? MenuRankInsignia.Gold : UiTheme.WithAlpha(Color.white, owned ? 0.25f : 0.08f);
            }
        }

        private void OnHeadgear()
        {
            var shown = _preview != null && _preview.ToggleHeadgear();
            UiFactory.SetButtonLabel(_headgear, SoldierPreviewLogic.HeadgearLabel(shown));
        }

        /// <summary>İsim plakası ve rütbe apoletini günceller.</summary>
        public void SetIdentity(MilitaryRank rank, string formattedName)
        {
            if (_plateName != null)
                _plateName.text = formattedName ?? string.Empty;
            if (_plateRank != null)
                _plateRank.text = RankCatalog.GetName(rank);
            if (rank != _shownRank && _insignia != null)
            {
                MenuRankInsignia.Rebuild(_insignia, rank);
                _shownRank = rank;
            }

            RefreshSwatches();
        }

        private void OnDestroy()
        {
            var svc = CosmeticsRuntime.Service;
            if (svc != null)
                svc.Changed -= RefreshSwatches;
        }
    }
}
