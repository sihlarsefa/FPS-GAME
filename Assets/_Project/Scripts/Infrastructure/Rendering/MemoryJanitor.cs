using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Infrastructure.Rendering
{
    /// <summary>Saf (Unity nesnesiz) yaş/karar kuralları; testlenebilir.</summary>
    public static class MemoryJanitorRules
    {
        /// <summary>Bu kadar son sahnede kullanılan önbellek girdisi korunur.</summary>
        public const int KeepScenes = 2;
        /// <summary>İki temizlik arası en az süre (sn); agresif olmamak için.</summary>
        public const float MinIntervalSeconds = 20f;
        public const string MenuScene = "MainMenu";

        public static bool IsMenu(string scene) => scene == MenuScene;

        /// <summary>Girdi, "sahne sayacı" damgasıyla yaşlanır: current - lastUsed &lt; keep ise korunur.</summary>
        public static bool ShouldKeep(int lastUsedStamp, int currentStamp, int keep = KeepScenes)
        {
            if (lastUsedStamp < 0) return false;
            return currentStamp - lastUsedStamp < Math.Max(1, keep);
        }

        public static int CountEvictable(IList<int> stamps, int currentStamp, int keep = KeepScenes)
        {
            if (stamps == null) return 0;
            var n = 0;
            for (var i = 0; i < stamps.Count; i++)
                if (!ShouldKeep(stamps[i], currentStamp, keep)) n++;
            return n;
        }

        /// <summary>Yalnız Single yükleme ve menü&lt;-&gt;maç geçişinde; aynı sahne yeniden yüklemede ve sık tekrarda değil.</summary>
        public static bool ShouldCleanup(string from, string to, bool single, float secondsSinceLast)
        {
            if (!single || string.IsNullOrEmpty(to)) return false;
            if (secondsSinceLast < MinIntervalSeconds) return false;
            if (from == to) return false;
            return IsMenu(from) != IsMenu(to);
        }

        /// <summary>Menüye dönüşte harita kartları lazım (bırakma); maça girişte bırakılır.</summary>
        public static bool ShouldReleaseMenuArt(string to) => !IsMenu(to);

        public static string FormatLog(string from, string to, float beforeMb, float afterMb)
        {
            var inv = CultureInfo.InvariantCulture;
            return "[BELLEK] " + from + " -> " + to + ": doku " + beforeMb.ToString("0.#", inv) + " MB -> "
                   + afterMb.ToString("0.#", inv) + " MB (" + (afterMb - beforeMb).ToString("+0.#;-0.#;0", inv) + ")";
        }
    }

    /// <summary>
    /// Sahne geçişinde tek seferlik, agresif olmayan bellek hijyeni: kayıtlı bırakıcılar + UnloadUnusedAssets + GC.
    /// Kendi RuntimeInitializeOnLoad aboneliğini kurar; GameSession'a dokunmaz.
    /// </summary>
    public static class MemoryJanitor
    {
        private static readonly List<Action> Releasers = new List<Action>();
        private static string _lastScene;
        private static float _lastCleanup = -999f;
        private static bool _installed;
        private static bool _mapArtResolved;
        private static Action _mapArtRelease;

        /// <summary>Menü sanat önbelleği vb. için bırakıcı kaydı (maça girişte çağrılır).</summary>
        public static void RegisterMenuReleaser(Action release)
        {
            if (release != null && !Releasers.Contains(release)) Releasers.Add(release);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Releasers.Clear(); _lastScene = null; _lastCleanup = -999f; _installed = false;
            _mapArtResolved = false; _mapArtRelease = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            _lastScene = SceneManager.GetActiveScene().name;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                var to = scene.name;
                var now = Time.realtimeSinceStartup;
                var from = _lastScene;
                _lastScene = to;
                if (!MemoryJanitorRules.ShouldCleanup(from, to, mode == LoadSceneMode.Single, now - _lastCleanup)) return;
                _lastCleanup = now;

                var before = TextureMb();
                if (MemoryJanitorRules.ShouldReleaseMenuArt(to))
                {
                    ReleaseMapPreviews();
                    for (var i = 0; i < Releasers.Count; i++)
                        try { Releasers[i](); } catch (Exception e) { Debug.LogWarning("[BELLEK] bırakıcı hata: " + e.Message); }
                }
                Resources.UnloadUnusedAssets();
                GC.Collect();
                // UnloadUnusedAssets eşzamansız ilerler; anlık değer alt sınırdır.
                Debug.Log(MemoryJanitorRules.FormatLog(from, to, before, TextureMb()));
            }
            catch (Exception e) { Debug.LogWarning("[BELLEK] temizlik atlandı: " + e.Message); }
        }

        private static float TextureMb() => Texture.currentTextureMemory / (1024f * 1024f);

        // Infrastructure -> Presentation bağımlılığı olmadan MapPreviewArt.ReleaseMemory (yansıma, bir kez çözülür).
        private static void ReleaseMapPreviews()
        {
            if (!_mapArtResolved)
            {
                _mapArtResolved = true;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("Project.Presentation.UI.MapPreviewArt", false);
                    var m = t?.GetMethod("ReleaseMemory", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (m != null) { _mapArtRelease = (Action)Delegate.CreateDelegate(typeof(Action), m); break; }
                }
            }
            _mapArtRelease?.Invoke();
        }
    }
}
