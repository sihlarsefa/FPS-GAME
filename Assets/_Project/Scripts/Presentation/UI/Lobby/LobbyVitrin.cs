using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Weapons;
using Project.Infrastructure.Weapons.Skins;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Presentation.UI.Lobby
{
    /// <summary>
    /// VİTRİN sekmesi: solda kaplama listesi (seçili = kırmızı çerçeve), ortada silahın 3D dönen önizlemesi
    /// (ekran dışı sahne + RenderTexture kamerası; fareyle sürükleyerek çevrilir), altta KUŞAN düğmesi.
    /// Seçim silah başına kalıcıdır (<see cref="WeaponSkinSelection"/>); oyunda WeaponModelFactory uygular.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyVitrin : MonoBehaviour
    {
        private static readonly string[] WeaponList = { WeaponIds.Mpt55, WeaponIds.G3, WeaponIds.Sar109, WeaponIds.Jng90, WeaponIds.Sar9 };
        private static readonly string[] WeaponNames = { "MPT-55", "G3A7", "SAR 109T", "JNG-90", "SAR 9" };
        private static readonly Vector3 StagePos = new Vector3(0f, -4000f, 0f);

        private int _weapon;
        private int _selected; // 0 = kaplamasız, i>0 = All[i-1]
        private readonly List<Image> _frames = new List<Image>();
        private readonly List<Text> _marks = new List<Text>();
        private Text _weaponName, _skinName;
        private Button _equipButton;
        private RawImage _view;
        private LoadoutMiniBars _mini;

        private RenderTexture _rt;
        private Camera _cam;
        private GameObject _stage;
        private Transform _pivot;
        private GameObject _model;
        private bool _dragging;
        private float _yaw = 90f;

        public static LobbyVitrin Create(RectTransform parent)
        {
            var root = UiFactory.CreateRect("Vitrin", parent);
            UiFactory.Stretch(root);
            var v = root.gameObject.AddComponent<LobbyVitrin>();
            v.Build(root);
            return v;
        }

        private void Build(RectTransform root)
        {
            // Sol: kaplama listesi.
            var list = UiFactory.CreateRect("SkinList", root);
            UiFactory.SetRect(list, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(560f, 120f), new Vector2(860f, -30f));
            var vl = list.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 8f; vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            var head = UiFactory.Label(list, "KAPLAMALAR", 20, TextAnchor.MiddleLeft, LobbyTheme.Gold, FontStyle.Bold);
            head.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            for (int i = 0; i <= WeaponSkinCatalog.All.Length; i++) BuildRow(list, i);

            // Orta: önizleme.
            var frame = UiFactory.Image(root, null, LobbyTheme.Panel);
            frame.gameObject.name = "PreviewFrame";
            UiFactory.SetRect(frame.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(900f, 120f), new Vector2(-60f, -30f));
            _rt = new RenderTexture(1024, 512, 16, RenderTextureFormat.ARGB32) { name = "VitrinRT", antiAliasing = 1 };
            _view = UiFactory.RawImage(frame.rectTransform, _rt);
            UiFactory.Stretch(_view, 6f);
            _view.raycastTarget = true;
            _view.gameObject.AddComponent<DragRelay>().Owner = this;

            _weaponName = UiFactory.Label(frame.rectTransform, string.Empty, 28, TextAnchor.MiddleCenter, LobbyTheme.Text, FontStyle.Bold);
            UiFactory.SetRect(_weaponName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-200f, -52f), new Vector2(200f, -8f));
            MakeArrow(frame.rectTransform, "◀", -1, new Vector2(0.5f, 1f), new Vector2(-300f, -52f));
            MakeArrow(frame.rectTransform, "▶", 1, new Vector2(0.5f, 1f), new Vector2(240f, -52f));
            _skinName = UiFactory.Label(frame.rectTransform, string.Empty, 20, TextAnchor.MiddleCenter, LobbyTheme.TextDim);
            UiFactory.SetRect(_skinName.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-250f, 78f), new Vector2(250f, 108f));

            // Sol-alt: silahın 6 çubuk özeti (hasar, hız, menzil, kontrol, hareket, nişan) + genel not.
            _mini = LoadoutMiniBars.Create(frame.rectTransform, new Vector2(20f, 20f), 250f, LobbyTheme.TextDim, LobbyTheme.Border);

            // Alt: KUŞAN.
            _equipButton = UiFactory.Button(frame.rectTransform, "KUŞAN", Equip, UiButtonStyle.Primary);
            UiFactory.SetRect((RectTransform)_equipButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-130f, 16f), new Vector2(130f, 72f));
            LobbyGlowButton.Attach(_equipButton);

            _selected = EquippedIndex();
            RefreshUi();
        }

        private void BuildRow(RectTransform list, int index)
        {
            var row = UiFactory.CreateRect("Row_" + index, list);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;
            var frame = UiFactory.Image(row, null, LobbyTheme.Red);
            UiFactory.Stretch(frame);
            var body = UiFactory.Image(row, null, LobbyTheme.PanelRaised);
            UiFactory.Stretch(body, 3f);
            body.raycastTarget = true;
            var swatch = UiFactory.Image(row, null, index == 0 ? LobbyTheme.Border : WeaponSkinCatalog.All[index - 1].Primary);
            UiFactory.SetRect(swatch.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, -16f), new Vector2(46f, 16f));
            var name = UiFactory.Label(row, index == 0 ? "KAPLAMASIZ" : WeaponSkinCatalog.All[index - 1].Name.ToUpperInvariant(),
                18, TextAnchor.MiddleLeft, LobbyTheme.Text, FontStyle.Bold);
            UiFactory.SetRect(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(58f, 0f), new Vector2(-40f, 0f));
            var mark = UiFactory.Label(row, "✓", 22, TextAnchor.MiddleRight, LobbyTheme.Gold, FontStyle.Bold);
            UiFactory.SetRect(mark.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-40f, 0f), new Vector2(-12f, 0f));
            var btn = row.gameObject.AddComponent<Button>();
            btn.targetGraphic = body;
            btn.transition = Selectable.Transition.None;
            int captured = index;
            btn.onClick.AddListener(() => { _selected = captured; RefreshUi(); ApplyPreviewSkin(); UiSounds.Play(UiSfx.Tab); });
            _frames.Add(frame);
            _marks.Add(mark);
        }

        private void MakeArrow(RectTransform parent, string glyph, int delta, Vector2 anchor, Vector2 pos)
        {
            var b = UiFactory.Button(parent, glyph, () => { _weapon = (_weapon + delta + WeaponList.Length) % WeaponList.Length; _selected = EquippedIndex(); RebuildModel(); RefreshUi(); }, UiButtonStyle.Default);
            UiFactory.SetRect((RectTransform)b.transform, anchor, anchor, pos, pos + new Vector2(60f, 44f));
        }

        private string CurrentWeaponId => WeaponList[_weapon];

        private int EquippedIndex() => WeaponSkinSelection.IndexOf(WeaponSkinSelection.Get(CurrentWeaponId)) + 1;

        private string SelectedSkinId => _selected <= 0 ? WeaponSkinCatalog.DefaultId : WeaponSkinCatalog.All[_selected - 1].Id;

        private void Equip()
        {
            WeaponSkinSelection.Set(CurrentWeaponId, SelectedSkinId);
            RefreshUi();
            UiSounds.Play(UiSfx.Press);
        }

        private void RefreshUi()
        {
            int equipped = EquippedIndex();
            for (int i = 0; i < _frames.Count; i++)
            {
                _frames[i].enabled = i == _selected;
                _marks[i].enabled = i == equipped;
            }
            _weaponName.text = WeaponNames[_weapon];
            if (_mini != null)
            {
                WeaponCatalog.TryGet(CurrentWeaponId, out var def);
                _mini.SetWeapon(def);
            }
            _skinName.text = _selected == equipped ? "KUŞANILDI" : (_selected == 0 ? "Kaplamasız" : WeaponSkinCatalog.All[_selected - 1].Name);
            if (_equipButton != null)
            {
                _equipButton.interactable = _selected != equipped;
                UiFactory.SetButtonLabel(_equipButton, _selected == equipped ? "KUŞANILDI" : "KUŞAN");
            }
        }

        // ------------------------------------------------------------------ 3D önizleme

        private void OnEnable()
        {
            try
            {
                EnsureStage();
                if (_cam != null) _cam.enabled = true;
            }
            catch (Exception e) { Debug.LogWarning("[Vitrin] sahne kurulamadı: " + e.Message); }
        }

        private void OnDisable()
        {
            if (_cam != null) _cam.enabled = false;
        }

        private void OnDestroy()
        {
            if (_stage != null) Destroy(_stage);
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
        }

        private void EnsureStage()
        {
            if (_stage != null) return;
            _stage = new GameObject("VitrinSahne");
            _stage.transform.position = StagePos;
            _pivot = new GameObject("Pivot").transform;
            _pivot.SetParent(_stage.transform, false);

            var camGo = new GameObject("VitrinKamera");
            camGo.transform.SetParent(_stage.transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.07f, 0.08f, 0.09f, 1f);
            _cam.fieldOfView = 25f;
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 50f;
            _cam.targetTexture = _rt;
            _cam.allowHDR = false;
            _cam.allowMSAA = false;

            var lightGo = new GameObject("VitrinIsik");
            lightGo.transform.SetParent(_stage.transform, false);
            lightGo.transform.localPosition = new Vector3(1.2f, 1.0f, -0.8f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point; l.range = 12f; l.intensity = 6f; l.color = new Color(1f, 0.96f, 0.9f);
            l.shadows = LightShadows.None;

            RebuildModel();
        }

        private void RebuildModel()
        {
            if (_pivot == null) return;
            if (_model != null) Destroy(_model);
            var style = WeaponStyles.Resolve(CurrentWeaponId, default);
            var wm = WeaponModelFactory.BuildModel(style, null, _pivot, 0, true);
            if (wm == null) return;
            _model = wm.gameObject;
            _model.transform.localPosition = Vector3.zero;
            _model.transform.localRotation = Quaternion.identity;

            var rs = _model.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            _model.transform.position -= b.center - _pivot.position;
            float halfLen = Mathf.Max(b.size.x, b.size.z, b.size.y) * 0.5f;
            float dist = Mathf.Max(0.4f, halfLen / (Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2f) * 1.25f);
            _cam.transform.position = _pivot.position + new Vector3(0f, 0f, -dist);
            _cam.transform.LookAt(_pivot.position);
            _yaw = 90f;
            ApplyPreviewSkin();
        }

        private void ApplyPreviewSkin()
        {
            if (_model == null) return;
            var rs = _model.GetComponentsInChildren<Renderer>(true);
            WeaponSkinApplier.Clear(rs);
            if (_selected > 0) WeaponSkinApplier.Apply(rs, SelectedSkinId);
        }

        private void Update()
        {
            if (_pivot == null) return;
            if (!_dragging) _yaw += 18f * Time.unscaledDeltaTime;
            _pivot.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        internal void BeginDrag() { _dragging = true; }
        internal void EndDrag() { _dragging = false; }
        internal void DragBy(float dx) { _yaw -= dx * 0.5f; }

        private sealed class DragRelay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public LobbyVitrin Owner;
            public void OnBeginDrag(PointerEventData e) { if (Owner != null) Owner.BeginDrag(); }
            public void OnDrag(PointerEventData e) { if (Owner != null) Owner.DragBy(e.delta.x); }
            public void OnEndDrag(PointerEventData e) { if (Owner != null) Owner.EndDrag(); }
        }
    }
}
