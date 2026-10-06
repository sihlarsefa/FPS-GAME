using System.Text;
using Project.Infrastructure.Characters;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>
    /// Lobi askeri üniformasının neden soluk pembe göründüğünü tanılamak için tek seferlik batch aracı:
    /// ForTeam(0) kamuflajını üretir, malzeme/shader/doku içeriğini Logs/camo_tani.txt'ye yazar.
    /// Çalıştırma: -executeMethod Project.EditorTools.CamoTani.Dump
    /// </summary>
    public static class CamoTani
    {
        public static void Dump()
        {
            var sb = new StringBuilder();
            try
            {
                var look = SoldierLook.ForTeam(0, new System.Random(1));
                sb.AppendLine($"CamoA={look.CamoA} B={look.CamoB} C={look.CamoC} D={look.CamoD} seed={look.CamoSeed}");

                var m = CharacterMaterials.Camo(look.CamoA, look.CamoB, look.CamoC, look.CamoD, look.CamoSeed);
                sb.AppendLine("Camo() null mu: " + (m == null));
                if (m != null)
                {
                    sb.AppendLine("shader=" + m.shader.name);
                    sb.AppendLine("_BaseColor=" + (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "yok"));
                    var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") as Texture2D : null;
                    sb.AppendLine("_BaseMap=" + (tex == null ? "YOK" : tex.name + " " + tex.width + "x" + tex.height));
                    if (tex != null)
                    {
                        var px = tex.GetPixels32();
                        // Köşe + orta örnekler ve ortalama
                        long r = 0, g = 0, b = 0;
                        foreach (var p in px) { r += p.r; g += p.g; b += p.b; }
                        int n = px.Length;
                        sb.AppendLine($"ortalama RGB = ({r / n},{g / n},{b / n})");
                        for (var i = 0; i < 5; i++)
                        {
                            var p = px[(i * 7919 + 13) % n];
                            sb.AppendLine($"ornek{i} = ({p.r},{p.g},{p.b})");
                        }
                    }
                    var bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                    sb.AppendLine("_BumpMap=" + (bump == null ? "YOK" : bump.name));
                    sb.AppendLine("keywords=" + string.Join(",", m.shaderKeywords));
                    sb.AppendLine("renderQueue=" + m.renderQueue);
                }

                // SoldierModel gerçek kurulumda hangi malzemeyi kullanıyor? Küçük bir kukla kur ve üniforma renderer'ını dök.
                var holder = new GameObject("TaniAsker").transform;
                var model = SoldierModel.Build(holder, look, null, false, 0);
                sb.AppendLine("model null mu: " + (model == null));
                if (model != null)
                {
                    foreach (var rend in holder.GetComponentsInChildren<Renderer>(true))
                    {
                        var mat = rend.sharedMaterial;
                        if (mat == null) continue;
                        var nm = rend.gameObject.name;
                        if (nm.Contains("Gövde") || nm.Contains("Govde") || nm.Contains("Leg") || nm.Contains("Bacak") || nm.Contains("Kol") || nm.Contains("Arm") || nm.Contains("Chest") || nm.Contains("Torso") || nm.Contains("Thigh") || nm.Contains("Shin") || nm.Contains("Cloth"))
                            sb.AppendLine($"R {nm}: mat={mat.name} shader={mat.shader.name} base={(mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor").ToString() : "-")} tex={(mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null ? mat.GetTexture("_BaseMap").name : "YOK")}");
                    }
                    Object.DestroyImmediate(holder.gameObject);
                }
            }
            catch (System.Exception e)
            {
                sb.AppendLine("HATA: " + e);
            }

            System.IO.File.WriteAllText("Logs/camo_tani.txt", sb.ToString());
            Debug.Log("[CamoTani] yazıldı: Logs/camo_tani.txt");
            EditorApplication.Exit(0);
        }
    }
}
