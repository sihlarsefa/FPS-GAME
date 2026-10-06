using System;
using Project.Core.Domain;
using Project.Infrastructure.Vfx;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Infrastructure.Vehicles
{
    /// <summary>
    /// Sürüş hissi: gövde yatış/yunuslama (süspansiyon görseli), lastik tozu/çamuru, korna (H), farlar (N),
    /// nişancı stabilizasyonu (V), taret motor uğultusu, hasar dumanı/yangını ve sürücü göstergesi (OnGUI).
    /// Tüm girdi yalnız yerel oyuncu sürücü/nişancıyken okunur.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleFeel : MonoBehaviour
    {
        private DrivableVehicle _vehicle;
        private Rigidbody _rb;
        private Transform _root;
        private Transform _gunnerView;
        private Quaternion _gunnerBase = Quaternion.identity;
        private Quaternion _tilt = Quaternion.identity;
        private float _pitch, _roll, _heave;
        private Vector3 _lastVel;
        private float _longAccel, _latAccel;
        private int _gear = 1, _lastGear = 1;
        private float _shiftDip;
        private float _dustNext, _smokeNext;
        private Light _left, _right, _fire;
        private bool _headlights, _headlightsInit;
        private HeadlightMode _lightMode = HeadlightMode.Kapali;
        private AudioLowPassFilter _muffle;
        private readonly float[] _wheelOff = new float[4], _wheelVel = new float[4], _wheelPrevComp = new float[4], _wheelApplied = new float[4];
        private AudioSource _horn, _whine;
        private static AudioClip _hornClip, _whineClip;
        private GUIStyle _label, _big;
        private Texture2D _white;

        public int Gear => _gear;
        public float Rpm01 { get; private set; }
        public bool GunnerStabilized { get; private set; } = true;
        public bool HeadlightsOn => _headlights;
        public HeadlightMode LightMode => _lightMode;
        public VehicleDamageState DamageState { get; private set; }

        public static VehicleFeel Attach(DrivableVehicle vehicle, Transform modelRoot, Transform gunnerView)
        {
            var f = vehicle.gameObject.AddComponent<VehicleFeel>();
            f._vehicle = vehicle;
            f._rb = vehicle.GetComponent<Rigidbody>();
            f._root = modelRoot;
            f._gunnerView = gunnerView;
            if (gunnerView != null)
                f._gunnerBase = gunnerView.localRotation;
            f.BuildLights();
            f.BuildAudio();
            return f;
        }

        private void BuildLights()
        {
            var cfg = _vehicle.Config;
            var z = cfg.WheelBaseZ + 1.0f;
            _left = MakeSpot("FarSol", new Vector3(-cfg.TrackX * 0.8f, 0.95f, z));
            _right = MakeSpot("FarSag", new Vector3(cfg.TrackX * 0.8f, 0.95f, z));
            var fireGo = new GameObject("MotorYangin");
            fireGo.transform.SetParent(transform, false);
            fireGo.transform.localPosition = new Vector3(0f, 1.7f, z - 0.6f);
            _fire = fireGo.AddComponent<Light>();
            _fire.type = LightType.Point;
            _fire.color = new Color(1f, 0.5f, 0.15f);
            _fire.range = 7f;
            _fire.intensity = 0f;
            _fire.shadows = LightShadows.None;
            _fire.enabled = false;
        }

        private Light MakeSpot(string name, Vector3 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root != null ? _root : transform, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = 62f;
            l.innerSpotAngle = 30f;
            l.range = 48f;
            l.intensity = 6f;
            l.color = new Color(1f, 0.95f, 0.8f);
            l.shadows = LightShadows.None;
            l.enabled = false;
            return l;
        }

        private void BuildAudio()
        {
            _horn = gameObject.AddComponent<AudioSource>();
            Project.Infrastructure.Audio.HdrMix.MixerRouting.Route(_horn, Project.Infrastructure.Audio.HdrMix.MixChannel.Arac); // mikser Arac grubu (yoksa no-op)
            _horn.clip = HornClip();
            _horn.loop = true;
            _horn.spatialBlend = 1f;
            _horn.rolloffMode = AudioRolloffMode.Linear;
            _horn.maxDistance = 140f;
            _horn.volume = 0.8f;
            _horn.playOnAwake = false;
            // İçerideyken dış sesler boğuk (düşük-geçiren filtre); dışarıda 22 kHz = etkisiz.
            _muffle = gameObject.GetComponent<AudioLowPassFilter>();
            if (_muffle == null) _muffle = gameObject.AddComponent<AudioLowPassFilter>();
            _muffle.cutoffFrequency = 22000f;

            var anchor = _vehicle.Turret != null && _vehicle.Turret.YawPivot != null ? _vehicle.Turret.YawPivot.gameObject : gameObject;
            _whine = anchor.AddComponent<AudioSource>();
            Project.Infrastructure.Audio.HdrMix.MixerRouting.Route(_whine, Project.Infrastructure.Audio.HdrMix.MixChannel.Arac);
            _whine.clip = WhineClip();
            _whine.loop = true;
            _whine.spatialBlend = 1f;
            _whine.rolloffMode = AudioRolloffMode.Linear;
            _whine.maxDistance = 30f;
            _whine.volume = 0f;
            _whine.playOnAwake = false;
        }

        /// <summary>Prosedürel iki tonlu korna (ses dosyası gerekmez).</summary>
        private static AudioClip HornClip()
        {
            if (_hornClip != null) return _hornClip;
            const int rate = 22050;
            var n = rate / 2;
            var d = new float[n];
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)rate;
                var a = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 392f * t)) * 0.5f + Mathf.Sin(2f * Mathf.PI * 494f * t) * 0.5f;
                d[i] = a * 0.35f;
            }

            _hornClip = AudioClip.Create("VehicleHorn", n, 1, rate, false);
            _hornClip.SetData(d, 0);
            return _hornClip;
        }

        /// <summary>Prosedürel servo uğultusu: ~420 Hz + harmonik (tam döngü: döngü dikişi yok).</summary>
        private static AudioClip WhineClip()
        {
            if (_whineClip != null) return _whineClip;
            const int rate = 22050;
            var n = rate; // 1 sn: 420 ve 840 Hz tam sayıda periyot
            var d = new float[n];
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)rate;
                d[i] = (Mathf.Sin(2f * Mathf.PI * 420f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 840f * t) * 0.25f) * 0.3f;
            }

            _whineClip = AudioClip.Create("TurretWhine", n, 1, rate, false);
            _whineClip.SetData(d, 0);
            return _whineClip;
        }

        private bool LocalDriving => _vehicle != null && _vehicle.HasDriver && _vehicle.Driver.IsLocalPlayer;

        private static bool IsDark()
        {
            try
            {
                var t = Project.Infrastructure.Rendering.Atmosphere.CurrentTime;
                return t == TimeOfDay.Gece || t == TimeOfDay.Safak || t == TimeOfDay.Aksam;
            }
            catch (Exception) { return false; }
        }

        private void FixedUpdate()
        {
            if (_rb == null)
                return;
            var dt = Time.fixedDeltaTime;
            var vel = _rb.linearVelocity;
            var worldAccel = (vel - _lastVel) / Mathf.Max(dt, 0.0001f);
            _lastVel = vel;
            var local = transform.InverseTransformDirection(worldAccel);
            _longAccel = Mathf.Lerp(_longAccel, local.z, 0.25f);
            _latAccel = Mathf.Lerp(_latAccel, local.x, 0.25f);
        }

        private void Update()
        {
            if (_vehicle == null)
                return;
            var dt = Time.deltaTime;
            var cfg = _vehicle.Config;
            var drive = _vehicle.HasDriver;

            // Vites/devir
            _gear = VehicleFeelMath.GearFor(_vehicle.SpeedKmh, cfg.MaxSpeedKmh);
            Rpm01 = VehicleFeelMath.Rpm01(_vehicle.SpeedKmh, cfg.MaxSpeedKmh);
            if (_gear != _lastGear)
            {
                if (_gear > _lastGear) _shiftDip = 1f;
                _lastGear = _gear;
            }

            _shiftDip = Mathf.MoveTowards(_shiftDip, 0f, dt * 4f);

            ApplyBodyTilt(dt);
            UpdateWheelSprings(dt);
            HandleInput(drive);
            UpdateHeadlights(drive);
            UpdateDamage(dt);
            UpdateTires(dt);
            UpdateWhine(dt);
            UpdateMuffle(dt);
        }

        /// <summary>İçeride (yerel sürücü/nişancı) dış ses boğuklaşır; nişancı kapağı açık sayılır → daha açık.</summary>
        private void UpdateMuffle(float dt)
        {
            if (_muffle == null)
                return;
            var inside = LocalDriving;
            var turret = _vehicle.Turret;
            var hatch = turret != null && turret.PlayerGunner ? 1f : 0f;
            var target = VehicleAmbienceMath.MuffleCutoff(inside, hatch);
            _muffle.cutoffFrequency = VehicleFeelMath.Smooth(_muffle.cutoffFrequency, target, 6f, dt);
            if (_horn != null) _horn.volume = 0.8f * VehicleAmbienceMath.InsideVolumeScale(inside);
        }

        /// <summary>Teker görselleri bağımsız yay/sönümle sıçrar (fizik süspansiyonunun üstüne ek ofset).</summary>
        private void UpdateWheelSprings(float dt)
        {
            for (var i = 0; i < 4; i++)
            {
                var t = _vehicle.WheelVisual(i);
                if (t == null)
                    continue;
                var comp = _vehicle.WheelCompression(i);
                // Sıkışma değişimi yaya darbe verir; yay aşırı salınımla yerine oturur.
                _wheelVel[i] += (comp - _wheelPrevComp[i]) * 6f;
                _wheelPrevComp[i] = comp;
                VehicleAmbienceMath.SpringStep(ref _wheelOff[i], ref _wheelVel[i], 0f, 140f, 11f, dt);
                _wheelOff[i] = VehicleAmbienceMath.ClampTravel(_wheelOff[i], 0.09f);
                var lp = t.localPosition;
                lp.y += _wheelOff[i] - _wheelApplied[i];
                _wheelApplied[i] = _wheelOff[i];
                t.localPosition = lp;
            }
        }

        /// <summary>Motor sesi perdesi (DrivableVehicle çağırır).</summary>
        public float EnginePitch(float throttleAbs) => VehicleFeelMath.EnginePitch(Rpm01, throttleAbs, _shiftDip);

        private void ApplyBodyTilt(float dt)
        {
            if (_root == null)
                return;
            var pT = VehicleFeelMath.BodyPitchTarget(_longAccel);
            var rT = VehicleFeelMath.BodyRollTarget(_latAccel);
            // Tekerlek sıkışma farkından küçük sarsıntı: sol-sağ → yatış, ön-arka → yunuslama.
            var fl = _vehicle.WheelCompression(0); var fr = _vehicle.WheelCompression(1);
            var rl = _vehicle.WheelCompression(2); var rr = _vehicle.WheelCompression(3);
            rT += ((fl + rl) - (fr + rr)) * 1.2f;
            pT += ((rl + rr) - (fl + fr)) * 1.0f;
            _pitch = VehicleFeelMath.Smooth(_pitch, pT, 7f, dt);
            _roll = VehicleFeelMath.Smooth(_roll, rT, 8f, dt);
            var heaveT = -((fl + fr + rl + rr) * 0.25f - 0.5f) * 0.05f;
            _heave = VehicleFeelMath.Smooth(_heave, heaveT, 10f, dt);
            _tilt = Quaternion.Euler(_pitch, 0f, _roll);
            _root.localRotation = _tilt;
            _root.localPosition = new Vector3(0f, _heave, 0f);
        }

        private void HandleInput(bool drive)
        {
            var kb = Keyboard.current;
            var local = LocalDriving && kb != null;
            if (_horn != null)
            {
                var want = local && kb[Key.H].isPressed;
                if (want && !_horn.isPlaying) _horn.Play();
                else if (!want && _horn.isPlaying) _horn.Stop();
            }

            if (local && kb[Key.N].wasPressedThisFrame)
            {
                _lightMode = VehicleAmbienceMath.NextMode(_lightMode); // Kapalı -> Açık -> Karartma
                _headlightsInit = true;
            }

            var turret = _vehicle.Turret;
            if (local && turret != null && turret.PlayerGunner && kb[Key.V].wasPressedThisFrame)
                GunnerStabilized = !GunnerStabilized;
        }

        private void UpdateHeadlights(bool drive)
        {
            if (!drive)
            {
                _headlightsInit = false;
                _headlights = false;
                _lightMode = HeadlightMode.Kapali;
            }
            else
            {
                if (!_headlightsInit)
                {
                    _lightMode = VehicleFeelMath.AutoHeadlights(IsDark()) ? HeadlightMode.Acik : HeadlightMode.Kapali;
                    _headlightsInit = true;
                }

                _headlights = _lightMode != HeadlightMode.Kapali && _vehicle.Health > 0f;
            }

            ApplyLight(_left);
            ApplyLight(_right);
        }

        private void ApplyLight(Light l)
        {
            if (l == null)
                return;
            if (l.enabled != _headlights) l.enabled = _headlights;
            if (!_headlights)
                return;
            l.intensity = VehicleAmbienceMath.HeadlightIntensity(_lightMode);
            l.range = VehicleAmbienceMath.HeadlightRange(_lightMode);
            l.spotAngle = VehicleAmbienceMath.HeadlightAngle(_lightMode);
            l.innerSpotAngle = l.spotAngle * 0.5f;
            // Karartma: kırmızımsı-kısık ışık (uzaktan görünürlük düşük).
            l.color = _lightMode == HeadlightMode.Karartma ? new Color(1f, 0.55f, 0.35f) : new Color(1f, 0.95f, 0.8f);
        }

        private void LateUpdate()
        {
            // Nişancı kamerası: stabilizasyon açıkken gövde yatışı kamera pozunda geri alınır.
            if (_gunnerView == null)
                return;
            var turret = _vehicle != null ? _vehicle.Turret : null;
            _gunnerView.localRotation = _gunnerBase;
            if (turret != null && turret.PlayerGunner && GunnerStabilized)
            {
                var corr = transform.rotation * Quaternion.Inverse(_tilt) * Quaternion.Inverse(transform.rotation);
                _gunnerView.rotation = corr * _gunnerView.rotation;
            }
        }

        private void UpdateDamage(float dt)
        {
            var max = Mathf.Max(1f, _vehicle.Config.MaxHealth);
            var destroyed = _vehicle.Health <= 0f;
            DamageState = VehicleFeelMath.DamageStateFor(_vehicle.Health / max, destroyed);
            var burning = DamageState == VehicleDamageState.Yangin;
            if (_fire != null)
            {
                _fire.enabled = burning;
                if (burning)
                    _fire.intensity = 2.5f + Mathf.PerlinNoise(Time.time * 9f, 0.3f) * 3f;
            }

            var interval = VehicleFeelMath.SmokeInterval(DamageState);
            if (interval <= 0f || Time.time < _smokeNext)
                return;
            _smokeNext = Time.time + interval;
            var cfg = _vehicle.Config;
            var pos = transform.TransformPoint(new Vector3(0f, 1.8f, cfg.WheelBaseZ + 0.4f));
            try
            {
                GameVfx.SmokeCloud(pos, burning ? 1.3f : 0.8f, burning ? 3.5f : 2.5f);
                if (burning)
                    GameVfx.MuzzleFlash(pos, Vector3.up, 1.6f);
            }
            catch (Exception) { /* efekt yok sayılır */ }
        }

        private void UpdateTires(float dt)
        {
            if (!_vehicle.HasDriver || _vehicle.SpeedKmh < 14f || Time.time < _dustNext)
                return;
            var tier = Project.Infrastructure.Rendering.QualityTierApplier.LastTier; // kalite kademesi (-1: bilinmiyor → tam)
            _dustNext = Time.time + Mathf.Lerp(0.28f, 0.1f, Mathf.Clamp01(_vehicle.SpeedKmh / 80f))
                * VehicleAmbienceMath.DustIntervalScale(tier < 0 ? 3 : tier);
            var cfg = _vehicle.Config;
                        for (var i = 0; i < 4; i++)
            {
                if (!_vehicle.WheelGrounded(i))
                    continue;
                // Yalnız arka tekerler + dönüşte dış ön teker toz kaldırır (efekt bütçesi).
                if (i < 2 && Mathf.Abs(_latAccel) < 2f)
                    continue;
                var lx = (i % 2 == 0 ? -1f : 1f) * cfg.TrackX;
                var lz = (i < 2 ? 1f : -1f) * cfg.WheelBaseZ;
                var p = transform.TransformPoint(new Vector3(lx, 0.1f, lz));
                var snow = false;
                if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var gh, 1.5f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
                    snow = Project.Infrastructure.Vfx.SurfaceClassifier.Classify(gh.collider, gh.point) == Project.Infrastructure.Vfx.SurfaceKind.Snow;
                var scale = VehicleAmbienceMath.TrailScale(_vehicle.SpeedKmh, cfg.MaxSpeedKmh, snow);
                try
                {
                    // Tekerin hemen arkasına (hareketin tersi) iz bırakır.
                    var back = p - transform.forward * 0.5f;
                    if (snow) GameVfx.FootstepDust(back, Project.Infrastructure.Vfx.SurfaceKind.Snow, scale);
                    else GameVfx.Dust(back, scale);
                }
                catch (Exception) { /* yok sayılır */ }
            }
        }

        private void UpdateWhine(float dt)
        {
            var turret = _vehicle.Turret;
            if (_whine == null || turret == null)
                return;
            var lvl = turret.PlayerGunner ? turret.WhineLevel : 0f;
            _whine.volume = Mathf.MoveTowards(_whine.volume, lvl * 0.35f, dt * 2.5f);
            _whine.pitch = 0.8f + lvl * 0.5f;
            if (_whine.volume > 0.01f && !_whine.isPlaying) _whine.Play();
            else if (_whine.volume <= 0.01f && _whine.isPlaying) _whine.Stop();
        }

        // ---- Gösterge (yalnız yerel oyuncu sürerken) ----
        private void OnGUI()
        {
            if (!LocalDriving || Event.current.type != EventType.Repaint)
                return;
            EnsureStyles();
            var cfg = _vehicle.Config;
            var s = Mathf.Max(0.8f, Screen.height / 1080f);
            var w = 330f * s; var h = 150f * s;
            var x = Screen.width - w - 24f * s; var y = Screen.height - h - 24f * s;
            Fill(new Rect(x, y, w, h), new Color(0.04f, 0.06f, 0.05f, 0.72f));
            _big.fontSize = Mathf.RoundToInt(44f * s);
            _label.fontSize = Mathf.RoundToInt(15f * s);
            GUI.Label(new Rect(x + 12f * s, y + 6f * s, 170f * s, 54f * s), Mathf.RoundToInt(_vehicle.SpeedKmh) + " km/s", _big);
            GUI.Label(new Rect(x + 12f * s, y + 62f * s, 180f * s, 20f * s),
                "VİTES " + _gear + "   DEVİR %" + Mathf.RoundToInt(Rpm01 * 100f), _label);
            GUI.Label(new Rect(x + 12f * s, y + 82f * s, 180f * s, 20f * s),
                "YÖN " + VehicleFeelMath.HeadingLabel(transform.eulerAngles.y), _label);
            GUI.Label(new Rect(x + 12f * s, y + 102f * s, 200f * s, 20f * s),
                "FAR [N] " + (_lightMode == HeadlightMode.Acik ? "AÇIK" : _lightMode == HeadlightMode.Karartma ? "KARARTMA" : "KAPALI") + "   KORNA [H]", _label);
            var turret = _vehicle.Turret;
            GUI.Label(new Rect(x + 12f * s, y + 122f * s, 200f * s, 20f * s),
                turret != null && turret.PlayerGunner ? "STABİLİZASYON [V] " + (GunnerStabilized ? "AÇIK" : "KAPALI") : cfg.DisplayName.ToUpperInvariant(), _label);

            // Hasar şeması: 2x2 bölge
            var hp01 = _vehicle.Health / Mathf.Max(1f, cfg.MaxHealth);
            var bx = x + w - 112f * s; var by = y + 12f * s;
            for (var i = 0; i < 4; i++)
            {
                var st = VehicleFeelMath.ZoneState(i, hp01);
                var c = st == 0 ? new Color(0.35f, 0.75f, 0.35f, 0.9f) : st == 1 ? new Color(0.95f, 0.75f, 0.2f, 0.95f) : new Color(0.9f, 0.2f, 0.15f, 1f);
                var r = new Rect(bx + (i % 2) * 52f * s, by + (i / 2) * 52f * s, 48f * s, 48f * s);
                Fill(r, c);
                _label.alignment = TextAnchor.MiddleCenter;
                _label.fontSize = Mathf.RoundToInt(11f * s);
                GUI.Label(r, VehicleFeelMath.ZoneNames[i], _label);
                _label.alignment = TextAnchor.UpperLeft;
            }

            _label.fontSize = Mathf.RoundToInt(13f * s);
            GUI.Label(new Rect(bx, by + 108f * s, 104f * s, 18f * s), "ZIRH %" + Mathf.RoundToInt(hp01 * 100f), _label);
        }

        private void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = prev;
        }

        private void EnsureStyles()
        {
            if (_label != null)
                return;
            _white = Texture2D.whiteTexture;
            _label = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _label.normal.textColor = new Color(0.92f, 0.95f, 0.88f);
            _big = new GUIStyle(_label) { fontStyle = FontStyle.Bold };
        }
    }
}
