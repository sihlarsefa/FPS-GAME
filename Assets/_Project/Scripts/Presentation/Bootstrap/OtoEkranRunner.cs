using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Project.Core.Domain;
using Project.Infrastructure.Diagnostics;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using Project.Presentation.Benchmark;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Presentation.Bootstrap
{
    /// <summary>
    /// Otomatik görsel QA: <c>-otoekran &lt;dir&gt;</c> (AAA_Benchmark Vitrin planları + kalite kademeleri + perf.csv) ve
    /// <c>-otoekran-harita &lt;sahne&gt; &lt;dir&gt;</c> (harita sahnesi, 5 bakış noktası). Varsayılan 3840x2160 (4K) pencere
    /// (<c>-otoekran-coz &lt;genişlik&gt; &lt;yükseklik&gt;</c> ile değişir); çekimler Ultra kademe + render scale 1.0 ile yapılır,
    /// çıkışta eski kademe geri yüklenir. Her çekimden önce TAA/temporal oturması için bekler; çözünürlük değişiminden sonra
    /// en az 2 kare beklenir. Asla takılmaz: sert zaman aşımı (varsayılan 120 sn) sonrası çıkar.
    /// Çıkışta "[OTOEKRAN] bitti" günlüğe yazılır. Bkz. Tools/UnityVerify/oto_ekran.sh.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    [DisallowMultipleComponent]
    public sealed class OtoEkranRunner : MonoBehaviour
    {
        public const string DoneLog = "[OTOEKRAN] bitti";
        private const float SceneWaitSeconds = 60f;
        private const string PerfHeader = "label,tier,fps,avg_ms,low1_fps,low1_ms,draw_calls,batches,gc_alloc_bytes,width,height";

        private static int _bootstrapped;

        private OtoEkranOptions _opt;
        private float _startTime;
        private bool _finished;
        private int _captured;
        private PerfSampler _sampler;
        private readonly StringBuilder _perf = new StringBuilder();
        private bool _hasPose;
        private Vector3 _posePosition;
        private Vector3 _poseLook;
        private Camera _camera;
        private int _restoreTier = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_bootstrapped != 0)
                return;
            var args = Environment.GetCommandLineArgs();
            if (!OtoEkranArgs.TryParse(args, out var options, out var reason))
            {
                if (OtoEkranArgs.HasAnyFlag(args))
                    Debug.LogWarning("[OTOEKRAN] adım: argümanlar çözümlenemedi (" + reason + ") args=[" + string.Join(" | ", args) + "]");
                return;
            }
            _bootstrapped = 1;
            var go = new GameObject("[OtoEkran]");
            DontDestroyOnLoad(go);
            go.AddComponent<OtoEkranRunner>().Begin(options);
        }

        private void Begin(OtoEkranOptions options)
        {
            _opt = options;
            _startTime = Time.realtimeSinceStartup;
            _sampler = new PerfSampler(240);
            _perf.AppendLine(PerfHeader);
            Debug.Log("[OTOEKRAN] başladı: " + options.Mode + " → " + options.OutDir
                      + (string.IsNullOrEmpty(options.Scene) ? string.Empty : " sahne=" + options.Scene)
                      + " zaman aşımı=" + options.TimeoutSeconds.ToString("0", CultureInfo.InvariantCulture) + " sn");
            StartCoroutine(Main());
        }

        private void Update()
        {
            if (_finished)
                return;
            _sampler?.SampleFrame();
            if (Time.realtimeSinceStartup - _startTime > _opt.TimeoutSeconds)
            {
                Debug.LogWarning("[OTOEKRAN] zaman aşımı — sert çıkış");
                Finish("zaman aşımı");
            }
        }

        private void LateUpdate()
        {
            if (_finished || !_hasPose)
                return;
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return;
            var t = _camera.transform;
            t.position = _posePosition;
            var dir = _poseLook - _posePosition;
            if (dir.sqrMagnitude > 1e-4f)
                t.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        private void OnDestroy()
        {
            _sampler?.Dispose();
        }

        // ------------------------------------------------------------------ Akış

        private IEnumerator Main()
        {
            yield return Safe(Prepare(), "hazırlık");
            if (!_finished)
                yield return Safe(_opt.Mode == OtoEkranMode.Ses ? RunAudio()
                    : _opt.Mode == OtoEkranMode.Harita ? RunMap() : RunBenchmark(), _opt.Mode == OtoEkranMode.Ses ? "ses" : "çekim");
            Finish("tamam");
        }

        private IEnumerator Prepare()
        {
            try
            {
                Directory.CreateDirectory(_opt.OutDir);
                if (_opt.Mode != OtoEkranMode.Ses)
                    Screen.SetResolution(_opt.Width, _opt.Height, FullScreenMode.Windowed);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OTOEKRAN] çözünürlük/klasör: " + e.Message);
            }

            if (_opt.Mode == OtoEkranMode.Ses)
            {
                for (var i = 0; i < 5; i++)
                    yield return null;
                yield break;
            }

            // Çekimler Ultra kademe + render scale 1.0 ile yapılır; çıkışta eski kademe geri yüklenir (Finish).
            _restoreTier = QualityTierApplier.LastTier;
            ForceUltra();

            // Çözünürlük bir kaç kare sonra oturur (en az 2 kare şart).
            for (var i = 0; i < 10; i++)
                yield return null;
            yield return Wait(0.5f);
        }

        /// <summary>Çekim kalitesi: Ultra kademe (PipelineTiers son satır, render scale 1.0) zorlanır.</summary>
        private static void ForceUltra()
        {
            try
            {
                PostProcessing.ApplyQuality(PipelineTiers.Count - 1);
                Debug.Log("[OTOEKRAN] Ultra kademe + render scale 1.0 zorlandı");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OTOEKRAN] Ultra kademe zorlanamadı: " + e.Message);
            }
        }

        /// <summary>-otoses: benchmark sahnesinde 25 sn betikli ses dizisi; 100 ms'de bir ses_rapor.csv satırı.</summary>
        private IEnumerator RunAudio()
        {
            AaaBenchmarkBootstrap.Begin(true);
            var deadline = Time.realtimeSinceStartup + SceneWaitSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (SceneManager.GetActiveScene().name == SceneNames.AaaBenchmark)
                {
                    var boot = FindAnyObjectByType<AaaBenchmarkBootstrap>();
                    if (boot != null && boot.IsReady)
                        break;
                }

                yield return null;
            }

            try
            {
                Project.Infrastructure.Audio.GameAudio.Initialize();
                Project.Infrastructure.Audio.HdrMix.AudioMix.Ensure();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SES] ses kurulumu: " + e.Message);
            }

            yield return Wait(1.0f);
            Debug.Log("[SES] başladı: " + Project.Infrastructure.Audio.AudioSelfTest.DurationSeconds.ToString("0", CultureInfo.InvariantCulture) + " sn dizi");
            var session = new Project.Infrastructure.Audio.AudioSelfTest.Session();
            var t0 = Time.realtimeSinceStartup;
            while (!session.Done && !_finished)
            {
                session.Tick(Time.realtimeSinceStartup - t0);
                yield return null;
            }

            try
            {
                Directory.CreateDirectory(_opt.OutDir);
                File.WriteAllText(Path.Combine(_opt.OutDir, Project.Infrastructure.Audio.AudioSelfTest.CsvFileName), session.Csv, new UTF8Encoding(false));
                _captured = session.Samples;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SES] ses_rapor.csv yazılamadı: " + e.Message);
            }

            Debug.Log(session.Summary());
        }

        private IEnumerator RunBenchmark()
        {
            AaaBenchmarkBootstrap.Begin(true);

            AaaBenchmarkBootstrap boot = null;
            AaaBenchmarkVitrin vitrin = null;
            var deadline = Time.realtimeSinceStartup + SceneWaitSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (SceneManager.GetActiveScene().name == SceneNames.AaaBenchmark)
                {
                    boot = FindAnyObjectByType<AaaBenchmarkBootstrap>();
                    vitrin = FindAnyObjectByType<AaaBenchmarkVitrin>();
                    if (boot != null && boot.IsReady && vitrin != null && vitrin.ShotCount > 0)
                        break;
                }

                yield return null;
            }

            if (vitrin == null || vitrin.ShotCount == 0)
            {
                Debug.LogError("[OTOEKRAN] AAA_Benchmark hazır olmadı.");
                yield break;
            }

            yield return Wait(1.5f);

            // 6 plan: her biri için TAA oturması + çekim + perf satırı.
            var tier = QualityTierApplier.LastTier >= 0 ? QualityTierApplier.LastTier : AaaBenchmarkBootstrap.BenchmarkTier;
            AaaBenchmarkVitrin.EnsureUltraTier();
            tier = AaaBenchmarkBootstrap.BenchmarkTier;
            for (var i = 0; i < vitrin.ShotCount; i++)
            {
                var name = vitrin.ShotNameAt(i);
                vitrin.SeekToShot(i, 0f);
                // Özne/çapa yenilemesi + 0.5 sn kamera oturması, sonra TAA bekleme süresi.
                yield return Wait(AaaBenchmarkVitrin.SettleSeconds);
                _sampler.Dispose();
                _sampler = new PerfSampler(240);
                yield return Wait(_opt.SettleSeconds);
                yield return Capture(OtoEkranArgs.FileName(i + 1, name), name, tier);
            }

            // Kalite kademeleri: 1. planı 0..3 ile yakala.
            for (var q = 0; q <= 3; q++)
            {
                vitrin.SeekToShot(0, 0f);
                try { PostProcessing.ApplyQuality(q); }
                catch (Exception e) { Debug.LogWarning("[OTOEKRAN] kademe " + q + " uygulanamadı: " + e.Message); }
                _sampler.Dispose();
                _sampler = new PerfSampler(240);
                yield return Wait(_opt.SettleSeconds + 0.5f);
                yield return Capture("tier" + q + "_" + OtoEkranArgs.Slug(vitrin.ShotNameAt(0)) + ".png", "kademe " + q, q);
            }

            try { PostProcessing.ApplyQuality(AaaBenchmarkBootstrap.BenchmarkTier); }
            catch (Exception) { }
        }

        private static void Step(string text)
        {
            Debug.Log("[OTOEKRAN] adım: " + text);
        }

        private IEnumerator RunMap()
        {
            var scene = _opt.Scene;
            Step("harita modu, sahne=" + scene + " klasör=" + _opt.OutDir);

            // Botlar kapalı: ekran görüntüsü sırasında yapay zeka çalışmasın.
            try { Project.Infrastructure.AI.BotRuntimeGate.ShouldRunBots = () => false; }
            catch (Exception e) { Debug.LogWarning("[OTOEKRAN] adım: bot kapısı kapatılamadı: " + e.Message); }
            Step("botlar kapatıldı (BotRuntimeGate)");

            try
            {
                GameSession.EnsureInitialized();
                GameSession.Mode = GameMode.BattleRoyale;
                GameSession.SetMapOverride(SceneNames.MapIdForScene(scene));
                GameSession.CreateMatchConfig();
                Step("oturum hazır, sahne yükleniyor (GameSession.LoadScene)");
                GameSession.LoadScene(scene);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OTOEKRAN] adım: oturum kurulumu hatası, doğrudan yükleme: " + e.Message);
            }

            var start = Time.realtimeSinceStartup;
            var deadline = start + SceneWaitSeconds;
            var directTried = false;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (SceneManager.GetActiveScene().name == scene)
                    break;
                if (!directTried && Time.realtimeSinceStartup - start > 15f)
                {
                    directTried = true;
                    Step("sahne 15 sn'de açılmadı, SceneManager.LoadScene ile doğrudan yükleniyor");
                    try { SceneManager.LoadScene(scene, LoadSceneMode.Single); }
                    catch (Exception e) { Debug.LogError("[OTOEKRAN] adım: doğrudan yükleme başarısız: " + e.Message); }
                }

                yield return null;
            }

            if (SceneManager.GetActiveScene().name != scene)
            {
                Debug.LogError("[OTOEKRAN] adım: harita sahnesi yüklenmedi: " + scene + " (aktif=" + SceneManager.GetActiveScene().name + ")");
                yield break;
            }

            Step("sahne aktif: " + scene + ", arazi bekleniyor (en çok " + SceneWaitSeconds.ToString("0", CultureInfo.InvariantCulture) + " sn)");
            var terrainDeadline = Time.realtimeSinceStartup + SceneWaitSeconds;
            while (Time.realtimeSinceStartup < terrainDeadline && Terrain.activeTerrain == null)
                yield return null;
            var terrain = Terrain.activeTerrain;
            Step(terrain != null ? "arazi bulundu: " + terrain.name : "arazi bulunamadı (60 sn doldu), yedek bakışlar kullanılacak");

            // WorldMetadata kısa süre daha beklenir (sahne kurulumunda gecikebilir).
            var metaDeadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < metaDeadline && WorldMetadata.Instance == null)
                yield return null;
            var world = WorldMetadata.Instance;
            Step(world != null ? "WorldMetadata hazır" : "WorldMetadata yok, arazi sınırlarından bakış noktaları türetilecek");

            // Sahne kurulumu (arazi, bitki, oyuncu) otursun.
            Step("sahne oturması bekleniyor (8 sn)");
            yield return Wait(8f);
            if (terrain == null)
                terrain = Terrain.activeTerrain;

            // Ekran görüntülerinde HUD/yükleme katmanı olmasın.
            try
            {
                var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
                for (var i = 0; i < canvases.Length; i++)
                    if (canvases[i] != null && canvases[i].renderMode != RenderMode.WorldSpace)
                        canvases[i].enabled = false;
                Step("HUD katmanları gizlendi");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OTOEKRAN] HUD gizlenemedi: " + e.Message);
            }

            _camera = Camera.main;
            if (_camera == null)
            {
                Step("ana kamera yok, yedek kamera oluşturuluyor");
                var go = new GameObject("[OtoEkranKamera]");
                go.tag = "MainCamera";
                _camera = go.AddComponent<Camera>();
                _camera.fieldOfView = 70f;
                go.AddComponent<AudioListener>();
            }

            // Engel kontrolü: küre taraması (fizik) + arazi ağaçları (çarpışmasız olabilirler, konumdan bakılır).
            Func<Vector3, Vector3, bool> viewBlocked = null;
            Func<Vector3, bool> treeNear = null;
            try
            {
                var trees = SnapshotTrees(terrain);
                viewBlocked = (pos, look) => ViewBlocked(trees, pos, look);
                treeNear = pos => TreeWithin(trees, pos, 3f);
                Step("engel kontrolü hazır: " + trees.Length + " arazi ağacı");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OTOEKRAN] adım: engel kontrolü kurulamadı: " + e.Message);
            }

            List<OtoEkranView> views = null;
            if (world != null)
            {
                try
                {
                    views = OtoEkranViewpoints.Build(world.MapCenter, world.MapHalfSize, world.WaterLevel, world.Locations,
                        (x, z) => world.SampleGroundHeight(new Vector3(x, 0f, z)),
                        world.Layout != null ? world.Layout.Roads : null, viewBlocked, treeNear);
                    Step("bakış noktaları WorldMetadata'dan: " + views.Count);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[OTOEKRAN] adım: WorldMetadata bakışları başarısız: " + e.Message);
                }
            }

            if (views == null || views.Count == 0)
            {
                views = FallbackViews(terrain);
                Step("yedek bakış noktaları (arazi sınırları): " + views.Count);
            }

            // Sahne yüklenirken kayıtlı kullanıcı kademesi uygulanmış olabilir: çekim için Ultra'ya geri zorla.
            ForceUltra();
            var tier = QualityTierApplier.LastTier;
            for (var i = 0; i < views.Count; i++)
            {
                var v = views[i];
                Step("bakış " + (i + 1) + "/" + views.Count + ": " + v.Name + " konum=" + v.Position + " hedef=" + v.LookAt
                     + (string.IsNullOrEmpty(v.Looks) ? string.Empty : " — " + v.Looks));
                _hasPose = true;
                _posePosition = v.Position;
                _poseLook = v.LookAt;
                _sampler.Dispose();
                _sampler = new PerfSampler(240);
                // Yeni konuma ışınlanınca arazi/LOD/bitki akışı için ek süre.
                yield return Wait(_opt.SettleSeconds + 1f);
                yield return Capture(OtoEkranArgs.FileName(i + 1, v.Name), v.Name, tier);
            }

            Step("çekimler tamam, çıkılıyor");
        }

        /// <summary>Arazi sınırlarından 5 nokta: merkez ve dört köşe yönü, yükseklik+2 m, arazi merkezine bakış.</summary>
        private static List<OtoEkranView> FallbackViews(Terrain terrain)
        {
            Vector3 origin = Vector3.zero;
            Vector3 size = new Vector3(500f, 50f, 500f);
            if (terrain != null && terrain.terrainData != null)
            {
                origin = terrain.transform.position;
                size = terrain.terrainData.size;
            }

            var cx = origin.x + size.x * 0.5f;
            var cz = origin.z + size.z * 0.5f;
            float H(float x, float z)
            {
                return terrain != null ? terrain.SampleHeight(new Vector3(x, 0f, z)) + origin.y : 0f;
            }

            var center = new Vector3(cx, H(cx, cz) + 3f, cz);
            var views = new List<OtoEkranView>(5);
            var names = new[] { "guney", "kuzey", "dogu", "bati" };
            var offs = new[] { new Vector2(0f, -0.35f), new Vector2(0f, 0.35f), new Vector2(0.35f, 0f), new Vector2(-0.35f, 0f) };
            for (var i = 0; i < 4; i++)
            {
                var x = cx + offs[i].x * size.x;
                var z = cz + offs[i].y * size.z;
                views.Add(new OtoEkranView(names[i], new Vector3(x, H(x, z) + 2f, z), center));
            }

            views.Add(new OtoEkranView("yukaridan", new Vector3(cx, H(cx, cz) + 2f + Mathf.Max(30f, size.x * 0.15f), cz - size.z * 0.05f), center));
            return views;
        }

        // ------------------------------------------------------------- Engel kontrolü

        /// <summary>Taç yarıçapı (m): görüş ışını bir ağacın gövdesine bu kadar yaklaşırsa engelli sayılır.</summary>
        private const float TreeCanopyRadius = 2.2f;

        /// <summary>Küre taraması: yarıçap ve menzil (görev tanımı: 1,5 m yarıçap, ilk 10 m).</summary>
        private const float ViewProbeRadius = 1.5f;
        private const float ViewProbeRange = 10f;

        /// <summary>
        /// Arazi ağaçlarının dünya konumları (x, taban y, z) + yaklaşık taç yüksekliği (w). Arazi ağaçlarının çoğunda
        /// çarpışma yoktur; küre taraması onları göremeyeceği için konumlarından ayrıca denetlenir.
        /// </summary>
        private static Vector4[] SnapshotTrees(Terrain terrain)
        {
            if (terrain == null || terrain.terrainData == null)
                return Array.Empty<Vector4>();
            var data = terrain.terrainData;
            var origin = terrain.transform.position;
            var size = data.size;
            var instances = data.treeInstances;
            var trees = new Vector4[instances.Length];
            for (var i = 0; i < instances.Length; i++)
            {
                var p = instances[i].position;
                trees[i] = new Vector4(
                    origin.x + p.x * size.x,
                    origin.y + p.y * size.y,
                    origin.z + p.z * size.z,
                    Mathf.Max(6f, 14f * instances[i].heightScale));
            }

            return trees;
        }

        /// <summary>Kamera konumunun <paramref name="radius"/> m yakınında (yatay; taç yüksekliği içinde) arazi ağacı var mı?</summary>
        private static bool TreeWithin(Vector4[] trees, Vector3 pos, float radius)
        {
            var r2 = radius * radius;
            for (var i = 0; i < trees.Length; i++)
            {
                var dx = trees[i].x - pos.x;
                var dz = trees[i].z - pos.z;
                if (dx * dx + dz * dz <= r2 && pos.y <= trees[i].y + trees[i].w + 2f)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Bakış yönünde ilk 10 m: 1,5 m yarıçaplı küre taraması (bina/kaya/arazi) veya görüş ışınına
        /// <see cref="TreeCanopyRadius"/> m'den fazla yaklaşan arazi ağacı → engelli.
        /// </summary>
        private static bool ViewBlocked(Vector4[] trees, Vector3 pos, Vector3 lookAt)
        {
            var dir = lookAt - pos;
            var dist = dir.magnitude;
            if (dist < 1e-3f)
                return false;
            dir /= dist;
            var range = Mathf.Min(ViewProbeRange, dist);

            try
            {
                if (Physics.SphereCast(new Ray(pos, dir), ViewProbeRadius, range,
                        Project.Infrastructure.GameLayers.WorldMask, QueryTriggerInteraction.Ignore))
                    return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OTOEKRAN] küre taraması başarısız: " + e.Message);
            }

            // Arazi ağaçları: ışının ilk `range` m'si ağacın dikey silindirinden (gövde+taç) geçiyor mu?
            var dxz2 = dir.x * dir.x + dir.z * dir.z;
            for (var i = 0; i < trees.Length; i++)
            {
                var tx = trees[i].x - pos.x;
                var tz = trees[i].z - pos.z;
                var s = dxz2 > 1e-6f ? Mathf.Clamp((tx * dir.x + tz * dir.z) / dxz2, 0f, range) : 0f;
                var cx = dir.x * s - tx;
                var cz = dir.z * s - tz;
                if (cx * cx + cz * cz > TreeCanopyRadius * TreeCanopyRadius)
                    continue;
                var cy = pos.y + dir.y * s;
                if (cy >= trees[i].y - 1f && cy <= trees[i].y + trees[i].w)
                    return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ Yardımcılar

        private IEnumerator Wait(float seconds)
        {
            var end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end && !_finished)
                yield return null;
        }

        private IEnumerator Capture(string fileName, string label, int tier)
        {
            // Çözünürlük hedeften sapmışsa (pencere/odak değişimi) yeniden uygula; oturması için 2 kare bekle.
            if (Screen.width != _opt.Width || Screen.height != _opt.Height)
            {
                try { Screen.SetResolution(_opt.Width, _opt.Height, FullScreenMode.Windowed); }
                catch (Exception e) { Debug.LogWarning("[OTOEKRAN] çözünürlük yeniden uygulanamadı: " + e.Message); }
                yield return null;
                yield return null;
            }

            yield return new WaitForEndOfFrame();
            try
            {
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                if (tex == null)
                    throw new InvalidOperationException("doku alınamadı");
                var bytes = tex.EncodeToPNG();
                var w = tex.width;
                var h = tex.height;
                Destroy(tex);
                var path = Path.Combine(_opt.OutDir, fileName);
                File.WriteAllBytes(path, bytes);
                _captured++;
                AppendPerf(label, tier, w, h);
                Debug.Log("[OTOEKRAN] " + fileName + " " + w + "x" + h + " (" + bytes.Length / 1024 + " KB)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OTOEKRAN] çekim başarısız (" + fileName + "): " + e.Message);
            }
        }

        private void AppendPerf(string label, int tier, int w, int h)
        {
            var ci = CultureInfo.InvariantCulture;
            _perf.Append(label.Replace(',', ';')).Append(',')
                .Append(tier.ToString(ci)).Append(',')
                .Append(_sampler.Fps.ToString("0.0", ci)).Append(',')
                .Append(_sampler.AverageFrameMs().ToString("0.00", ci)).Append(',')
                .Append(_sampler.OnePercentLowFps().ToString("0.0", ci)).Append(',')
                .Append(_sampler.OnePercentLowMs().ToString("0.00", ci)).Append(',')
                .Append(_sampler.DrawCallsLast.ToString(ci)).Append(',')
                .Append(_sampler.BatchesLast.ToString(ci)).Append(',')
                .Append(_sampler.GcAllocBytesLast.ToString(ci)).Append(',')
                .Append(w.ToString(ci)).Append(',')
                .Append(h.ToString(ci)).AppendLine();
        }

        /// <summary>İç iteratörü istisna yutarak sürer (try/catch içinde yield yok).</summary>
        private IEnumerator Safe(IEnumerator inner, string name)
        {
            while (true)
            {
                bool more;
                try { more = inner.MoveNext(); }
                catch (Exception e)
                {
                    Debug.LogError("[OTOEKRAN] " + name + " hatası: " + e);
                    yield break;
                }

                if (!more)
                    yield break;
                yield return inner.Current;
            }
        }

        private void Finish(string reason)
        {
            if (_finished)
                return;
            _finished = true;
            try
            {
                Directory.CreateDirectory(_opt.OutDir);
                File.WriteAllText(Path.Combine(_opt.OutDir, "perf.csv"), _perf.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OTOEKRAN] perf.csv yazılamadı: " + e.Message);
            }

            // Çekim için zorlanan Ultra kademe geri alınır (önceki kullanıcı kademesi).
            if (_restoreTier >= 0 && _restoreTier != QualityTierApplier.LastTier)
            {
                try
                {
                    PostProcessing.ApplyQuality(_restoreTier);
                    Debug.Log("[OTOEKRAN] kalite kademesi geri yüklendi: " + _restoreTier);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[OTOEKRAN] kademe geri yüklenemedi: " + e.Message);
                }
            }

            Debug.Log(DoneLog + " (" + reason + ", " + _captured + " görüntü)");
            UnityEngine.Application.Quit();
        }
    }
}
