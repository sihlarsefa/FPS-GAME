using System;
using System.Collections.Generic;
using System.Reflection;
using Project.Infrastructure.Audio.HdrMix;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Project.EditorTools
{
    /// <summary>
    /// Resources/Audio/MainMixer.mixer varlığını üretir: Master → (Muzik, Efekt → (Silah, Darbe, Adim, Arac), Ortam, Konusma, UI).
    /// Açık parametreler: her grup için dB ses (MuzikVol…), Master ses "MasterGainDb", Ortam ses "AmbienceGainDb"
    /// (AudioMix sürer) ve Master üstünde alçak geçiren "WorldLowpass" ile bastırma boğması "MuffleCutoffHz" (ikinci alçak geçiren). AudioMixerController herkese açık bir API değildir;
    /// bu yüzden yansıma (reflection) kullanılır ve her adım korumalıdır — başarısız olursa uyarı yazar, kurulumu bozmaz.
    /// Çalışma zamanı (MixerRouting) varlık yoksa/parametre eksikse sessizce no-op olur.
    /// </summary>
    public static class MixerBuilder
    {
        public const string AssetPath = "Assets/_Project/Resources/Audio/MainMixer.mixer";

        // grup adı → (üst grup, açık parametre adı)
        private static readonly (string group, string parent, string param)[] Layout =
        {
            ("Muzik", "Master", MixerRouting.MuzikParam),
            ("Efekt", "Master", MixerRouting.EfektParam),
            ("Silah", "Efekt", MixerRouting.SilahParam),
            ("Darbe", "Efekt", MixerRouting.DarbeParam),
            ("Adim", "Efekt", MixerRouting.AdimParam),
            ("Arac", "Efekt", MixerRouting.AracParam),
            ("Ortam", "Master", AudioMix.AmbienceParam),
            ("Konusma", "Master", MixerRouting.KonusmaParam),
            ("UI", "Master", MixerRouting.UiParam),
        };

        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        [MenuItem("HAREKÂT/Kurulum/Ses Mikseri Kur")]
        public static void MenuBuild() => Debug.Log(EnsureMixer());

        /// <summary>Eşgüçlü: mixer varsa dokunmaz (kullanıcı ince ayarı korunur), yoksa üretir.</summary>
        public static string EnsureMixer()
        {
            try
            {
                if (AssetDatabase.LoadAssetAtPath<AudioMixer>(AssetPath) != null)
                    return "[HAREKÂT] MainMixer zaten var: " + AssetPath;

                var dir = System.IO.Path.GetDirectoryName(AssetPath);
                if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
                    System.IO.Directory.CreateDirectory(dir);

                var editorAsm = typeof(Editor).Assembly;
                var ctrlType = editorAsm.GetType("UnityEditor.Audio.AudioMixerController");
                var groupType = editorAsm.GetType("UnityEditor.Audio.AudioMixerGroupController");
                if (ctrlType == null || groupType == null)
                    return "[HAREKÂT] MainMixer üretilemedi: AudioMixerController bulunamadı (Unity sürümü API'yi değiştirmiş olabilir).";

                var create = ctrlType.GetMethod("CreateMixerControllerAtPath", All, null, new[] { typeof(string) }, null);
                if (create == null)
                    return "[HAREKÂT] MainMixer üretilemedi: CreateMixerControllerAtPath yok.";
                var controller = create.Invoke(null, new object[] { AssetPath });
                if (controller == null)
                    return "[HAREKÂT] MainMixer üretilemedi: denetleyici oluşturulamadı.";

                var master = ctrlType.GetProperty("masterGroup", All)?.GetValue(controller);
                var groups = new Dictionary<string, object> { { "Master", master } };
                var notes = new List<string>();

                foreach (var (name, parent, _) in Layout)
                {
                    try
                    {
                        var g = Call(controller, "CreateNewGroup", name, true);
                        Call(controller, "AddChildToParent", g, groups[parent]);
                        groups[name] = g;
                    }
                    catch (Exception e)
                    {
                        notes.Add("grup " + name + ": " + Short(e));
                    }
                }

                var exposed = 0;
                // Master: ses → MasterGainDb
                exposed += Expose(controller, groups, "Master", AudioMix.MasterParam, notes) ? 1 : 0;
                foreach (var (name, _, param) in Layout)
                    if (groups.ContainsKey(name))
                        exposed += Expose(controller, groups, name, param, notes) ? 1 : 0;

                try
                {
                    if (AddLowpass(controller, groups["Master"], editorAsm, groupType, notes))
                        exposed++;
                }
                catch (Exception e)
                {
                    notes.Add("lowpass: " + Short(e));
                }

                // Bastırma boğulması: ikinci alçak geçiren, kesim "MuffleCutoffHz" (MixerRouting.SetMuffle sürer).
                try
                {
                    if (AddLowpass(controller, groups["Master"], editorAsm, groupType, notes, MixerRouting.MuffleCutoffParam, 2))
                        exposed++;
                }
                catch (Exception e)
                {
                    notes.Add("muffle lowpass: " + Short(e));
                }

                EditorUtility.SetDirty((UnityEngine.Object)controller);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);

                var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(AssetPath);
                return "[HAREKÂT] MainMixer üretildi: " + (mixer != null ? "yüklendi" : "YÜKLENEMEDİ")
                       + ", grup=" + (groups.Count) + ", açık parametre=" + exposed
                       + (notes.Count > 0 ? " | uyarılar: " + string.Join("; ", notes) : "");
            }
            catch (Exception e)
            {
                return "[HAREKÂT] MainMixer üretimi başarısız (çalışma zamanı betik yoluna düşer): " + Short(e);
            }
        }

        // ------------------------------------------------------------------ yansıma yardımcıları

        private static string Short(Exception e)
        {
            var x = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
            return x.GetType().Name + " " + x.Message;
        }

        /// <summary>Ada ve argüman sayısına göre yöntemi bulup çağırır (parametre türleri sürüme göre değişebilir).</summary>
        private static object Call(object target, string method, params object[] args)
        {
            foreach (var m in target.GetType().GetMethods(All))
            {
                if (m.Name != method)
                    continue;
                var ps = m.GetParameters();
                if (ps.Length != args.Length)
                    continue;
                var ok = true;
                for (var i = 0; i < ps.Length && ok; i++)
                    ok = args[i] == null || ps[i].ParameterType.IsInstanceOfType(args[i]);
                if (ok)
                    return m.Invoke(target, args);
            }

            throw new MissingMethodException(target.GetType().Name + "." + method + "/" + args.Length);
        }

        private static bool Expose(object controller, Dictionary<string, object> groups, string groupName, string paramName, List<string> notes)
        {
            try
            {
                var group = groups[groupName];
                var guid = Call(group, "GetGUIDForVolume");
                return ExposeGuid(controller, group, null, guid, paramName, notes);
            }
            catch (Exception e)
            {
                notes.Add("açık " + paramName + ": " + Short(e));
                return false;
            }
        }

        private static bool ExposeGuid(object controller, object group, object effect, object guid, string paramName, List<string> notes)
        {
            var editorAsm = typeof(Editor).Assembly;
            var ctrlType = controller.GetType();
            object path;
            if (effect == null)
            {
                var t = editorAsm.GetType("UnityEditor.Audio.AudioGroupParameterPath");
                path = Activator.CreateInstance(t, group, guid);
            }
            else
            {
                var t = editorAsm.GetType("UnityEditor.Audio.AudioEffectParameterPath");
                path = Activator.CreateInstance(t, group, effect, guid);
            }

            Call(controller, "AddExposedParameter", path);

            // Ad ata: exposedParameters (struct dizisi: guid + name) içinde ilgili GUID'i bul.
            var prop = ctrlType.GetProperty("exposedParameters", All);
            var arr = (Array)prop.GetValue(controller);
            for (var i = 0; i < arr.Length; i++)
            {
                var item = arr.GetValue(i);
                var it = item.GetType();
                var gf = it.GetField("guid", All);
                var nf = it.GetField("name", All);
                if (gf == null || nf == null)
                    continue;
                if (!gf.GetValue(item).Equals(guid))
                    continue;
                nf.SetValue(item, paramName); // boxed struct üzerinde
                arr.SetValue(item, i);
                prop.SetValue(controller, arr);
                return true;
            }

            notes.Add(paramName + ": eklenen parametre bulunamadı");
            return false;
        }

        private static bool AddLowpass(object controller, object masterGroup, Assembly editorAsm, Type groupType, List<string> notes,
            string paramName = AudioMix.LowpassParam, int insertIndex = 1)
        {
            var effType = editorAsm.GetType("UnityEditor.Audio.AudioMixerEffectController");
            var effect = Activator.CreateInstance(effType, "Lowpass Simple");
            Call(effect, "PreallocateGUIDs");
            AssetDatabase.AddObjectToAsset((UnityEngine.Object)effect, AssetPath);
            var effects = (Array)groupType.GetProperty("effects", All).GetValue(masterGroup);
            // Attenuation sonrası (dizin 1; boğma efekti 2) ekle; Receive/Send yok.
            Call(masterGroup, "InsertEffect", effect, Math.Min(insertIndex, effects.Length));
            var guid = Call(effect, "GetGUIDForParameter", "Cutoff freq");
            return ExposeGuid(controller, masterGroup, effect, guid, paramName, notes);
        }
    }
}
