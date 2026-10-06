using System;
using Project.Application.Services;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Prosedürel dekor / prop fabrikası. Hepsi Default katmanında, çarpıştırıcılı.</summary>
    public static class PropFactory
    {
        public static GameObject Sandbags(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var root = Root(parent, "KumTorbası", pos, yaw);
            var n = 3 + (rng?.Next(0, 3) ?? 1);
            for (var i = 0; i < n; i++)
            {
                var x = (i - (n - 1) * 0.5f) * 0.55f;
                StructureKit.CreateBox(root.transform, "Torba" + i, new Vector3(x, 0.25f, 0f),
                    new Vector3(0.5f, 0.45f, 0.35f), Quaternion.identity, MaterialId.Sandbag);
            }
            StructureKit.MarkStatic(root);
            return root;
        }

        public static GameObject SandbagRing(Transform parent, Vector3 pos, float yaw, System.Random rng, float radius = 3.5f)
        {
            var root = Root(parent, "KumTorbasıHalka", pos, yaw);
            var count = 10;
            for (var i = 0; i < count; i++)
            {
                var a = i * (360f / count) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(a) * radius, 0.25f, Mathf.Cos(a) * radius);
                StructureKit.CreateBox(root.transform, "Torba" + i, p, new Vector3(0.55f, 0.45f, 0.35f),
                    Quaternion.Euler(0f, i * (360f / count), 0f), MaterialId.Sandbag);
            }
            StructureKit.MarkStatic(root);
            return root;
        }

        public static GameObject Hesco(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var root = Root(parent, "Hesco", pos, yaw);
            var n = 2 + (rng?.Next(0, 3) ?? 1);
            for (var i = 0; i < n; i++)
            {
                StructureKit.CreateBox(root.transform, "Hesco" + i, new Vector3(i * 1.15f, 0.7f, 0f),
                    new Vector3(1.1f, 1.35f, 1.1f), Quaternion.identity, MaterialId.Hesco);
            }
            StructureKit.MarkStatic(root);
            return root;
        }

        public static GameObject Container(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var root = Root(parent, "Konteyner", pos, yaw);
            StructureKit.CreateBox(root.transform, "Govde", new Vector3(0f, 1.3f, 0f),
                new Vector3(2.4f, 2.5f, 6f), Quaternion.identity, MaterialId.MetalPanel);
            StructureKit.CreateBox(root.transform, "Kapi", new Vector3(0f, 1.2f, 3.05f),
                new Vector3(2.2f, 2.2f, 0.08f), Quaternion.identity, MaterialId.MetalDark, false);
            StructureKit.MarkStatic(root);
            return root;
        }

        public static GameObject AmmoCrate(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var ov = TryOverride(parent, "MühimmatSandığı", "ammo_crate", pos, yaw);
            if (ov != null)
                return ov;
            var root = Root(parent, "MühimmatSandığı", pos, yaw);
            var kasa = StructureKit.CreateBox(root.transform, "Kasa", new Vector3(0f, 0.35f, 0f),
                new Vector3(1.1f, 0.65f, 0.7f), Quaternion.identity, MaterialId.Wood);
            StructureKit.CreateBox(root.transform, "Serit", new Vector3(0f, 0.5f, 0f),
                new Vector3(1.12f, 0.08f, 0.72f), Quaternion.identity, MaterialId.VehicleOlive, false);
            StructureKit.MarkStatic(root);
            MarkWood(kasa, 90f);
            return root;
        }

        public static GameObject Barrel(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var ov = TryOverride(parent, "Varil", "barrel", pos, yaw);
            if (ov != null)
                return ov;
            var root = Root(parent, "Varil", pos, yaw);
            StructureKit.CreateCylinder(root.transform, "Govde", new Vector3(0f, 0.55f, 0f), 0.32f, 1.05f, MaterialId.Rust);
            StructureKit.MarkStatic(root);
            return root;
        }

        public static GameObject Wreck(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var root = Root(parent, "YanmışAraç", pos, yaw);
            StructureKit.CreateBox(root.transform, "Govde", new Vector3(0f, 0.7f, 0f),
                new Vector3(1.8f, 0.9f, 4.2f), Quaternion.Euler(0f, 0f, rng != null && rng.NextDouble() > 0.5 ? 8f : -6f), MaterialId.Rust);
            StructureKit.CreateBox(root.transform, "Kabın", new Vector3(0f, 1.2f, 0.8f),
                new Vector3(1.6f, 0.7f, 1.4f), Quaternion.identity, MaterialId.MetalDark);
            StructureKit.CreateCylinder(root.transform, "Teker1", new Vector3(-0.9f, 0.35f, 1.2f), 0.35f, 0.22f, MaterialId.Tire);
            StructureKit.CreateCylinder(root.transform, "Teker2", new Vector3(0.9f, 0.35f, -1.2f), 0.35f, 0.22f, MaterialId.Tire);
            StructureKit.MarkStatic(root);
            return root;
        }

        public static GameObject Tent(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var root = Root(parent, "AskeriÇadır", pos, yaw);
            StructureKit.CreateBox(root.transform, "Taban", new Vector3(0f, 0.05f, 0f),
                new Vector3(4f, 0.08f, 6f), Quaternion.identity, MaterialId.Dirt, false);
            StructureKit.CreateBox(root.transform, "Cati", new Vector3(0f, 1.6f, 0f),
                new Vector3(4.2f, 0.12f, 6.2f), Quaternion.identity, MaterialId.TentCanvas);
            StructureKit.CreateBox(root.transform, "DuvarL", new Vector3(-2f, 0.9f, 0f),
                new Vector3(0.08f, 1.7f, 6f), Quaternion.identity, MaterialId.TentCanvas);
            StructureKit.CreateBox(root.transform, "DuvarR", new Vector3(2f, 0.9f, 0f),
                new Vector3(0.08f, 1.7f, 6f), Quaternion.identity, MaterialId.TentCanvas);
            StructureKit.CreateBox(root.transform, "Arka", new Vector3(0f, 0.9f, -3f),
                new Vector3(4f, 1.7f, 0.08f), Quaternion.identity, MaterialId.TentCanvas);
            StructureKit.MarkStatic(root);
            return root;
        }

        public static GameObject CamoNet(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var root = Root(parent, "KamuflajAğı", pos, yaw);
            StructureKit.CreateBox(root.transform, "Ag", new Vector3(0f, 2.2f, 0f),
                new Vector3(6f, 0.05f, 4f), Quaternion.Euler(8f, 0f, 0f), MaterialId.CamoNet, false);
            StructureKit.CreateCylinder(root.transform, "Direk1", new Vector3(-2.8f, 1.1f, -1.8f), 0.06f, 2.2f, MaterialId.WoodDark);
            StructureKit.CreateCylinder(root.transform, "Direk2", new Vector3(2.8f, 1.1f, 1.8f), 0.06f, 2.2f, MaterialId.WoodDark);
            StructureKit.MarkStatic(root);
            return root;
        }

        public static GameObject Fence(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var root = Root(parent, "Çit", pos, yaw);
            var len = 4 + (rng?.Next(0, 4) ?? 0);
            var posts = new System.Collections.Generic.List<GameObject>(len * 2);
            for (var i = 0; i < len; i++)
            {
                var direk = StructureKit.CreateBox(root.transform, "Direk" + i, new Vector3(i * 1.2f, 0.7f, 0f),
                    new Vector3(0.1f, 1.4f, 0.1f), Quaternion.identity, MaterialId.WoodDark);
                posts.Add(direk);
                if (i < len - 1)
                {
                    posts.Add(StructureKit.CreateBox(root.transform, "Ray" + i, new Vector3(i * 1.2f + 0.6f, 0.9f, 0f),
                        new Vector3(1.15f, 0.08f, 0.06f), Quaternion.identity, MaterialId.Wood));
                }
            }
            StructureKit.MarkStatic(root);
            for (var i = 0; i < posts.Count; i++)
                MarkWood(posts[i], 30f);
            return root;
        }

        /// <summary>Ahşap kapı (çerçeve + kırılabilir kanat).</summary>
        public static GameObject WoodenDoor(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var root = Root(parent, "AhsapKapi", pos, yaw);
            StructureKit.CreateBox(root.transform, "KapiCerceve", new Vector3(0f, 1.05f, 0f),
                new Vector3(1.2f, 2.1f, 0.14f), Quaternion.identity, MaterialId.WoodDark, false);
            StructureKit.MarkStatic(root);
            try
            {
                // Çerçeve sabit; kanat menteşeli kapı (F: aç/kapat, basılı tut: aralık). Başarısızsa statik kanat yedeği.
                Project.Infrastructure.World.WoodenDoor.Build(root.transform, Vector3.zero, 0f, 1.0f, 2.0f,
                    rng != null && rng.NextDouble() < 0.5);
            }
            catch (System.Exception)
            {
                var kanat = StructureKit.CreateBox(root.transform, "KapiKanat", new Vector3(0f, 1f, 0f),
                    new Vector3(1f, 2f, 0.08f), Quaternion.identity, MaterialId.Wood);
                MarkWood(kanat, 60f);
            }
            return root;
        }

        /// <summary>Parçayı kırılabilir ahşap yapar (statik işaretini kaldırır; kırılınca nesne yok edilir).</summary>
        private static void MarkWood(GameObject go, float hp)
        {
            if (go == null)
                return;
            go.isStatic = false;
            Destructible.Mark(go, DestructibleKind.Wood, MaterialId.Wood, hp);
        }

        public static GameObject HayBale(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var root = Root(parent, "SamanBalyası", pos, yaw);
            StructureKit.CreateCylinder(root.transform, "Balya", new Vector3(0f, 0.45f, 0f), 0.55f, 0.9f, MaterialId.Hay);
            root.transform.GetChild(0).localRotation = Quaternion.Euler(0f, 0f, 90f);
            StructureKit.MarkStatic(root);
            return root;
        }

        public static GameObject Rock(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var root = Root(parent, "Kaya", pos, yaw);
            var s = 0.6f + (float)(rng?.NextDouble() ?? 0.3) * 1.2f;
            StructureKit.CreateBox(root.transform, "Kaya", new Vector3(0f, s * 0.4f, 0f),
                new Vector3(s, s * 0.75f, s * 0.9f), Quaternion.Euler(0f, yaw * 0.3f, 12f), MaterialId.Rock);
            StructureKit.MarkStatic(root);
            return root;
        }

        public static GameObject Hedgehog(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var root = Root(parent, "TanksavarEngeli", pos, yaw);
            StructureKit.CreateBox(root.transform, "A", Vector3.up * 0.7f, new Vector3(0.18f, 1.5f, 0.18f),
                Quaternion.Euler(35f, 0f, 35f), MaterialId.MetalDark);
            StructureKit.CreateBox(root.transform, "B", Vector3.up * 0.7f, new Vector3(0.18f, 1.5f, 0.18f),
                Quaternion.Euler(-35f, 60f, 35f), MaterialId.MetalDark);
            StructureKit.CreateBox(root.transform, "C", Vector3.up * 0.7f, new Vector3(0.18f, 1.5f, 0.18f),
                Quaternion.Euler(35f, -60f, -35f), MaterialId.MetalDark);
            StructureKit.MarkStatic(root);
            return root;
        }

        public static GameObject FlagPole(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var root = Root(parent, "BayrakDireği", pos, yaw);
            StructureKit.CreateCylinder(root.transform, "Direk", new Vector3(0f, 4f, 0f), 0.06f, 8f, MaterialId.MetalDark);
            var flag = StructureKit.CreateBox(root.transform, "Bayrak", new Vector3(0.9f, 7.2f, 0f),
                new Vector3(1.8f, 1.15f, 0.04f), Quaternion.identity, MaterialId.TurkishFlag, false);
            var wave = flag.AddComponent<FlagWave>();
            wave.Initialize(flag.transform);
            StructureKit.MarkStatic(root);
            // Bayrak dalgalanacak — static değil
            flag.isStatic = false;
            return root;
        }

        public static GameObject Helipad(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var root = Root(parent, "Helipad", pos, yaw);
            StructureKit.CreateCylinder(root.transform, "Zemin", new Vector3(0f, 0.04f, 0f), 8f, 0.08f, MaterialId.Concrete);
            StructureKit.CreateBox(root.transform, "H1", new Vector3(0f, 0.1f, 0f), new Vector3(0.5f, 0.05f, 4f),
                Quaternion.identity, MaterialId.White, false);
            StructureKit.CreateBox(root.transform, "H2", new Vector3(0f, 0.1f, 0f), new Vector3(2.2f, 0.05f, 0.5f),
                Quaternion.identity, MaterialId.White, false);
            StructureKit.MarkStatic(root);
            return root;
        }

        public static GameObject Antenna(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var root = Root(parent, "Anten", pos, yaw);
            StructureKit.CreateCylinder(root.transform, "Mast", new Vector3(0f, 6f, 0f), 0.08f, 12f, MaterialId.MetalPanel);
            StructureKit.CreateBox(root.transform, "Anten", new Vector3(0f, 11.5f, 0f),
                new Vector3(2.5f, 0.15f, 0.4f), Quaternion.identity, MaterialId.MetalDark);
            StructureKit.MarkStatic(root);
            return root;
        }

        public static GameObject StreetLamp(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var root = Root(parent, "SokakLambası", pos, yaw);
            StructureKit.CreateCylinder(root.transform, "Direk", new Vector3(0f, 3f, 0f), 0.07f, 6f, MaterialId.MetalDark);
            StructureKit.CreateBox(root.transform, "Kol", new Vector3(0.6f, 5.8f, 0f),
                new Vector3(1.2f, 0.08f, 0.08f), Quaternion.identity, MaterialId.MetalDark);
            StructureKit.CreateBox(root.transform, "Lamba", new Vector3(1.15f, 5.6f, 0f),
                new Vector3(0.35f, 0.2f, 0.35f), Quaternion.identity, MaterialId.Yellow, false);
            StructureKit.MarkStatic(root);
            return root;
        }

        public static GameObject Well(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var root = Root(parent, "Çeşme", pos, yaw);
            StructureKit.CreateCylinder(root.transform, "Havuz", new Vector3(0f, 0.4f, 0f), 1.1f, 0.8f, MaterialId.Stone);
            StructureKit.CreateCylinder(root.transform, "Su", new Vector3(0f, 0.55f, 0f), 0.85f, 0.15f, MaterialId.Water, false);
            StructureKit.CreateBox(root.transform, "Musluk", new Vector3(0f, 1.1f, 0.9f),
                new Vector3(0.15f, 0.8f, 0.15f), Quaternion.identity, MaterialId.MetalDark);
            StructureKit.MarkStatic(root);
            return root;
        }

        public static GameObject TreeStump(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var ov = TryOverride(parent, "Kütük", "stump", pos, yaw);
            if (ov != null)
                return ov;
            var root = Root(parent, "Kütük", pos, yaw);
            StructureKit.CreateCylinder(root.transform, "Kütük", new Vector3(0f, 0.3f, 0f), 0.4f, 0.55f, MaterialId.Bark);
            StructureKit.MarkStatic(root);
            return root;
        }

        /// <summary>Orman içi düşmüş kütük (TreeScatter.ScatterLogSpots tüketicisi); gövde yatay, kök yaw'a göre döner.</summary>
        public static GameObject FallenLog(Transform parent, Vector3 pos, float yaw, float length)
        {
            var root = Root(parent, "DüşmüşKütük", pos, yaw);
            var r = Mathf.Clamp(length * 0.07f, 0.16f, 0.4f);
            var log = StructureKit.CreateCylinder(root.transform, "Gövde", new Vector3(0f, r * 0.85f, 0f), r, length, MaterialId.Bark);
            if (log != null)
                log.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            StructureKit.MarkStatic(root);
            return root;
        }

        public static GameObject Woodpile(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var root = Root(parent, "OdunYığını", pos, yaw);
            var logs = new System.Collections.Generic.List<GameObject>(5);
            for (var i = 0; i < 5; i++)
            {
                var odun = StructureKit.CreateCylinder(root.transform, "Odun" + i, new Vector3(0f, 0.12f + i * 0.14f, (i % 2) * 0.1f),
                    0.1f, 1.4f, MaterialId.WoodDark);
                root.transform.GetChild(i).localRotation = Quaternion.Euler(0f, 0f, 90f);
                logs.Add(odun);
            }
            StructureKit.MarkStatic(root);
            for (var i = 0; i < logs.Count; i++)
                MarkWood(logs[i], 25f);
            return root;
        }

        public static GameObject ConcreteBarrier(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var ov = TryOverride(parent, "BetonBariyer", "barrier", pos, yaw);
            if (ov != null)
                return ov;
            var root = Root(parent, "BetonBariyer", pos, yaw);
            StructureKit.CreateBox(root.transform, "Bariyer", new Vector3(0f, 0.55f, 0f),
                new Vector3(2.2f, 1.05f, 0.55f), Quaternion.identity, MaterialId.Concrete);
            StructureKit.MarkStatic(root);
            return root;
        }

        public static GameObject GuardBooth(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            var root = Root(parent, "NöbetçiKulübesi", pos, yaw);
            StructureKit.CreateBox(root.transform, "Govde", new Vector3(0f, 1.2f, 0f),
                new Vector3(1.6f, 2.3f, 1.6f), Quaternion.identity, MaterialId.Plaster);
            StructureKit.CreateBox(root.transform, "Cati", new Vector3(0f, 2.5f, 0f),
                new Vector3(1.9f, 0.15f, 1.9f), Quaternion.identity, MaterialId.RoofMetal);
            StructureKit.CreateBox(root.transform, "Pencere", new Vector3(0f, 1.5f, 0.82f),
                new Vector3(0.7f, 0.6f, 0.05f), Quaternion.identity, MaterialId.Glass, false);
            StructureKit.MarkStatic(root);
            return root;
        }

        /// <summary>
        /// Rüzgâr türbini: koni benzeri kule (iki kademe silindir), nasel, göbek ve dönen 3 kanat (<see cref="TurbineRotor"/>).
        /// Kanat/göbek çarpıştırıcısızdır; kule çarpışır. Rotor ön yüzü (yaw yönü) +Z'dir.
        /// </summary>
        public static GameObject WindTurbine(Transform parent, Vector3 pos, float yaw, System.Random rng)
        {
            const float towerH = 42f;
            const float bladeLen = 17f;
            var root = Root(parent, "RüzgârTürbini", pos, yaw);
            StructureKit.CreateCylinder(root.transform, "KuleAlt", new Vector3(0f, towerH * 0.25f, 0f), 1.5f, towerH * 0.5f, MaterialId.White);
            StructureKit.CreateCylinder(root.transform, "KuleÜst", new Vector3(0f, towerH * 0.75f, 0f), 1.0f, towerH * 0.5f, MaterialId.White);
            StructureKit.CreateBox(root.transform, "Taban", new Vector3(0f, 0.3f, 0f), new Vector3(4.5f, 0.6f, 4.5f), Quaternion.identity, MaterialId.Concrete);
            StructureKit.CreateBox(root.transform, "Nasel", new Vector3(0f, towerH + 1f, 0.4f), new Vector3(2.4f, 2.4f, 6.5f), Quaternion.identity, MaterialId.White, false);
            var hubPos = new Vector3(0f, towerH + 1f, 4f);
            var rotor = StructureKit.CreateGroup(root.transform, "Rotor", hubPos, Quaternion.identity);
            StructureKit.CreateCylinder(rotor.transform, "Göbek", Vector3.zero, 0.9f, 0.9f, MaterialId.Gray, false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (var i = 0; i < 3; i++)
            {
                var arm = StructureKit.CreateGroup(rotor.transform, "Kanat" + i, Vector3.zero, Quaternion.Euler(0f, 0f, i * 120f));
                StructureKit.CreateBox(arm.transform, "Pala", new Vector3(0f, bladeLen * 0.5f + 0.6f, 0f),
                    new Vector3(1.1f, bladeLen, 0.18f), Quaternion.identity, MaterialId.White, false);
            }

            // Kule/taban/nasel statik; rotor dönecek.
            for (var i = 0; i < root.transform.childCount; i++)
            {
                var c = root.transform.GetChild(i).gameObject;
                if (c != rotor)
                    StructureKit.MarkStatic(c);
            }

            var spin = rotor.AddComponent<TurbineRotor>();
            spin.Initialize(rotor.transform, 22f + (float)(rng?.NextDouble() ?? 0.5) * 16f, (float)(rng?.NextDouble() ?? 0.0) * 360f);
            return root;
        }

        /// <summary>
        /// PropOverride (ContentOverrides.props) varsa prosedürel yerine prefab örneğini yerleştirir; yoksa null (prosedürel kalır).
        /// Prefab collider içermiyorsa renderer sınırlarından BoxCollider eklenir.
        /// </summary>
        private static GameObject TryOverride(Transform parent, string name, string propId, Vector3 pos, float yaw)
        {
            GameObject prefab;
            try
            {
                if (!Project.Infrastructure.Content.ContentOverrides.TryGetProp(propId, out prefab) || prefab == null)
                    return null;
            }
            catch (System.Exception)
            {
                return null;
            }

            var root = Root(parent, name, pos, yaw);
            var inst = UnityEngine.Object.Instantiate(prefab, root.transform);
            inst.name = prefab.name;
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            SetLayerRecursive(inst, GameLayers.Default);
            if (inst.GetComponentInChildren<Collider>(true) == null)
            {
                var r = inst.GetComponentInChildren<Renderer>(true);
                if (r != null)
                {
                    var box = inst.AddComponent<BoxCollider>();
                    var b = r.bounds;
                    box.center = inst.transform.InverseTransformPoint(b.center);
                    box.size = b.size;
                }
            }
            StructureKit.MarkStatic(root);
            return root;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            var t = go.transform;
            for (var i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i).gameObject, layer);
        }

        private static GameObject Root(Transform parent, string name, Vector3 pos, float yaw)
        {
            var go = new GameObject(name);
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.layer = GameLayers.Default;
            return go;
        }
    }

    /// <summary>Rüzgâr türbini rotoru: yerel Z ekseninde sabit hızla döner (derece/sn).</summary>
    public sealed class TurbineRotor : MonoBehaviour
    {
        private Transform _t;
        private float _degPerSec;

        public void Initialize(Transform t, float degreesPerSecond, float startAngle)
        {
            _t = t;
            _degPerSec = degreesPerSecond;
            if (_t != null)
                _t.localRotation = Quaternion.Euler(0f, 0f, startAngle);
        }

        private void Update()
        {
            if (_t != null)
                _t.Rotate(0f, 0f, _degPerSec * Time.deltaTime, Space.Self);
        }
    }

    /// <summary>Türk bayrağı için hafif dalgalanma.</summary>
    public sealed class FlagWave : MonoBehaviour
    {
        private Transform _t;
        private Vector3 _baseScale;
        private float _phase;

        public void Initialize(Transform t)
        {
            _t = t;
            _baseScale = t.localScale;
            _phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }

        private void LateUpdate()
        {
            if (_t == null)
                return;
            var w = 1f + Mathf.Sin(Time.time * 2.2f + _phase) * 0.04f;
            var h = 1f + Mathf.Sin(Time.time * 2.8f + _phase * 1.3f) * 0.03f;
            _t.localScale = new Vector3(_baseScale.x * w, _baseScale.y * h, _baseScale.z);
            _t.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 1.7f + _phase) * 6f, 0f);
        }
    }
}
