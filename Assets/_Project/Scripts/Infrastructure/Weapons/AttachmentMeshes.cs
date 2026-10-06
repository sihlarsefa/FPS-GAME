using System.Collections.Generic;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.Weapons
{
    /// <summary>Eklenti türü (kimlikten çözülür; katalogda olmayan holo/açılı kabza/el feneri de tanınır).</summary>
    public enum AttachmentKind
    {
        None = 0, Suppressor, VerticalGrip, AngledGrip, RedDot, Holo, Scope2x, Scope4x, Flashlight, ExtMag, Stock, Scope8x, FlashHider, Compensator
    }

    /// <summary>Eklentinin takıldığı silah soketi.</summary>
    public enum AttachmentSocket
    {
        Muzzle = 0, Sight, GripLeft, Magazine, Stock, Rail
    }

    /// <summary>Tek malzemeli birleşik eklenti parçası: ayrıntılı ve 30 m sonrası için basit mesh.</summary>
    public sealed class AttachmentPartMesh
    {
        public string Name;
        public MaterialId Material;
        public Mesh Detail;
        public Mesh Simple;
    }

    /// <summary>
    /// Prosedürel eklenti mesh'leri (silindir + kutu birleşimleri). Soket yerel uzayı: +Z namlu yönü, +Y yukarı,
    /// başlangıç soketin kendi noktası. Mesh'ler tür başına bir kez üretilir ve önbelleğe alınır (tahsissiz tekrar).
    /// </summary>
    public static class AttachmentMeshes
    {
        public const float LodDistance = 30f;

        private static readonly Dictionary<AttachmentKind, List<AttachmentPartMesh>> Cache = new Dictionary<AttachmentKind, List<AttachmentPartMesh>>();
        private static readonly Dictionary<long, List<AttachmentPartMesh>> MagCache = new Dictionary<long, List<AttachmentPartMesh>>();

        public static AttachmentKind KindOf(string itemId)
        {
            switch (itemId)
            {
                case "att_suppressor": return AttachmentKind.Suppressor;
                case "att_vgrip": return AttachmentKind.VerticalGrip;
                case "att_agrip": case "att_anglegrip": case "att_angledgrip": return AttachmentKind.AngledGrip;
                case "att_reddot": return AttachmentKind.RedDot;
                case "att_holo": case "att_holosight": return AttachmentKind.Holo;
                case "att_scope2x": return AttachmentKind.Scope2x;
                case "att_scope4x": return AttachmentKind.Scope4x;
                case "att_flashlight": case "att_light": return AttachmentKind.Flashlight;
                case "att_scope8x": return AttachmentKind.Scope8x;
                case "att_flashhider": return AttachmentKind.FlashHider;
                case "att_compensator": return AttachmentKind.Compensator;
                case "att_extmag": return AttachmentKind.ExtMag;
                case "att_stock": return AttachmentKind.Stock;
                default: return AttachmentKind.None;
            }
        }

        public static AttachmentSocket SocketOf(AttachmentKind kind)
        {
            switch (kind)
            {
                case AttachmentKind.Suppressor:
                case AttachmentKind.FlashHider:
                case AttachmentKind.Compensator: return AttachmentSocket.Muzzle;
                case AttachmentKind.VerticalGrip:
                case AttachmentKind.AngledGrip: return AttachmentSocket.GripLeft;
                case AttachmentKind.RedDot:
                case AttachmentKind.Holo:
                case AttachmentKind.Scope2x:
                case AttachmentKind.Scope4x:
                case AttachmentKind.Scope8x: return AttachmentSocket.Sight;
                case AttachmentKind.Flashlight: return AttachmentSocket.Rail;
                case AttachmentKind.ExtMag: return AttachmentSocket.Magazine;
                default: return AttachmentSocket.Stock;
            }
        }

        public static IReadOnlyList<AttachmentPartMesh> Parts(AttachmentKind kind)
        {
            if (kind == AttachmentKind.None || kind == AttachmentKind.ExtMag)
                return null;
            if (!Cache.TryGetValue(kind, out var list))
            {
                list = Compose(kind, false, default);
                var simple = Compose(kind, true, default);
                for (var i = 0; i < list.Count; i++)
                    list[i].Simple = FindSimple(simple, list[i].Material, list[i].Detail);
                Cache[kind] = list;
            }

            return list;
        }

        /// <summary>Şarjör büyütücü uzantısı: şarjörün yerel sınırlarına göre (en, boy, derinlik) üretilir.</summary>
        public static IReadOnlyList<AttachmentPartMesh> MagExtension(Bounds magBounds)
        {
            var key = ((long)Mathf.RoundToInt(magBounds.size.x * 1000f) << 40) ^ ((long)Mathf.RoundToInt(magBounds.size.z * 1000f) << 20)
                      ^ (long)Mathf.RoundToInt(magBounds.min.y * 1000f + 5000f);
            if (!MagCache.TryGetValue(key, out var list))
            {
                list = Compose(AttachmentKind.ExtMag, false, magBounds);
                var simple = Compose(AttachmentKind.ExtMag, true, magBounds);
                for (var i = 0; i < list.Count; i++)
                    list[i].Simple = FindSimple(simple, list[i].Material, list[i].Detail);
                MagCache[key] = list;
            }

            return list;
        }

        /// <summary>Sahne yeniden kurulurken (domain reload kapalıyken) önbelleği boşaltır.</summary>
        public static void ClearCache()
        {
            foreach (var kv in Cache)
                DestroyParts(kv.Value);
            foreach (var kv in MagCache)
                DestroyParts(kv.Value);
            Cache.Clear();
            MagCache.Clear();
        }

        private static void DestroyParts(List<AttachmentPartMesh> parts)
        {
            for (var i = 0; i < parts.Count; i++)
            {
                if (parts[i].Detail != null) Object.Destroy(parts[i].Detail);
                if (parts[i].Simple != null) Object.Destroy(parts[i].Simple);
            }
        }

        private static Mesh FindSimple(List<AttachmentPartMesh> simple, MaterialId material, Mesh fallback)
        {
            for (var i = 0; i < simple.Count; i++)
            {
                if (simple[i].Material == material)
                    return simple[i].Detail;
            }

            return fallback;
        }

        // ---------------------------------------------------------------- kompozisyon

        private static List<AttachmentPartMesh> Compose(AttachmentKind kind, bool simple, Bounds mag)
        {
            var set = new PartSet(kind.ToString(), simple);
            var seg = simple ? 6 : 16;
            switch (kind)
            {
                case AttachmentKind.Suppressor:
                {
                    // Pah (montaj bileziği) + silindir gövde + uç kapağı; z: 0 = namlu ucu.
                    var m = set.Mat(MaterialId.GunMetal);
                    m.Cylinder(new Vector3(0f, 0f, 0.095f), 0.0215f, 0.0215f, 0.19f, seg, Quaternion.identity);
                    if (!simple)
                    {
                        m.Cylinder(new Vector3(0f, 0f, -0.012f), 0.0165f, 0.0165f, 0.03f, seg, Quaternion.identity);
                        var d = set.Mat(MaterialId.Black);
                        d.Cylinder(new Vector3(0f, 0f, 0.1935f), 0.0235f, 0.0235f, 0.009f, seg, Quaternion.identity);
                        d.Cylinder(new Vector3(0f, 0f, 0.05f), 0.0222f, 0.0222f, 0.006f, seg, Quaternion.identity);
                        d.Cylinder(new Vector3(0f, 0f, 0.14f), 0.0222f, 0.0222f, 0.006f, seg, Quaternion.identity);
                    }

                    break;
                }
                case AttachmentKind.VerticalGrip:
                {
                    // Üst = soketin 0 noktası, aşağı sarkar.
                    var p = set.Mat(MaterialId.GunPolymer);
                    p.Cylinder(new Vector3(0f, -0.045f, 0f), 0.0155f, 0.0135f, 0.09f, simple ? 6 : 12, Quaternion.Euler(90f, 0f, 0f));
                    if (!simple)
                    {
                        p.Box(new Vector3(0f, 0.004f, 0f), new Vector3(0.036f, 0.012f, 0.05f), Quaternion.identity);
                        set.Mat(MaterialId.Black).Box(new Vector3(0f, -0.092f, 0f), new Vector3(0.03f, 0.006f, 0.03f), Quaternion.identity);
                    }

                    break;
                }
                case AttachmentKind.AngledGrip:
                {
                    var p = set.Mat(MaterialId.GunPolymer);
                    p.Box(new Vector3(0f, -0.022f, 0.026f), new Vector3(0.03f, 0.075f, 0.036f), Quaternion.Euler(-38f, 0f, 0f));
                    if (!simple)
                    {
                        p.Box(new Vector3(0f, 0.004f, 0.004f), new Vector3(0.036f, 0.012f, 0.05f), Quaternion.identity);
                        set.Mat(MaterialId.GunMetal).Box(new Vector3(0f, -0.012f, 0.0f), new Vector3(0.012f, 0.02f, 0.03f), Quaternion.identity);
                    }

                    break;
                }
                case AttachmentKind.RedDot:
                {
                    // Mini kırmızı nokta: ray tabanı + gövde + kapak + kırmızı mercek; z: 0 = soket, öne uzanır.
                    var p = set.Mat(MaterialId.GunPolymer);
                    p.Box(new Vector3(0f, 0.008f, 0.03f), new Vector3(0.028f, 0.016f, 0.07f), Quaternion.identity);
                    p.Box(new Vector3(0f, 0.034f, 0.035f), new Vector3(0.032f, 0.034f, 0.05f), Quaternion.identity);
                    if (!simple)
                    {
                        p.Box(new Vector3(0f, 0.054f, 0.035f), new Vector3(0.03f, 0.006f, 0.052f), Quaternion.identity);
                        set.Mat(MaterialId.Black).Box(new Vector3(0f, 0.035f, 0.011f), new Vector3(0.026f, 0.026f, 0.004f), Quaternion.identity);
                        set.Mat(MaterialId.Red).Box(new Vector3(0f, 0.036f, 0.0595f), new Vector3(0.022f, 0.024f, 0.003f), Quaternion.identity);
                    }

                    break;
                }
                case AttachmentKind.Holo:
                {
                    // Holografik: açık çerçeve (iki yan + tavan) + cam + ray.
                    var p = set.Mat(MaterialId.GunPolymer);
                    p.Box(new Vector3(0f, 0.008f, 0.04f), new Vector3(0.034f, 0.016f, 0.09f), Quaternion.identity);
                    if (simple)
                    {
                        p.Box(new Vector3(0f, 0.04f, 0.04f), new Vector3(0.036f, 0.05f, 0.08f), Quaternion.identity);
                    }
                    else
                    {
                        p.Box(new Vector3(-0.016f, 0.038f, 0.04f), new Vector3(0.004f, 0.045f, 0.085f), Quaternion.identity);
                        p.Box(new Vector3(0.016f, 0.038f, 0.04f), new Vector3(0.004f, 0.045f, 0.085f), Quaternion.identity);
                        p.Box(new Vector3(0f, 0.063f, 0.04f), new Vector3(0.036f, 0.005f, 0.085f), Quaternion.identity);
                        set.Mat(MaterialId.Glass).Box(new Vector3(0f, 0.038f, 0.012f), new Vector3(0.028f, 0.04f, 0.002f), Quaternion.identity);
                        set.Mat(MaterialId.Red).Box(new Vector3(0f, 0.04f, 0.073f), new Vector3(0.012f, 0.006f, 0.003f), Quaternion.identity);
                    }

                    break;
                }
                case AttachmentKind.Scope2x:
                case AttachmentKind.Scope4x:
                case AttachmentKind.Scope8x:
                {
                    var eight = kind == AttachmentKind.Scope8x;
                    var four = kind != AttachmentKind.Scope2x;
                    var len = eight ? 0.34f : four ? 0.26f : 0.14f;
                    var r = eight ? 0.026f : four ? 0.022f : 0.017f;
                    var mt = set.Mat(four ? MaterialId.GunMetal : MaterialId.GunPolymer);
                    var cy = 0.04f + r;
                    mt.Cylinder(new Vector3(0f, cy, len * 0.5f), r, r, len, seg, Quaternion.identity);
                    var rail = set.Mat(MaterialId.GunPolymer);
                    rail.Box(new Vector3(0f, 0.018f, len * 0.5f), new Vector3(0.026f, 0.036f, len * 0.55f), Quaternion.identity);
                    if (!simple)
                    {
                        // Objektif ve oküler çanları, turret topuzu, kırmızı mercek halkası.
                        mt.Cylinder(new Vector3(0f, cy, len + 0.016f), r * 1.35f, r * 1.1f, 0.032f, seg, Quaternion.identity);
                        mt.Cylinder(new Vector3(0f, cy, -0.012f), r * 1.15f, r * 1.3f, 0.026f, seg, Quaternion.identity);
                        mt.Cylinder(new Vector3(0f, cy + r + 0.007f, len * 0.5f), 0.009f, 0.009f, 0.016f, 10, Quaternion.Euler(90f, 0f, 0f));
                        mt.Cylinder(new Vector3(0.0f + r + 0.007f, cy, len * 0.5f), 0.009f, 0.009f, 0.016f, 10, Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(90f, 0f, 0f));
                        set.Mat(MaterialId.Glass).Cylinder(new Vector3(0f, cy, len + 0.0325f), r * 1.2f, r * 1.2f, 0.002f, seg, Quaternion.identity);
                        var dk = set.Mat(MaterialId.Black);
                        dk.Cylinder(new Vector3(0f, cy, len * 0.28f), r * 1.08f, r * 1.08f, 0.012f, seg, Quaternion.identity);
                        dk.Cylinder(new Vector3(0f, cy, len * 0.72f), r * 1.08f, r * 1.08f, 0.012f, seg, Quaternion.identity);
                    }

                    break;
                }
                case AttachmentKind.FlashHider:
                {
                    // Çatal uçlu alev gizleyici: kısa gövde + ön halka + 4 çatal dişi; z: 0 = namlu ucu.
                    var m = set.Mat(MaterialId.GunMetal);
                    m.Cylinder(new Vector3(0f, 0f, 0.035f), 0.0185f, 0.0185f, 0.07f, seg, Quaternion.identity);
                    if (!simple)
                    {
                        var d = set.Mat(MaterialId.Black);
                        d.Cylinder(new Vector3(0f, 0f, 0.0745f), 0.0195f, 0.0195f, 0.009f, seg, Quaternion.identity);
                        for (var k = 0; k < 4; k++)
                        {
                            var rot = Quaternion.Euler(0f, 0f, k * 90f + 45f);
                            m.Box(rot * new Vector3(0.0165f, 0f, 0.087f), new Vector3(0.007f, 0.012f, 0.026f), rot);
                        }
                    }

                    break;
                }
                case AttachmentKind.Compensator:
                {
                    // Kompansatör: kalın gövde + üstte yan menfezler; z: 0 = namlu ucu.
                    var m = set.Mat(MaterialId.GunMetal);
                    m.Cylinder(new Vector3(0f, 0f, 0.04f), 0.0205f, 0.0205f, 0.08f, seg, Quaternion.identity);
                    if (!simple)
                    {
                        var d = set.Mat(MaterialId.Black);
                        d.Box(new Vector3(0f, 0.0195f, 0.045f), new Vector3(0.026f, 0.006f, 0.012f), Quaternion.identity);
                        d.Box(new Vector3(0f, 0.0195f, 0.062f), new Vector3(0.026f, 0.006f, 0.012f), Quaternion.identity);
                        d.Box(new Vector3(0.0195f, 0f, 0.05f), new Vector3(0.006f, 0.02f, 0.012f), Quaternion.identity);
                        d.Box(new Vector3(-0.0195f, 0f, 0.05f), new Vector3(0.006f, 0.02f, 0.012f), Quaternion.identity);
                        m.Cylinder(new Vector3(0f, 0f, 0.083f), 0.0225f, 0.0225f, 0.006f, seg, Quaternion.identity);
                    }

                    break;
                }
                case AttachmentKind.Flashlight:
                {
                    // Namlu altı ray feneri; ışık yok, yalnız gövde. z: 0 = soket, öne uzanır.
                    var m = set.Mat(MaterialId.GunMetal);
                    m.Cylinder(new Vector3(0f, 0f, 0.05f), 0.0145f, 0.0145f, 0.1f, simple ? 6 : 12, Quaternion.identity);
                    if (!simple)
                    {
                        m.Cylinder(new Vector3(0f, 0f, 0.108f), 0.0195f, 0.0155f, 0.022f, 12, Quaternion.identity);
                        set.Mat(MaterialId.White).Cylinder(new Vector3(0f, 0f, 0.1195f), 0.0165f, 0.0165f, 0.002f, 12, Quaternion.identity);
                        var p = set.Mat(MaterialId.GunPolymer);
                        p.Box(new Vector3(0f, 0.018f, 0.03f), new Vector3(0.022f, 0.012f, 0.05f), Quaternion.identity);
                        set.Mat(MaterialId.Black).Box(new Vector3(0f, 0.0155f, 0.03f), new Vector3(0.008f, 0.004f, 0.012f), Quaternion.identity);
                    }

                    break;
                }
                case AttachmentKind.ExtMag:
                {
                    // Şarjör yerel uzayı: alt kenar (min.y) altına uzantı + taban plakası.
                    var w = Mathf.Max(0.02f, mag.size.x) * 1.04f;
                    var d = Mathf.Max(0.02f, mag.size.z) * 1.04f;
                    var cx = mag.center.x;
                    var cz = mag.center.z;
                    var p = set.Mat(MaterialId.GunPolymer);
                    p.Box(new Vector3(cx, mag.min.y - 0.034f, cz), new Vector3(w, 0.072f, d), Quaternion.identity);
                    if (!simple)
                        set.Mat(MaterialId.GunMetal).Box(new Vector3(cx, mag.min.y - 0.0735f, cz), new Vector3(w * 1.06f, 0.008f, d * 1.1f), Quaternion.identity);
                    break;
                }
                case AttachmentKind.Stock:
                {
                    var p = set.Mat(MaterialId.GunPolymer);
                    p.Box(new Vector3(0f, 0.0f, 0f), new Vector3(0.04f, 0.03f, 0.14f), Quaternion.identity);
                    break;
                }
            }

            return set.Finish();
        }

        // ---------------------------------------------------------------- mesh kurucu

        private sealed class PartSet
        {
            private readonly string _name;
            private readonly bool _simple;
            private readonly List<MaterialId> _order = new List<MaterialId>(3);
            private readonly Dictionary<MaterialId, MeshBuilder> _by = new Dictionary<MaterialId, MeshBuilder>(3);

            public PartSet(string name, bool simple)
            {
                _name = name;
                _simple = simple;
            }

            public MeshBuilder Mat(MaterialId id)
            {
                if (!_by.TryGetValue(id, out var b))
                {
                    b = new MeshBuilder();
                    _by[id] = b;
                    _order.Add(id);
                }

                return b;
            }

            public List<AttachmentPartMesh> Finish()
            {
                var list = new List<AttachmentPartMesh>(_order.Count);
                for (var i = 0; i < _order.Count; i++)
                {
                    var mesh = _by[_order[i]].ToMesh(_name + "_" + _order[i] + (_simple ? "_lod" : string.Empty));
                    list.Add(new AttachmentPartMesh { Name = _name + "_" + _order[i], Material = _order[i], Detail = mesh, Simple = mesh });
                }

                return list;
            }
        }

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _v = new List<Vector3>(64);
            private readonly List<Vector3> _n = new List<Vector3>(64);
            private readonly List<Vector2> _uv = new List<Vector2>(64);
            private readonly List<int> _t = new List<int>(128);

            private static readonly Vector3[] FaceN =
                { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

            public void Box(Vector3 center, Vector3 size, Quaternion rot)
            {
                var h = size * 0.5f;
                for (var f = 0; f < 6; f++)
                {
                    var n = FaceN[f];
                    Vector3 a, b;
                    if (f < 2) { a = Vector3.forward; b = Vector3.up; }
                    else if (f < 4) { a = Vector3.right; b = Vector3.forward; }
                    else { a = Vector3.right; b = Vector3.up; }

                    var c = Vector3.Scale(n, h);
                    var ah = Vector3.Scale(a, h);
                    var bh = Vector3.Scale(b, h);
                    var i0 = _v.Count;
                    Add(center + rot * (c - ah - bh), rot * n, 0f, 0f);
                    Add(center + rot * (c - ah + bh), rot * n, 0f, 1f);
                    Add(center + rot * (c + ah + bh), rot * n, 1f, 1f);
                    Add(center + rot * (c + ah - bh), rot * n, 1f, 0f);
                    Tri(i0, i0 + 1, i0 + 2, rot * n);
                    Tri(i0, i0 + 2, i0 + 3, rot * n);
                }
            }

            /// <summary>Z ekseni boyunca silindir/koni (r0 = -Z ucu, r1 = +Z ucu), iki ucu kapalı.</summary>
            public void Cylinder(Vector3 center, float r0, float r1, float length, int seg, Quaternion rot)
            {
                var hl = length * 0.5f;
                var ring = seg + 1;
                var i0 = _v.Count;
                for (var s = 0; s <= seg; s++)
                {
                    var ang = s / (float)seg * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                    var nrm = rot * dir;
                    Add(center + rot * (dir * r0 + new Vector3(0f, 0f, -hl)), nrm, s / (float)seg, 0f);
                    Add(center + rot * (dir * r1 + new Vector3(0f, 0f, hl)), nrm, s / (float)seg, 1f);
                }

                for (var s = 0; s < seg; s++)
                {
                    var a = i0 + s * 2;
                    var avg = (_n[a] + _n[a + 2]).normalized;
                    Tri(a, a + 1, a + 3, avg);
                    Tri(a, a + 3, a + 2, avg);
                }

                Cap(center, r0, -hl, seg, rot, -1f);
                Cap(center, r1, hl, seg, rot, 1f);
                _ = ring;
            }

            private void Cap(Vector3 center, float r, float z, int seg, Quaternion rot, float sign)
            {
                var n = rot * new Vector3(0f, 0f, sign);
                var c = _v.Count;
                Add(center + rot * new Vector3(0f, 0f, z), n, 0.5f, 0.5f);
                for (var s = 0; s <= seg; s++)
                {
                    var ang = s / (float)seg * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                    Add(center + rot * (dir * r + new Vector3(0f, 0f, z)), n, 0.5f + dir.x * 0.5f, 0.5f + dir.y * 0.5f);
                }

                for (var s = 0; s < seg; s++)
                    Tri(c, c + 1 + s, c + 2 + s, n);
            }

            private void Add(Vector3 p, Vector3 n, float u, float v)
            {
                _v.Add(p);
                _n.Add(n);
                _uv.Add(new Vector2(u, v));
            }

            /// <summary>Üçgeni dışa bakan yöne göre (Unity: çapraz çarpım görünür yüzü işaret eder) doğru sarımla ekler.</summary>
            private void Tri(int a, int b, int c, Vector3 outward)
            {
                var cross = Vector3.Cross(_v[b] - _v[a], _v[c] - _v[a]);
                if (Vector3.Dot(cross, outward) >= 0f)
                {
                    _t.Add(a); _t.Add(b); _t.Add(c);
                }
                else
                {
                    _t.Add(a); _t.Add(c); _t.Add(b);
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = "Att_" + name };
                mesh.SetVertices(_v);
                mesh.SetNormals(_n);
                mesh.SetUVs(0, _uv);
                mesh.SetTriangles(_t, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
