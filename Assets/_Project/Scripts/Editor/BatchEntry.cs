using System;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>Batchmode giriş noktaları (-executeMethod).</summary>
    public static class BatchEntry
    {
        public static void SetupAll()
        {
            Debug.Log("[HAREKÂT] BatchEntry.SetupAll başlıyor…");
            ProjectSetup.RunAllSteps();
            ProjectSetup.RunStep("Ses mikseri (MainMixer)", () => Debug.Log(MixerBuilder.EnsureMixer()));
            Debug.Log("[HAREKÂT] BatchEntry.SetupAll bitti.");
        }

        /// <summary>Fotogrametri doku + HDRI (+ varsa ThirdPartyModelBinder) bağla, doğrula.</summary>
        public static void BindThirdParty()
        {
            Debug.Log("[HAREKÂT] BatchEntry.BindThirdParty başlıyor…");
            Debug.Log(ThirdPartyBinder.BindAll());

            Type modelBinder = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                modelBinder = asm.GetType("Project.EditorTools.ThirdPartyModelBinder", false);
                if (modelBinder != null) break;
            }
            if (modelBinder != null)
            {
                var m = modelBinder.GetMethod("BindAll", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, Type.EmptyTypes, null);
                if (m != null)
                {
                    try { Debug.Log("[HAREKÂT] ThirdPartyModelBinder: " + m.Invoke(null, null)); }
                    catch (Exception e) { Debug.LogError("[HAREKÂT] ThirdPartyModelBinder hatası: " + (e.InnerException ?? e)); }
                }
            }

            Type weaponBinder = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                weaponBinder = asm.GetType("Project.EditorTools.ThirdPartyWeaponBinder", false);
                if (weaponBinder != null) break;
            }
            if (weaponBinder != null)
            {
                var wm = weaponBinder.GetMethod("BindAll", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, Type.EmptyTypes, null);
                if (wm != null)
                {
                    try { Debug.Log("[HAREKÂT] ThirdPartyWeaponBinder: " + wm.Invoke(null, null)); }
                    catch (Exception e) { Debug.LogError("[HAREKÂT] ThirdPartyWeaponBinder hatası: " + (e.InnerException ?? e)); }
                }
            }

            Type packBinder = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                packBinder = asm.GetType("Project.EditorTools.PurchasedPackBinder", false);
                if (packBinder != null) break;
            }
            if (packBinder != null)
            {
                var pm = packBinder.GetMethod("BindAll", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, Type.EmptyTypes, null);
                if (pm != null)
                {
                    try { Debug.Log("[HAREKÂT] PurchasedPackBinder: " + pm.Invoke(null, null)); }
                    catch (Exception e) { Debug.LogError("[HAREKÂT] PurchasedPackBinder hatası: " + (e.InnerException ?? e)); }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log(ContentOverridesValidator.Validate(ContentOverridesSetup.EnsureAsset()));
            AssetDatabase.SaveAssets();
            Debug.Log("[HAREKÂT] BatchEntry.BindThirdParty bitti.");
        }

        /// <summary>Satın alınan Asset Store paketini (Assets/&lt;Paket&gt;/) tara, asker + silahları bağla, doğrula.</summary>
        public static void BindPurchasedPack()
        {
            Debug.Log("[HAREKÂT] BatchEntry.BindPurchasedPack başlıyor…");
            Debug.Log(PurchasedPackBinder.BindAll());
            AssetDatabase.SaveAssets();
            Debug.Log(ContentOverridesValidator.Validate(ContentOverridesSetup.EnsureAsset()));
            AssetDatabase.SaveAssets();
            Debug.Log("[HAREKÂT] BatchEntry.BindPurchasedPack bitti.");
        }

        /// <summary>Ses kapsam denetimi (Docs/SES_KAPSAM.md).</summary>
        public static void SesDenetim() => Debug.Log("[HAREKÂT] SesDenetim\n" + ClipAudit.Audit(true));

        public static void BuildWindowsClient() => BuildTool.BuildWindowsClient();
        public static void BuildWindowsServer() => BuildTool.BuildWindowsServer();
        public static void BuildMac() => BuildTool.BuildMac();
    }
}
