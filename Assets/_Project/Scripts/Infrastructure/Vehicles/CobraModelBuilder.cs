using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Infrastructure.Vehicles
{
    /// <summary>Otokar Cobra (4x4 hafif zırhlı) prosedürel modeli; Kirpi ile aynı Result yapısını döndürür.</summary>
    public static class CobraModelBuilder
    {
        public const int Seats = 6;

        public static KirpiModelBuilder.Result Build(Transform parent)
        {
            var root = new GameObject("CobraModel");
            if (parent != null)
                root.transform.SetParent(parent, false);

            var body = StructureKit.CreateBox(root.transform, "Govde", new Vector3(0f, 1.0f, 0f),
                new Vector3(2.1f, 1.1f, 4.4f), Quaternion.identity, MaterialId.VehicleTan);
            StructureKit.CreateBox(root.transform, "Kabin", new Vector3(0f, 1.75f, -0.1f),
                new Vector3(1.95f, 0.7f, 3.1f), Quaternion.identity, MaterialId.VehicleOlive);
            StructureKit.CreateBox(root.transform, "Kaput", new Vector3(0f, 1.3f, 1.85f),
                new Vector3(1.9f, 0.4f, 0.9f), Quaternion.identity, MaterialId.VehicleTan);
            StructureKit.CreateBox(root.transform, "Cam", new Vector3(0f, 1.85f, 1.35f),
                new Vector3(1.55f, 0.45f, 0.08f), Quaternion.identity, MaterialId.Windshield, false);
            StructureKit.CreateBox(root.transform, "Tampon", new Vector3(0f, 0.6f, 2.35f),
                new Vector3(2.0f, 0.3f, 0.22f), Quaternion.identity, MaterialId.MetalDark);
            StructureKit.CreateBox(root.transform, "Kule", new Vector3(0f, 2.2f, -0.6f),
                new Vector3(0.8f, 0.35f, 0.8f), Quaternion.identity, MaterialId.VehicleDark);

            var turretYaw = new GameObject("TaretDonus").transform;
            turretYaw.SetParent(root.transform, false);
            turretYaw.localPosition = new Vector3(0f, 2.45f, -0.6f);
            StructureKit.CreateBox(turretYaw, "TaretGovde", new Vector3(0f, 0.1f, 0f),
                new Vector3(0.45f, 0.25f, 0.55f), Quaternion.identity, MaterialId.MetalDark, false);
            StructureKit.CreateBox(turretYaw, "TaretKalkan", new Vector3(0f, 0.28f, 0.35f),
                new Vector3(0.85f, 0.5f, 0.06f), Quaternion.identity, MaterialId.VehicleDark, false);
            var turretPitch = new GameObject("TaretEgim").transform;
            turretPitch.SetParent(turretYaw, false);
            turretPitch.localPosition = new Vector3(0f, 0.2f, 0.25f);
            StructureKit.CreateBox(turretPitch, "TaretNamlu", new Vector3(0f, 0f, 0.6f),
                new Vector3(0.1f, 0.1f, 1.2f), Quaternion.identity, MaterialId.MetalDark, false);
            var muzzle = new GameObject("TaretNamluUcu").transform;
            muzzle.SetParent(turretPitch, false);
            muzzle.localPosition = new Vector3(0f, 0f, 1.25f);
            var gunnerView = new GameObject("NisanciKamera").transform;
            gunnerView.SetParent(root.transform, false);
            gunnerView.localPosition = new Vector3(0f, 2.75f, -1.1f);

            var hull = body.GetComponent<BoxCollider>();
            if (hull == null)
                hull = body.AddComponent<BoxCollider>();
            GameLayers.SetLayerRecursively(root, GameLayers.Vehicle);

            var c = VehicleConfig.Cobra;
            var wheels = new Transform[4];
            wheels[0] = CreateWheel(root.transform, "TekerFL", new Vector3(-c.TrackX, c.WheelY, c.WheelBaseZ), c.WheelRadius);
            wheels[1] = CreateWheel(root.transform, "TekerFR", new Vector3(c.TrackX, c.WheelY, c.WheelBaseZ), c.WheelRadius);
            wheels[2] = CreateWheel(root.transform, "TekerRL", new Vector3(-c.TrackX, c.WheelY, -c.WheelBaseZ), c.WheelRadius);
            wheels[3] = CreateWheel(root.transform, "TekerRR", new Vector3(c.TrackX, c.WheelY, -c.WheelBaseZ), c.WheelRadius);

            VehicleDetailKit.Decorate(root.transform, VehicleDetailKit.CobraDims, wheels, turretYaw, false);

            var driverSeat = new GameObject("SurucuKoltugu").transform;
            driverSeat.SetParent(root.transform, false);
            driverSeat.localPosition = new Vector3(-0.42f, 1.4f, 0.8f);

            var driverView = new GameObject("SurucuKamera").transform;
            driverView.SetParent(root.transform, false);
            driverView.localPosition = new Vector3(-0.32f, 1.95f, 1.0f);

            var passengerSeats = new Transform[Seats];
            var passengerViews = new Transform[Seats];
            for (var i = 0; i < Seats; i++)
            {
                var row = i / 2;
                var col = i % 2;
                var seat = new GameObject("Yolcu" + (i + 1)).transform;
                seat.SetParent(root.transform, false);
                seat.localPosition = new Vector3(-0.5f + col * 1.0f, 1.35f, 0.1f - row * 0.85f);
                passengerSeats[i] = seat;

                var view = new GameObject("YolcuKamera" + (i + 1)).transform;
                view.SetParent(seat, false);
                view.localPosition = new Vector3(0f, 0.6f, 0.1f);
                passengerViews[i] = view;
            }

            var result = new KirpiModelBuilder.Result
            {
                Root = root,
                Body = body.transform,
                DriverSeat = driverSeat,
                DriverView = driverView,
                WheelVisuals = wheels,
                PassengerSeats = passengerSeats,
                PassengerViews = passengerViews,
                HullCollider = hull,
                TurretYaw = turretYaw,
                TurretPitch = turretPitch,
                TurretMuzzle = muzzle,
                GunnerView = gunnerView
            };
            TryApplyVisualOverride(root, wheels);
            return result;
        }

        /// <summary>ContentOverrides "cobra" prefab'ı: prosedürel görseller gizlenir (collider'lar kalır); Wheel_0..3 varsa tekerler onlara bağlanır.</summary>
        private static void TryApplyVisualOverride(GameObject root, Transform[] wheels)
        {
            try
            {
                if (root == null || !Project.Infrastructure.Content.ContentOverrides.TryGetVehicle(Project.Infrastructure.Content.ContentIds.Cobra, out var prefab) || prefab == null)
                    return;

                var visual = Object.Instantiate(prefab, root.transform, false);
                visual.name = "GovdeHazir";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                var cols = visual.GetComponentsInChildren<Collider>(true);
                for (var i = 0; i < cols.Length; i++)
                {
                    cols[i].enabled = false;
                    Object.Destroy(cols[i]);
                }

                GameLayers.SetLayerRecursively(visual, GameLayers.Vehicle);

                var proc = root.GetComponentsInChildren<Renderer>(true);
                for (var i = 0; i < proc.Length; i++)
                    if (!proc[i].transform.IsChildOf(visual.transform))
                        proc[i].enabled = false;

                var all = visual.GetComponentsInChildren<Transform>(true);
                for (var i = 0; i < wheels.Length; i++)
                    for (var j = 0; j < all.Length; j++)
                        if (all[j].name == "Wheel_" + i)
                        {
                            wheels[i] = all[j];
                            break;
                        }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CobraModelBuilder] Prefab override uygulanamadi: " + e.Message);
            }
        }

        private static Transform CreateWheel(Transform parent, string name, Vector3 localPos, float radius)
        {
            var go = StructureKit.CreateCylinder(parent, name, localPos, radius, 0.28f, MaterialId.Tire);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var col = go.GetComponent<Collider>();
            if (col != null)
                Object.Destroy(col);
            return go.transform;
        }
    }
}
