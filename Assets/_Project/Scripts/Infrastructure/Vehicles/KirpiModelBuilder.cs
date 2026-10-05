using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Infrastructure.Vehicles
{
    /// <summary>Kirpi MRAP görsel modeli (Transport modülünden bağımsız).</summary>
    public static class KirpiModelBuilder
    {
        public sealed class Result
        {
            public GameObject Root;
            public Transform Body;
            public Transform DriverSeat;
            public Transform DriverView;
            public Transform[] WheelVisuals; // FL, FR, RL, RR
            public Transform[] PassengerSeats;
            public Transform[] PassengerViews;
            public BoxCollider HullCollider;
        }

        public static Result Build(Transform parent)
        {
            var root = new GameObject("KirpiModel");
            if (parent != null)
                root.transform.SetParent(parent, false);

            var body = StructureKit.CreateBox(root.transform, "Govde", new Vector3(0f, 1.15f, 0f),
                new Vector3(2.35f, 1.35f, 5.1f), Quaternion.identity, MaterialId.VehicleOlive);
            StructureKit.CreateBox(root.transform, "Kabın", new Vector3(0f, 1.85f, 0.55f),
                new Vector3(2.15f, 0.95f, 2.4f), Quaternion.identity, MaterialId.VehicleDark);
            StructureKit.CreateBox(root.transform, "Kaput", new Vector3(0f, 1.45f, 2.05f),
                new Vector3(2.1f, 0.45f, 1.1f), Quaternion.identity, MaterialId.VehicleTan);
            StructureKit.CreateBox(root.transform, "Cam", new Vector3(0f, 2.05f, 1.55f),
                new Vector3(1.7f, 0.55f, 0.08f), Quaternion.identity, MaterialId.Windshield, false);
            StructureKit.CreateBox(root.transform, "Tampon", new Vector3(0f, 0.7f, 2.7f),
                new Vector3(2.2f, 0.35f, 0.25f), Quaternion.identity, MaterialId.MetalDark);
            StructureKit.CreateBox(root.transform, "Kule", new Vector3(0f, 2.55f, -0.2f),
                new Vector3(0.9f, 0.45f, 0.9f), Quaternion.identity, MaterialId.VehicleDark);

            var hull = body.GetComponent<BoxCollider>();
            if (hull == null)
                hull = body.AddComponent<BoxCollider>();
            GameLayers.SetLayerRecursively(root, GameLayers.Vehicle);

            var wheels = new Transform[4];
            wheels[0] = CreateWheel(root.transform, "TekerFL", new Vector3(-1.05f, 0.48f, 1.55f));
            wheels[1] = CreateWheel(root.transform, "TekerFR", new Vector3(1.05f, 0.48f, 1.55f));
            wheels[2] = CreateWheel(root.transform, "TekerRL", new Vector3(-1.05f, 0.48f, -1.55f));
            wheels[3] = CreateWheel(root.transform, "TekerRR", new Vector3(1.05f, 0.48f, -1.55f));

            var driverSeat = new GameObject("SurucuKoltugu").transform;
            driverSeat.SetParent(root.transform, false);
            driverSeat.localPosition = new Vector3(-0.45f, 1.55f, 0.85f);

            var driverView = new GameObject("SurucuKamera").transform;
            driverView.SetParent(root.transform, false);
            driverView.localPosition = new Vector3(-0.35f, 2.15f, 1.1f);

            var passengerSeats = new Transform[9];
            var passengerViews = new Transform[9];
            for (var i = 0; i < 9; i++)
            {
                var row = i / 3;
                var col = i % 3;
                var seat = new GameObject("Yolcu" + (i + 1)).transform;
                seat.SetParent(root.transform, false);
                seat.localPosition = new Vector3(-0.7f + col * 0.7f, 1.45f, -0.35f - row * 0.85f);
                passengerSeats[i] = seat;

                var view = new GameObject("YolcuKamera" + (i + 1)).transform;
                view.SetParent(seat, false);
                view.localPosition = new Vector3(0f, 0.65f, 0.1f);
                passengerViews[i] = view;
            }

            return new Result
            {
                Root = root,
                Body = body.transform,
                DriverSeat = driverSeat,
                DriverView = driverView,
                WheelVisuals = wheels,
                PassengerSeats = passengerSeats,
                PassengerViews = passengerViews,
                HullCollider = hull
            };
        }

        private static Transform CreateWheel(Transform parent, string name, Vector3 localPos)
        {
            var go = StructureKit.CreateCylinder(parent, name, localPos, 0.48f, 0.32f, MaterialId.Tire);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var col = go.GetComponent<Collider>();
            if (col != null)
                Object.Destroy(col);
            return go.transform;
        }
    }
}
