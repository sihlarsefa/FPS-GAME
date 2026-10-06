using System;
using System.Reflection;
using Project.Application.Replay;
using Project.Presentation.Replay;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.Player
{
    /// <summary>Killcam ayarı ve canlı tekrar verisi kaynağı.</summary>
    public static class KillCamSettings
    {
        private const string PrefKey = "harekat.killcam";

        /// <summary>Kapalıysa ölümde doğrudan ölüm özetine geçilir (varsayılan açık).</summary>
        public static bool Enabled
        {
            get { try { return PlayerPrefs.GetInt(PrefKey, 1) != 0; } catch (Exception) { return true; } }
            set { try { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); } catch (Exception) { } }
        }

        /// <summary>Test/entegrasyon kancası: verilirse kayıt buradan alınır, yoksa sahnedeki ReplayRecorder'ın canlı verisi okunur.</summary>
        public static Func<ReplayData> Provider;

        private static FieldInfo _dataField;
        private static PropertyInfo _dataProp;

        public static ReplayData ResolveLive()
        {
            try
            {
                if (Provider != null)
                    return Provider();
                var rec = UnityEngine.Object.FindFirstObjectByType<Project.Infrastructure.Replay.ReplayRecorder>();
                if (rec == null)
                    return null;
                var t = typeof(Project.Infrastructure.Replay.ReplayRecorder);
                if (_dataProp == null && _dataField == null)
                {
                    _dataProp = t.GetProperty("Data", BindingFlags.Public | BindingFlags.Instance)
                                ?? t.GetProperty("Live", BindingFlags.Public | BindingFlags.Instance);
                    if (_dataProp == null)
                        _dataField = t.GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (_dataProp != null) return _dataProp.GetValue(rec) as ReplayData;
                return _dataField != null ? _dataField.GetValue(rec) as ReplayData : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KillCam] Tekrar verisi okunamadı: " + e.Message);
                return null;
            }
        }
    }

    /// <summary>
    /// Ölüm killcam'i: kaydedicideki son 6 sn'yi katilin omuz arkasından oynatır; isabet anında 0.3x yavaş OYNATIM
    /// (timeScale değil) ve kill anında kırmızı vinyet. Düz sınıf: kamerayı çağıran (SpectatorController) sürer.
    /// </summary>
    public sealed class KillCam
    {
        private readonly Camera _camera;
        private readonly ReplayData _data;
        private readonly KillCamPlan _plan;
        private float _time;
        private bool _hasPose;
        private Vector3 _pos;
        private Vector3 _look;
        private Image _vignette;
        private Texture2D _tex;
        private Sprite _sprite;

        public KillCamPlan Plan => _plan;
        public bool Finished { get; private set; }

        private KillCam(Camera cam, ReplayData data, KillCamPlan plan)
        {
            _camera = cam;
            _data = data;
            _plan = plan;
            _time = plan.Start;
        }

        /// <summary>Kayıt, ölüm ve katil varsa başlatır; yoksa / mod kapalıysa null (doğrudan ölüm özeti).</summary>
        public static KillCam TryBegin(Camera cam, int victimId, RectTransform uiRoot)
        {
            if (cam == null || !KillCamSettings.Enabled)
                return null;
            var data = KillCamSettings.ResolveLive();
            var plan = KillCamTimeline.Build(data, victimId);
            if (!plan.Valid)
                return null;
            var kc = new KillCam(cam, data, plan);
            kc.BuildVignette(uiRoot);
            kc.ApplyPose(true);
            return kc;
        }

        /// <summary>Her karede çağrılır (gerçek dt). Bitince Finished = true.</summary>
        public void Tick(float dt)
        {
            if (Finished)
                return;
            _time = KillCamTimeline.Advance(_plan, _time, dt);
            ApplyPose(false);
            if (_vignette != null)
            {
                var a = KillCamTimeline.VignetteIntensity(_plan, _time);
                var c = _vignette.color;
                c.a = Mathf.Clamp01(a);
                _vignette.color = c;
            }
            if (KillCamTimeline.IsFinished(_plan, _time))
                Finished = true;
        }

        public void Dispose()
        {
            Finished = true;
            if (_vignette != null) UnityEngine.Object.Destroy(_vignette.gameObject);
            if (_sprite != null) UnityEngine.Object.Destroy(_sprite);
            if (_tex != null) UnityEngine.Object.Destroy(_tex);
            _vignette = null;
        }

        private void ApplyPose(bool snap)
        {
            var p = KillCamTimeline.Pose(_data, _plan, _time);
            if (p.Valid)
            {
                var pos = new Vector3(p.PosX, p.PosY, p.PosZ);
                var look = new Vector3(p.LookX, p.LookY, p.LookZ);
                if (!_hasPose || snap) { _pos = pos; _look = look; }
                else
                {
                    var k = 1f - Mathf.Exp(-14f * Mathf.Max(Time.deltaTime, 0.0001f));
                    _pos = Vector3.Lerp(_pos, pos, k);
                    _look = Vector3.Lerp(_look, look, k);
                }
                _hasPose = true;
            }
            if (!_hasPose || _camera == null)
                return;
            var dir = _look - _pos;
            if (dir.sqrMagnitude < 1e-4f)
                dir = Vector3.forward;
            _camera.transform.SetPositionAndRotation(_pos, Quaternion.LookRotation(dir, Vector3.up));
        }

        private void BuildVignette(RectTransform root)
        {
            if (root == null)
                return;
            try
            {
                const int n = 64;
                _tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                for (var y = 0; y < n; y++)
                    for (var x = 0; x < n; x++)
                    {
                        var dx = (x + 0.5f) / n * 2f - 1f;
                        var dy = (y + 0.5f) / n * 2f - 1f;
                        var d = Mathf.Sqrt(dx * dx + dy * dy);
                        var a = Mathf.Clamp01((d - 0.45f) / 0.85f);
                        _tex.SetPixel(x, y, new Color(0.83f, 0.23f, 0.18f, a * a));
                    }
                _tex.Apply(false, false);
                _sprite = Sprite.Create(_tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                _vignette = UiFactory.Image(root, _sprite, new Color(1f, 1f, 1f, 0f));
                _vignette.raycastTarget = false;
                UiFactory.Stretch(_vignette);
                _vignette.transform.SetSiblingIndex(0);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KillCam] Vinyet kurulamadı: " + e.Message);
            }
        }
    }
}
