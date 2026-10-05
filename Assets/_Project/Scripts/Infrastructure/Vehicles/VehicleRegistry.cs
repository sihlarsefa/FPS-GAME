using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.Vehicles
{
    /// <summary>Sahnedeki sürülebilir araçların statik kaydı.</summary>
    public static class VehicleRegistry
    {
        private static readonly List<DrivableVehicle> Vehicles = new(16);

        public static IReadOnlyList<DrivableVehicle> All => Vehicles;

        public static void Register(DrivableVehicle vehicle)
        {
            if (vehicle == null || Vehicles.Contains(vehicle))
                return;
            Vehicles.Add(vehicle);
        }

        public static void Unregister(DrivableVehicle vehicle)
        {
            if (vehicle == null)
                return;
            Vehicles.Remove(vehicle);
        }

        public static void Clear() => Vehicles.Clear();

        /// <summary>Verilen yarıçap içindeki en yakın (sağlam) araç.</summary>
        public static DrivableVehicle FindNearest(Vector3 pos, float radius)
        {
            DrivableVehicle best = null;
            var bestSqr = radius * radius;
            for (var i = Vehicles.Count - 1; i >= 0; i--)
            {
                var v = Vehicles[i];
                if (v == null)
                {
                    Vehicles.RemoveAt(i);
                    continue;
                }

                if (v.Health <= 0f)
                    continue;

                var sqr = (v.transform.position - pos).sqrMagnitude;
                if (sqr > bestSqr)
                    continue;

                bestSqr = sqr;
                best = v;
            }

            return best;
        }
    }
}
