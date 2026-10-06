using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Project.Presentation.Bootstrap
{
    /// <summary>
    /// Ana menü görsel QA: <c>-otoekran-menu &lt;outDir&gt;</c>. Menüde 6 sn bekler, 3840x2160 (4K varsayılan;
    /// <c>-otoekran-coz &lt;genişlik&gt; &lt;yükseklik&gt;</c> ile değişir) "menu_1.png", 2 sn sonra "menu_2.png"
    /// çeker, "[OTOEKRAN-MENU] bitti" yazıp çıkar. 60 sn sert zaman aşımı. OtoEkranRunner'dan bağımsızdır. Bkz. Tools/UnityVerify/oto_ekran.sh --menu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OtoEkranMenu : MonoBehaviour
    {
        public const string Flag = "-otoekran-menu";
        public const string DoneLog = "[OTOEKRAN-MENU] bitti";
        private const float FirstWait = 6f;
        private const float SecondWait = 2f;
        private const float HardTimeout = 60f;

        private static bool _started;

        private string _outDir;
        private int _width = OtoEkranArgs.DefaultWidth;
        private int _height = OtoEkranArgs.DefaultHeight;
        private float _startTime;
        private bool _finished;

        /// <summary>Komut satırından çıktı klasörünü okur (yoksa false).</summary>
        public static bool TryParse(string[] args, out string outDir)
        {
            outDir = null;
            if (args == null)
                return false;
            for (var i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], Flag, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                    return false;
                outDir = args[i + 1];
                return true;
            }

            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var args = Environment.GetCommandLineArgs();
            if (_started || !TryParse(args, out var dir))
                return;
            _started = true;
            var go = new GameObject("[OtoEkranMenu]");
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<OtoEkranMenu>();
            runner._outDir = dir;
            OtoEkranArgs.ParseResolution(args, out runner._width, out runner._height);
            runner._startTime = Time.realtimeSinceStartup;
            runner.StartCoroutine(runner.Main());
        }

        private IEnumerator Main()
        {
            Debug.Log("[OTOEKRAN-MENU] başladı → " + _outDir + " (" + _width + "x" + _height + ")");
            try
            {
                Directory.CreateDirectory(_outDir);
                Screen.SetResolution(_width, _height, FullScreenMode.Windowed);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OTOEKRAN-MENU] hazırlık: " + e.Message);
            }

            // Çözünürlük değişiminin oturması için en az 2 kare bekle.
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(FirstWait);
            DumpSoldierOutliers();
            yield return Capture("menu_1.png");
            yield return new WaitForSecondsRealtime(SecondWait);
            yield return Capture("menu_2.png");
            Finish();
        }

        /// <summary>Parçalanmış asker tanısı: insan hacmi dışına taşan renderer'ları gövde köküne göre loglar.</summary>
        private static void DumpSoldierOutliers()
        {
            try
            {
                foreach (var model in UnityEngine.Object.FindObjectsByType<Project.Infrastructure.Characters.SoldierModel>(FindObjectsSortMode.None))
                {
                    var kok = model.transform.position;
                    int toplam = 0, sapan = 0;
                    foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    {
                        toplam++;
                        var m = r.bounds.center;
                        var yatay = Vector2.Distance(new Vector2(m.x, m.z), new Vector2(kok.x, kok.z));
                        var boy = m.y - kok.y;
                        if (yatay > 1f || boy < -0.3f || boy > 2.3f || r.bounds.size.magnitude > 4f)
                        {
                            sapan++;
                            if (sapan <= 12)
                                Debug.Log($"[ASKER-SAPAN] {model.transform.parent?.name}/{r.gameObject.name} merkez={m} kok={kok} boyut={r.bounds.size} parentZinciri={Zincir(r.transform)}");
                        }
                    }
                    Debug.Log($"[ASKER-TANI] {model.transform.parent?.name}: renderer={toplam} sapan={sapan} pos={kok}");

                    // Kumaş/ten görünmezliği avı: ana asker için gövde parçalarının malzeme/shader/görünürlüğü.
                    if (model.transform.parent != null && model.transform.parent.name == "LobiAskeri")
                    {
                        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                        {
                            var n = r.gameObject.name;
                            if (!(n.Contains("Thigh") || n.Contains("Shin") || n.Contains("UpperArm") || n.Contains("Forearm") ||
                                  n.Contains("Skull") || n.Contains("Chest") || n.Contains("Abdomen") || n.Contains("Pelvis") ||
                                  n.Contains("Birlesik") || n.Contains("Combined")))
                                continue;
                            var m2 = r.sharedMaterial;
                            var mf = r.GetComponent<MeshFilter>();
                            Debug.Log($"[ASKER-MAT] {n} aktif={r.gameObject.activeInHierarchy} etkin={r.enabled} gorunur={r.isVisible}"
                                + $" mesh={(mf != null && mf.sharedMesh != null ? mf.sharedMesh.name + "/" + mf.sharedMesh.vertexCount : "YOK")}"
                                + $" mat={(m2 == null ? "YOK" : m2.name)} shader={(m2 != null && m2.shader != null ? m2.shader.name : "-")}"
                                + $" desteklenir={(m2 != null && m2.shader != null && m2.shader.isSupported)}"
                                + $" tex={(m2 != null && m2.HasProperty("_BaseMap") && m2.GetTexture("_BaseMap") != null ? m2.GetTexture("_BaseMap").name : "-")}"
                                + $" renk={(m2 != null && m2.HasProperty("_BaseColor") ? m2.GetColor("_BaseColor").ToString() : "-")}"
                                + $" q={(m2 != null ? m2.renderQueue.ToString() : "-")} boyut={r.bounds.size}");
                        }
                    }
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[ASKER-TANI] hata: " + e.Message); }
        }

        private static string Zincir(Transform t)
        {
            var s2 = t.name; var p2 = t.parent;
            for (var i = 0; i < 7 && p2 != null; i++, p2 = p2.parent) s2 = p2.name + "/" + s2;
            return s2;
        }

        private IEnumerator Capture(string fileName)
        {
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
                File.WriteAllBytes(Path.Combine(_outDir, fileName), bytes);
                Debug.Log("[OTOEKRAN-MENU] " + fileName + " " + w + "x" + h);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OTOEKRAN-MENU] çekim başarısız (" + fileName + "): " + e.Message);
            }
        }

        private void Update()
        {
            if (!_finished && Time.realtimeSinceStartup - _startTime > HardTimeout)
            {
                Debug.LogWarning("[OTOEKRAN-MENU] zaman aşımı — sert çıkış");
                Finish();
            }
        }

        private void Finish()
        {
            if (_finished)
                return;
            _finished = true;
            Debug.Log(DoneLog);
            UnityEngine.Application.Quit();
        }
    }
}
