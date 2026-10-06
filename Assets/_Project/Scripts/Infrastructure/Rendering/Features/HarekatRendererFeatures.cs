using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Infrastructure.Rendering.Features
{
    /// <summary>HAREKAT özel URP renderer özelliklerinin ortak sözleşmesi: kararlı anahtar + kalite kademesi uygulama.</summary>
    public interface IHarekatFeature
    {
        string FeatureKey { get; }
        /// <summary>Kalite kademesini (0 Düşük .. 3 Ultra) uygular; kademe kapalıysa özellik kendini devre dışı bırakır.</summary>
        void ApplyTier(int tier);
    }

    /// <summary>
    /// Özel renderer özellikleri için taban sınıf. ApplyTier: kademe tablosuna göre SetActive; kullanıcı/ayar kapatması (UserEnabled) ayrıca saygı görür.
    /// Alt sınıflar yalnız TierEnabled/OnTier ve Create/AddRenderPasses yazar.
    /// </summary>
    public abstract class HarekatFeatureBase : ScriptableRendererFeature, IHarekatFeature
    {
        public int Tier { get; private set; } = -1;
        public bool UserEnabled { get; set; } = true;

        public abstract string FeatureKey { get; }
        protected abstract bool TierEnabled(int tier);
        protected virtual void OnTier(int tier) { }

        public void ApplyTier(int tier)
        {
            Tier = tier < 0 ? 0 : tier;
            OnTier(Tier);
            SetActive(UserEnabled && TierEnabled(Tier));
        }

        /// <summary>AddRenderPasses içinde kullanılır: yalnız oyun kameraları (ofscreen/RT, önizleme, sahne, yansıma kameraları hariç).</summary>
        protected static bool IsMainGameCamera(Camera camera)
        {
            if (camera == null)
                return false;
            if (camera.cameraType != CameraType.Game)
                return false;
            return camera.targetTexture == null && !camera.orthographic;
        }
    }

    /// <summary>Gölgelendirici yükleyici: Shader.Find("HAREKAT/...") + tek seferlik uyarı + materyal önbelleği. Bulunamazsa null (özellik sessizce kapanır).</summary>
    public static class HarekatShaders
    {
        private static readonly HashSet<string> Warned = new HashSet<string>();

        public static Material CreateMaterial(string shaderName)
        {
            Shader shader = null;
            try
            {
                shader = Shader.Find(shaderName);
            }
            catch (Exception)
            {
            }

            if (shader == null || !shader.isSupported)
            {
                if (Warned.Add(shaderName))
                    Debug.LogWarning("[HAREKAT] Gölgelendirici bulunamadı/desteklenmiyor: " + shaderName + " (özellik atlandı; Graphics Settings > Always Included Shaders kontrol edin).");
                return null;
            }

            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return mat;
        }

        public static void Destroy(ref Material material)
        {
            if (material == null)
                return;
            if (UnityEngine.Application.isPlaying)
                UnityEngine.Object.Destroy(material);
            else
                UnityEngine.Object.DestroyImmediate(material);
            material = null;
        }
    }

    /// <summary>
    /// HAREKAT özel renderer özellik çerçevesi. Çalışma zamanında aktif UniversalRendererData'ya (yansıma ile m_RendererDataList / m_DefaultRendererIndex)
    /// kayıtlı özellikleri ekler (yoksa) ve kalite kademesine göre açar/kapatır. Idempotenttir; kademe değişimi yalnız SetActive çağırır (renderer yeniden yaratılmaz).
    /// Yeni özellik eklemek: Register("Anahtar", () => ScriptableObject.CreateInstance&lt;X&gt;()) çağırın (X : HarekatFeatureBase ya da IHarekatFeature uygulayan ScriptableRendererFeature).
    /// Entegrasyon: PostProcessing.ApplyQuality (SsaoTuner.Apply yanında) -> HarekatRendererFeatures.Install(level).
    /// </summary>
    public static class HarekatRendererFeatures
    {
        private sealed class Entry
        {
            public string Key;
            public Func<ScriptableRendererFeature> Factory;
        }

        private static readonly List<Entry> Entries = new List<Entry>();
        private static readonly List<ScriptableRendererFeature> Created = new List<ScriptableRendererFeature>();
        private static readonly List<ScriptableRendererData> TouchedData = new List<ScriptableRendererData>();
        private static bool _quitHooked;
        private static bool _warnedNoPipeline;
        private static int _lastTier = -1;

        static HarekatRendererFeatures()
        {
            Register(HarekatContactShadowsFeature.Key, () => ScriptableObject.CreateInstance<HarekatContactShadowsFeature>());
            Register(HarekatSsrLiteFeature.Key, () => ScriptableObject.CreateInstance<HarekatSsrLiteFeature>());
            Register(HarekatAutoExposureFeature.Key, () => ScriptableObject.CreateInstance<HarekatAutoExposureFeature>());
            Register(HarekatSharpenFeature.Key, () => ScriptableObject.CreateInstance<HarekatSharpenFeature>());
        }

        /// <summary>Son uygulanan kalite kademesi (-1: hiç uygulanmadı).</summary>
        public static int LastTier => _lastTier;

        /// <summary>Kayıtlı özellik anahtarları (kayıt sırasıyla).</summary>
        public static IReadOnlyList<string> RegisteredKeys
        {
            get
            {
                var list = new List<string>(Entries.Count);
                foreach (var e in Entries)
                    list.Add(e.Key);
                return list;
            }
        }

        /// <summary>Özellik fabrikası kaydı; aynı anahtar ikinci kez kaydedilmez (ilk kayıt kazanır).</summary>
        public static void Register(string key, Func<ScriptableRendererFeature> factory)
        {
            if (string.IsNullOrEmpty(key) || factory == null)
                return;
            foreach (var e in Entries)
                if (e.Key == key)
                    return;
            Entries.Add(new Entry { Key = key, Factory = factory });
        }

        /// <summary>Aktif renderer verisine (gerekirse) ekler ve kademeyi uygular. Pipeline/renderer yoksa tek uyarı + no-op. Dönüş: aktif özellik sayısı.</summary>
        public static int Install(int tier)
        {
            tier = tier < 0 ? 0 : tier > 3 ? 3 : tier;
            _lastTier = tier;
            try
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                    return 0;

                var datas = FindActiveRendererData();
                if (datas.Count == 0)
                {
                    if (!_warnedNoPipeline)
                    {
                        _warnedNoPipeline = true;
                        Debug.LogWarning("[HAREKAT] Aktif UniversalRendererData bulunamadı; özel renderer özellikleri atlandı.");
                    }
                    return 0;
                }

                var active = 0;
                foreach (var data in datas)
                {
                    var list = data.rendererFeatures;
                    if (list == null)
                        continue;
                    var added = false;
                    foreach (var entry in Entries)
                    {
                        var feature = FindFeature(list, entry.Key);
                        if (feature == null)
                        {
                            try
                            {
                                feature = entry.Factory();
                            }
                            catch (Exception e)
                            {
                                Debug.LogWarning("[HAREKAT] Özellik oluşturulamadı (" + entry.Key + "): " + e.Message);
                                continue;
                            }

                            if (feature == null)
                                continue;
                            feature.name = "HAREKAT " + entry.Key;
                            feature.hideFlags = HideFlags.HideAndDontSave;
                            list.Add(feature);
                            Created.Add(feature);
                            added = true;
                        }

                        if (feature is IHarekatFeature hf)
                            hf.ApplyTier(tier);
                        if (feature.isActive)
                            active++;
                    }

                    if (added)
                    {
                        data.SetDirty(); // renderer yeniden yaratılır -> Create() çağrılır
                        if (!TouchedData.Contains(data))
                            TouchedData.Add(data);
                    }
                }

                HookQuit();
                return active;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[HAREKAT] HarekatRendererFeatures.Install hata: " + e.Message);
                return 0;
            }
        }

        /// <summary>Kademe değişiminde çağrılır (Install ile aynı).</summary>
        public static int Apply(int tier) => Install(tier);

        /// <summary>Anahtara göre özelliği bulur (kurulu değilse null).</summary>
        public static ScriptableRendererFeature Find(string key)
        {
            foreach (var data in FindActiveRendererData())
            {
                var f = data.rendererFeatures != null ? FindFeature(data.rendererFeatures, key) : null;
                if (f != null)
                    return f;
            }
            return null;
        }

        /// <summary>Çalışma zamanında bir özelliği kullanıcı seviyesinde aç/kapat (ör. ayar menüsü). Kademe kapalıysa yine kapalı kalır.</summary>
        public static bool SetUserEnabled(string key, bool enabled)
        {
            if (!(Find(key) is HarekatFeatureBase f))
                return false;
            f.UserEnabled = enabled;
            f.ApplyTier(f.Tier < 0 ? Math.Max(0, _lastTier) : f.Tier);
            return true;
        }

        /// <summary>Eklenen özellikleri geri alır (oynatma bitişi: asset belleğinde HideAndDontSave örnek kalmasın).</summary>
        public static void Uninstall()
        {
            try
            {
                foreach (var data in TouchedData)
                {
                    if (data == null || data.rendererFeatures == null)
                        continue;
                    data.rendererFeatures.RemoveAll(f => f == null || Created.Contains(f));
                    data.SetDirty();
                }
            }
            catch (Exception)
            {
            }

            foreach (var f in Created)
            {
                if (f == null)
                    continue;
                if (UnityEngine.Application.isPlaying)
                    UnityEngine.Object.Destroy(f);
                else
                    UnityEngine.Object.DestroyImmediate(f);
            }
            Created.Clear();
            TouchedData.Clear();
            _lastTier = -1;
        }

        private static void HookQuit()
        {
            if (_quitHooked)
                return;
            _quitHooked = true;
            UnityEngine.Application.quitting += Uninstall;
        }

        private static ScriptableRendererFeature FindFeature(List<ScriptableRendererFeature> list, string key)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var f = list[i];
                if (f != null && f is IHarekatFeature hf && hf.FeatureKey == key)
                    return f;
            }
            return null;
        }

        /// <summary>Varsayılan renderer (m_DefaultRendererIndex) UniversalRendererData'sı; reflection ile (alanlar internal/private).</summary>
        private static List<ScriptableRendererData> FindActiveRendererData()
        {
            var result = new List<ScriptableRendererData>(1);
            var asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null)
                return result;

            var flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            var type = asset.GetType();
            var list = type.GetField("m_RendererDataList", flags)?.GetValue(asset) as IList;
            if (list == null || list.Count == 0)
                return result;

            var idxObj = type.GetField("m_DefaultRendererIndex", flags)?.GetValue(asset);
            var idx = idxObj is int i ? i : 0;
            if (idx < 0 || idx >= list.Count)
                idx = 0;

            if (list[idx] is ScriptableRendererData data && data != null)
                result.Add(data);
            return result;
        }
    }
}
