using System.Text;
using Project.Infrastructure.Characters;
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    /// <summary>
    /// Parçalanmış asker tanısı: SoldierModel'i batch'te kurar, her Part'ın dünya konumunu kemiğine göre ölçer,
    /// 0,5 m'den fazla sapan parçaları Logs/asker_tani.txt'ye yazar. Birleştirici (SoldierMeshCombiner) açık/kapalı
    /// iki kez kurup fark raporlar. Çalıştırma: -executeMethod Project.EditorTools.AskerTani.Dump
    /// </summary>
    public static class AskerTani
    {
        public static void Dump()
        {
            var sb = new StringBuilder();
            try
            {
                Run(sb, true);
                Run(sb, false);
            }
            catch (System.Exception e)
            {
                sb.AppendLine("HATA: " + e);
            }

            System.IO.File.WriteAllText("Logs/asker_tani.txt", sb.ToString());
            Debug.Log("[AskerTani] yazıldı: Logs/asker_tani.txt");
            EditorApplication.Exit(0);
        }

        private static void Run(StringBuilder sb, bool combine)
        {
            var alan = typeof(SoldierMeshCombiner).GetField("Enabled",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (alan != null)
                alan.SetValue(null, combine);

            sb.AppendLine($"===== BİRLEŞTİRİCİ: {(combine ? "AÇIK" : "KAPALI")} =====");
            var holder = new GameObject("TaniAsker_" + combine).transform;
            holder.position = new Vector3(5f, 0f, 8f); // sıfır dışı test: parçalar gövdeyi izliyor mu?

            var look = SoldierLook.ForTeam(0, new System.Random(1));
            var model = SoldierModel.Build(holder, look, null, false, 0);
            sb.AppendLine("model null mu: " + (model == null));
            if (model != null)
            {
                model.SetEquipment(2, 2, 2);
                model.SetLocomotion(Vector3.zero, Project.Core.Domain.Stance.Standing, true);

                int toplam = 0, sapan = 0;
                var govde = holder.position;
                foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
                {
                    toplam++;
                    var b = r.bounds;
                    var merkez = b.center;
                    var yatay = Vector2.Distance(new Vector2(merkez.x, merkez.z), new Vector2(govde.x, govde.z));
                    var boy = merkez.y - govde.y;
                    // İnsan hacmi dışına taşan her şey şüpheli: yatay > 1 m veya yükseklik [-0.2, 2.2] dışı veya sınır devasa.
                    if (yatay > 1f || boy < -0.2f || boy > 2.2f || b.size.magnitude > 4f)
                    {
                        sapan++;
                        if (sapan <= 40)
                            sb.AppendLine($"SAPAN {r.gameObject.name} merkez={merkez} boyut={b.size} parent={Yol(r.transform)}");
                    }
                }

                sb.AppendLine($"toplam renderer={toplam} sapan={sapan}");
                Object.DestroyImmediate(holder.gameObject);
            }
        }

        private static string Yol(Transform t)
        {
            var s = t.name;
            var p = t.parent;
            for (var i = 0; i < 6 && p != null; i++, p = p.parent)
                s = p.name + "/" + s;
            return s;
        }
    }
}
