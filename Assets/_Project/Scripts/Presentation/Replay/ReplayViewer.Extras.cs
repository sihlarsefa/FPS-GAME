using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Project.Application.Replay;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.Replay
{
    /// <summary>Tekrar izleyici cilası: isim etiketi + can çubuğu, hasar sayıları, mini harita, hız önayarları, ekran görüntüsü.</summary>
    public sealed partial class ReplayViewer
    {
        private sealed class Tag
        {
            public Transform Holder;
            public Transform Fill;
            public Renderer FillRenderer;
            public Renderer BgRenderer;
            public float Fraction = 1f;
        }

        private struct DamageNumber
        {
            public Transform Transform;
            public TextMesh Mesh;
            public float Start;
            public Vector3 Origin;
            public Color Color;
        }

        private const float TagWidth = 1.1f;
        private const float DamageLifetime = 0.9f;
        private const int MaxDamageNumbers = 40;
        private const float KillLead = 3f;
        private const float MinimapSize = 240f;

        private readonly Dictionary<int, Tag> _tags = new Dictionary<int, Tag>();
        private readonly List<DamageNumber> _damageNumbers = new List<DamageNumber>();
        private readonly List<RectTransform> _miniDots = new List<RectTransform>();
        private readonly List<Image> _miniDotImages = new List<Image>();
        private readonly Button[] _speedButtons = new Button[Speeds.Length];
        private ReplayHealthEstimator _health;
        private RectTransform _miniRoot;
        private RectTransform _miniCamera;
        private Vector2 _miniMin;
        private float _miniScale = 1f;
        private float _miniOffsetX, _miniOffsetY;

        private bool _firstPerson;
        private bool _shoulder;
        private float _fov = 60f;
        private float _targetFov = 60f;
        private Vector3 _freeVelocity;
        private float _freeYawTarget, _freePitchTarget;
        private bool _freeInit;
        private Ghost _hiddenFp;
        private float _toastUntil;
        private string _toast = string.Empty;
        private Text _toastLabel;

        private void SetupExtras()
        {
            _health = new ReplayHealthEstimator(_data.Events);
            if (_camera != null)
                _fov = _targetFov = Mathf.Clamp(_camera.fieldOfView, 30f, 90f);
            for (var i = 0; i < _ghosts.Count; i++)
                BuildTag(_ghosts[i]);
            BuildMinimap();
        }

        // ------------------------------------------------------------------ Girdi

        private void ExtraInput(Keyboard kb)
        {
            if (kb.nKey.wasPressedThisFrame) JumpToKill(kb.leftShiftKey.isPressed ? -1 : 1);
            if (kb.vKey.wasPressedThisFrame) ToggleFirstPerson();
            if (kb.bKey.wasPressedThisFrame) ToggleShoulder();
            if (kb.f12Key.wasPressedThisFrame) TakeScreenshot();
            if (kb.digit1Key.wasPressedThisFrame) SetSpeed(0);
            if (kb.digit2Key.wasPressedThisFrame) SetSpeed(1);
            if (kb.digit3Key.wasPressedThisFrame) SetSpeed(2);
            if (kb.digit4Key.wasPressedThisFrame) SetSpeed(3);
            if (kb.digit5Key.wasPressedThisFrame) SetSpeed(4);
        }

        public void SetSpeed(int index) => _speedIndex = Mathf.Clamp(index, 0, Speeds.Length - 1);

        public void ToggleFirstPerson()
        {
            if (_followId < 0)
                return;
            _firstPerson = !_firstPerson;
            if (_firstPerson)
                _follow = true;
            ShowToast(_firstPerson ? "Birinci şahıs" : "Üçüncü şahıs");
        }

        public void ToggleShoulder()
        {
            _shoulder = !_shoulder;
            ShowToast(_shoulder ? "Omuz üstü kamera" : "Orta kamera");
        }

        /// <summary>Bir sonraki (dir &lt; 0 ise önceki) ölüme atlar ve öldüreni takip eder.</summary>
        public void JumpToKill(int dir)
        {
            var seek = ReplayHealthEstimator.NextKillSeek(_data.Events, _time, KillLead, dir);
            if (seek < 0f)
            {
                ShowToast(dir >= 0 ? "Başka ölüm yok" : "Önceki ölüm yok");
                return;
            }
            Seek(seek);
            var at = seek + KillLead;
            for (var i = 0; i < _data.Events.Count; i++)
            {
                var e = _data.Events[i];
                if (e.Type == ReplayEventType.Death && Mathf.Abs(e.Time - at) < 0.01f)
                {
                    var who = e.Actor >= 0 && _ghostById.ContainsKey(e.Actor) ? e.Actor : e.Target;
                    if (_ghostById.ContainsKey(who))
                    {
                        _followId = who;
                        _follow = true;
                        _focusInit = false;
                        _orbitYaw = _ghostById[who].Yaw;
                    }
                    break;
                }
            }
            _playing = true;
        }

        public void TakeScreenshot()
        {
            try
            {
                var dir = Path.Combine(UnityEngine.Application.persistentDataPath, "Screenshots");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "tekrar_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".png");
                ScreenCapture.CaptureScreenshot(path);
                ShowToast("Ekran görüntüsü kaydedildi");
            }
            catch (Exception e)
            {
                ShowToast("Ekran görüntüsü alınamadı");
                Debug.LogWarning("[Tekrar] Ekran görüntüsü: " + e.Message);
            }
        }

        private void ShowToast(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 2f;
        }

        // ------------------------------------------------------------------ Etiket + can çubuğu

        private void BuildTag(Ghost g)
        {
            if (g == null || g.Root == null)
                return;
            try
            {
                var holder = new GameObject("EtiketKutusu").transform;
                holder.SetParent(g.Root, false);
                holder.localPosition = new Vector3(0f, 2.1f, 0f);
                if (g.Label != null)
                {
                    g.Label.transform.SetParent(holder, false);
                    g.Label.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                }
                var tag = new Tag { Holder = holder };
                var bg = MakeQuad(holder, "Zemin", new Color(0f, 0f, 0f, 0.65f), new Vector3(TagWidth + 0.06f, 0.14f, 1f), 0.002f);
                tag.BgRenderer = bg;
                var fill = MakeQuad(holder, "Can", Color.green, new Vector3(TagWidth, 0.09f, 1f), 0f);
                tag.Fill = fill != null ? fill.transform : null;
                tag.FillRenderer = fill;
                _tags[g.Player.Id] = tag;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Tekrar] Etiket kurulamadı: " + e.Message);
            }
        }

        private Renderer MakeQuad(Transform parent, string name, Color color, Vector3 scale, float z)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null)
                Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            var mat = LineMaterial();
            if (mr == null)
                return null;
            if (mat != null)
                mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            SetColor(mr, color);
            return mr;
        }

        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

        private static void SetColor(Renderer r, Color c)
        {
            if (r == null)
                return;
            Block.SetColor("_Color", c);
            r.SetPropertyBlock(Block);
        }

        private void UpdateTags()
        {
            if (_camera == null)
                return;
            var cam = _camera.transform;
            for (var i = 0; i < _ghosts.Count; i++)
            {
                var g = _ghosts[i];
                if (g.Root == null || !_tags.TryGetValue(g.Player.Id, out var tag) || tag.Holder == null)
                    continue;
                var dist = Vector3.Distance(cam.position, g.Position);
                var hide = !g.Visible || (_firstPerson && _follow && g.Player.Id == _followId) || dist > 160f;
                if (tag.Holder.gameObject.activeSelf == hide)
                    tag.Holder.gameObject.SetActive(!hide);
                if (hide)
                    continue;

                tag.Holder.rotation = cam.rotation;
                var s = Mathf.Clamp(dist / 14f, 1f, 3.5f);
                tag.Holder.localScale = Vector3.one * s;

                var f = g.Dead ? 0f : _health.Fraction(g.Player.Id, _time);
                if (Mathf.Abs(f - tag.Fraction) > 0.001f || tag.Fraction < 0f)
                {
                    tag.Fraction = f;
                    if (tag.Fill != null)
                    {
                        var w = Mathf.Max(0.001f, f) * TagWidth;
                        tag.Fill.localScale = new Vector3(w, 0.09f, 1f);
                        tag.Fill.localPosition = new Vector3(-(TagWidth - w) * 0.5f, 0f, 0f);
                    }
                    SetColor(tag.FillRenderer, f > 0.6f ? new Color(0.25f, 0.85f, 0.3f) : (f > 0.3f ? new Color(0.95f, 0.75f, 0.15f) : new Color(0.9f, 0.2f, 0.15f)));
                }
            }
        }

        // ------------------------------------------------------------------ Hasar sayıları

        private void SpawnDamageNumber(ReplayEvent e)
        {
            if (e.Value <= 0f || _damageNumbers.Count >= MaxDamageNumbers)
                return;
            var font = UiTheme.Font;
            if (font == null)
                return;
            var go = new GameObject("HasarSayısı");
            go.transform.SetParent(_runtimeRoot, false);
            var origin = new Vector3(e.X, e.Y + 1.6f, e.Z);
            go.transform.position = origin;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = Mathf.RoundToInt(e.Value).ToString(CultureInfo.InvariantCulture);
            tm.anchor = TextAnchor.MiddleCenter;
            tm.fontSize = 48;
            tm.characterSize = e.Flag ? 0.09f : 0.06f;
            var color = e.Flag ? new Color(1f, 0.82f, 0.2f) : Color.white;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = font.material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _damageNumbers.Add(new DamageNumber { Transform = go.transform, Mesh = tm, Start = Time.unscaledTime, Origin = origin, Color = color });
        }

        private void ClearDamageNumbers()
        {
            for (var i = 0; i < _damageNumbers.Count; i++)
                if (_damageNumbers[i].Transform != null)
                    Destroy(_damageNumbers[i].Transform.gameObject);
            _damageNumbers.Clear();
        }

        private void UpdateDamageNumbers()
        {
            if (_camera == null)
                return;
            var cam = _camera.transform;
            for (var i = _damageNumbers.Count - 1; i >= 0; i--)
            {
                var d = _damageNumbers[i];
                var age = (Time.unscaledTime - d.Start) / DamageLifetime;
                if (d.Transform == null || age >= 1f)
                {
                    if (d.Transform != null)
                        Destroy(d.Transform.gameObject);
                    _damageNumbers.RemoveAt(i);
                    continue;
                }
                var rise = 1f - (1f - age) * (1f - age);
                d.Transform.position = d.Origin + Vector3.up * (rise * 1.2f);
                d.Transform.rotation = cam.rotation;
                d.Transform.localScale = Vector3.one * Mathf.Clamp(Vector3.Distance(cam.position, d.Origin) / 12f, 1f, 4f);
                var c = d.Color;
                c.a = age < 0.6f ? 1f : 1f - (age - 0.6f) / 0.4f;
                d.Mesh.color = c;
            }
        }

        // ------------------------------------------------------------------ Mini harita

        private void BuildMinimap()
        {
            if (_canvas == null)
                return;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var any = false;
            for (var f = 0; f < _data.Frames.Count; f++)
            {
                var actors = _data.Frames[f].Actors;
                for (var a = 0; a < actors.Length; a++)
                {
                    min.x = Mathf.Min(min.x, actors[a].X); min.y = Mathf.Min(min.y, actors[a].Z);
                    max.x = Mathf.Max(max.x, actors[a].X); max.y = Mathf.Max(max.y, actors[a].Z);
                    any = true;
                }
            }
            if (!any)
            {
                min = new Vector2(-100f, -100f);
                max = new Vector2(100f, 100f);
            }
            var pad = 20f;
            min -= new Vector2(pad, pad);
            max += new Vector2(pad, pad);
            var span = Mathf.Max(max.x - min.x, max.y - min.y, 1f);
            _miniMin = min;
            _miniScale = (MinimapSize - 16f) / span;
            _miniOffsetX = 8f + ((MinimapSize - 16f) - (max.x - min.x) * _miniScale) * 0.5f;
            _miniOffsetY = 8f + ((MinimapSize - 16f) - (max.y - min.y) * _miniScale) * 0.5f;

            _miniRoot = UiFactory.Panel((RectTransform)_canvas.transform, UiTheme.WithAlpha(UiTheme.PanelDark, 0.8f));
            _miniRoot.gameObject.name = "MiniHarita";
            _miniRoot.anchorMin = _miniRoot.anchorMax = new Vector2(1f, 0f);
            _miniRoot.pivot = new Vector2(1f, 0f);
            _miniRoot.sizeDelta = new Vector2(MinimapSize, MinimapSize);
            _miniRoot.anchoredPosition = new Vector2(-24f, 260f);

            for (var i = 0; i < _ghosts.Count; i++)
            {
                var dot = UiFactory.Panel(_miniRoot, Color.white);
                dot.gameObject.name = "Nokta_" + i;
                dot.anchorMin = dot.anchorMax = Vector2.zero;
                dot.pivot = new Vector2(0.5f, 0.5f);
                dot.sizeDelta = new Vector2(7f, 7f);
                var img = dot.GetComponent<Image>();
                if (img != null) img.raycastTarget = false;
                _miniDots.Add(dot);
                _miniDotImages.Add(img);
            }

            _miniCamera = UiFactory.Panel(_miniRoot, new Color(1f, 0.95f, 0.4f, 1f));
            _miniCamera.gameObject.name = "KameraOku";
            _miniCamera.anchorMin = _miniCamera.anchorMax = Vector2.zero;
            _miniCamera.pivot = new Vector2(0.5f, 0.2f);
            _miniCamera.sizeDelta = new Vector2(8f, 18f);
            var cimg = _miniCamera.GetComponent<Image>();
            if (cimg != null) cimg.raycastTarget = false;
            var miniImg = _miniRoot.GetComponent<Image>();
            if (miniImg != null) miniImg.raycastTarget = false;

            _toastLabel = UiFactory.Label((RectTransform)_canvas.transform, "", UiTheme.FontSmall, TextAnchor.MiddleCenter, UiTheme.Text, FontStyle.Bold);
            UiFactory.SetRect(_toastLabel, new Vector2(0.3f, 0.5f), new Vector2(0.7f, 0.5f), new Vector2(0f, 120f), new Vector2(0f, 170f));
            UiFactory.AddShadow(_toastLabel, UiTheme.TextShadow, new Vector2(2f, -2f));
        }

        private Vector2 MiniPos(float x, float z)
        {
            return new Vector2(_miniOffsetX + (x - _miniMin.x) * _miniScale, _miniOffsetY + (z - _miniMin.y) * _miniScale);
        }

        private void UpdateMinimap()
        {
            if (_miniRoot == null)
                return;
            var local = _data.FindPlayer(_data.LocalPlayerId);
            for (var i = 0; i < _ghosts.Count && i < _miniDots.Count; i++)
            {
                var g = _ghosts[i];
                var dot = _miniDots[i];
                var show = g.Visible;
                if (dot.gameObject.activeSelf != show)
                    dot.gameObject.SetActive(show);
                if (!show)
                    continue;
                dot.anchoredPosition = MiniPos(g.Position.x, g.Position.z);
                var selected = g.Player.Id == _followId;
                var ally = local != null && local.Team == g.Player.Team;
                Color c = g.Dead ? new Color(0.5f, 0.5f, 0.5f, 0.7f) : (selected ? Color.white : (ally ? UiTheme.AllyBlue : UiTheme.EnemyRed));
                if (_miniDotImages[i] != null)
                    _miniDotImages[i].color = c;
                var size = selected ? 11f : 7f;
                dot.sizeDelta = new Vector2(size, size);
            }
            if (_camera != null && _miniCamera != null)
            {
                var p = _camera.transform.position;
                _miniCamera.anchoredPosition = MiniPos(p.x, p.z);
                _miniCamera.localRotation = Quaternion.Euler(0f, 0f, -_camera.transform.eulerAngles.y);
            }
        }

        // ------------------------------------------------------------------ Hız önayarları

        private void BuildSpeedPresets(Transform row)
        {
            var label = UiFactory.Label(row, "HIZ", UiTheme.FontSmall, TextAnchor.MiddleCenter, UiTheme.Khaki, FontStyle.Bold);
            UiFactory.LayoutSize(label, 70f, 52f);
            for (var i = 0; i < Speeds.Length; i++)
            {
                var index = i;
                var b = UiFactory.Button(row, Speeds[i].ToString("0.##", CultureInfo.InvariantCulture) + "x", () => SetSpeed(index), UiButtonStyle.Default);
                UiFactory.LayoutSize(b, 84f, 52f);
                _speedButtons[i] = b;
            }
            UiFactory.FlexibleSpacer(row);
            AddButton(row, "SONRAKİ ÖLÜM (N)", () => JumpToKill(1), 250f);
            AddButton(row, "BİRİNCİ ŞAHIS (V)", ToggleFirstPerson, 250f);
            AddButton(row, "OMUZ (B)", ToggleShoulder, 150f);
            AddButton(row, "EKRAN (F12)", TakeScreenshot, 190f);
        }

        private void UpdateSpeedButtons()
        {
            for (var i = 0; i < _speedButtons.Length; i++)
            {
                if (_speedButtons[i] == null)
                    continue;
                var txt = Speeds[i].ToString("0.##", CultureInfo.InvariantCulture) + "x";
                UiFactory.SetButtonLabel(_speedButtons[i], i == _speedIndex ? "[" + txt + "]" : txt);
            }
        }

        // ------------------------------------------------------------------ Döngü

        private void UpdateExtras(float dt)
        {
            if (_health == null)
                return;
            UpdateFirstPersonVisibility();
            UpdateTags();
            UpdateDamageNumbers();
            UpdateMinimap();
            UpdateSpeedButtons();
            if (_toastLabel != null)
                UiFactory.SetText(_toastLabel, Time.unscaledTime < _toastUntil ? _toast : string.Empty);
            if (_camera != null && Mathf.Abs(_camera.fieldOfView - _fov) > 0.01f)
                _camera.fieldOfView = _fov;
        }

        private void UpdateFirstPersonVisibility()
        {
            Ghost target = null;
            if (_firstPerson && _follow && _ghostById.TryGetValue(_followId, out var g) && g.Visible)
                target = g;
            if (_hiddenFp != null && _hiddenFp != target)
                SetModelRenderers(_hiddenFp, true);
            if (target != null)
                SetModelRenderers(target, false);
            _hiddenFp = target;
        }

        private void SetModelRenderers(Ghost g, bool enabled)
        {
            if (g.Root == null)
                return;
            var rs = g.Root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < rs.Length; i++)
            {
                var r = rs[i];
                if (r is MeshRenderer && r.GetComponent<TextMesh>() != null)
                    continue;
                if (_tags.TryGetValue(g.Player.Id, out var tag) && tag.Holder != null && r.transform.IsChildOf(tag.Holder))
                    continue;
                r.enabled = enabled;
            }
        }
    }
}
