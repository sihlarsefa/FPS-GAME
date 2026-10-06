using System;
using System.Collections.Generic;
using Project.Application.Services;
using UnityEngine;

namespace Project.Infrastructure.Transport
{
    /// <summary>
    /// Override araç prefab'ı soket çözümleyici (Rotor_Main/Tail/Blur, Wheel_*, Door_*, Seat_N, Light_*) ve
    /// temiz/kirli/yanmış materyal durumu. Soket yoksa null döner; çağıran prosedürel yola düşer.
    /// </summary>
    internal static class VehicleSockets
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        public static Transform Find(GameObject root, string[] aliases)
        {
            if (root == null || aliases == null)
                return null;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
                if (VehicleSocketRules.Matches(all[i].name, aliases))
                    return all[i];
            return null;
        }

        /// <summary>Door_* öneki taşıyan tüm kapılar (Door/Door_Rear dahil).</summary>
        public static List<Transform> FindDoors(GameObject root)
        {
            var result = new List<Transform>();
            if (root == null)
                return result;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
                if (VehicleSocketRules.HasPrefix(all[i].name, VehicleSocketRules.DoorPrefix) || VehicleSocketRules.Matches(all[i].name, new[] { "Door" }))
                    result.Add(all[i]);
            return result;
        }

        public static List<Transform> FindLights(GameObject root)
        {
            var result = new List<Transform>();
            if (root == null)
                return result;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
                if (VehicleSocketRules.HasPrefix(all[i].name, VehicleSocketRules.LightPrefix))
                    result.Add(all[i]);
            return result;
        }

        /// <summary>Light_* soketlerindeki Light bileşenlerini ve renderer'ları açar/kapatır.</summary>
        public static void SetLights(GameObject root, bool on)
        {
            var lights = FindLights(root);
            for (var i = 0; i < lights.Count; i++)
            {
                var l = lights[i].GetComponent<Light>();
                if (l != null)
                    l.enabled = on;
                var r = lights[i].GetComponent<Renderer>();
                if (r != null)
                    r.enabled = on;
            }
        }

        /// <summary>
        /// Seat_N soketleri prosedürel koltuğa yeterince yakınsa koltuk dönüşümünü ona oturtur (yön korunur).
        /// Uzaksa sessizce prosedürel koltuk korunur. Döner: oturtulan koltuk sayısı.
        /// </summary>
        public static int SnapSeats(GameObject root, Transform[] seats)
        {
            if (root == null || seats == null)
                return 0;
            var snapped = 0;
            for (var i = 0; i < seats.Length; i++)
            {
                var seat = seats[i];
                var socket = Find(root, VehicleSocketRules.SeatAliases(i));
                if (seat == null || socket == null)
                    continue;
                var d = seat.position - socket.position;
                if (!VehicleSocketRules.IsSeatTrustworthy(d.x, d.y, d.z))
                {
                    Debug.LogWarning("[Transport] " + socket.name + " koltuk noktası prosedürel koltuktan çok uzak; yok sayıldı.");
                    continue;
                }

                seat.position = socket.position;
                snapped++;
            }

            return snapped;
        }

        /// <summary>Rotor kanatlarının renderer'ları (bulanık disk geçişinde gizlenir); blur soketi hariç.</summary>
        public static Renderer[] BladeRenderers(Transform rotor, Renderer blur)
        {
            if (rotor == null)
                return new Renderer[0];
            var all = rotor.GetComponentsInChildren<Renderer>(true);
            var list = new List<Renderer>(all.Length);
            for (var i = 0; i < all.Length; i++)
                if (all[i] != blur)
                    list.Add(all[i]);
            return list.ToArray();
        }

        /// <summary>Durumu MaterialPropertyBlock ile uygular (paylaşılan materyaller değişmez); Clean = sıfırlama.</summary>
        public static void ApplyCondition(GameObject root, VehicleCondition condition)
        {
            if (root == null)
                return;
            try
            {
                VehicleSocketRules.ConditionTint(condition, out var r, out var g, out var b, out var smooth);
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                var block = new MaterialPropertyBlock();
                for (var i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    if (renderer == null)
                        continue;
                    if (condition == VehicleCondition.Clean)
                    {
                        renderer.SetPropertyBlock(null);
                        continue;
                    }

                    var shared = renderer.sharedMaterial;
                    if (shared == null)
                        continue;
                    renderer.GetPropertyBlock(block);
                    if (shared.HasProperty(BaseColorId))
                    {
                        var c = shared.GetColor(BaseColorId);
                        block.SetColor(BaseColorId, new Color(c.r * r, c.g * g, c.b * b, c.a));
                    }
                    else if (shared.HasProperty(ColorId))
                    {
                        var c = shared.GetColor(ColorId);
                        block.SetColor(ColorId, new Color(c.r * r, c.g * g, c.b * b, c.a));
                    }

                    if (shared.HasProperty(SmoothnessId))
                        block.SetFloat(SmoothnessId, shared.GetFloat(SmoothnessId) * smooth);
                    renderer.SetPropertyBlock(block);
                    block.Clear();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Transport] Arac durumu uygulanamadi: " + e.Message);
            }
        }
    }
}
