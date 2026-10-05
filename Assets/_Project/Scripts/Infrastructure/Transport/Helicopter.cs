using Project.Application.Services;
using UnityEngine;

namespace Project.Infrastructure.Transport
{
    /// <summary>T-70 helikopteri (geçici iskelet — tam uygulama yazılıyor).</summary>
    public sealed class Helicopter : TransportVehicle
    {
        private readonly Transform[] _seats = new Transform[10];

        public static Helicopter Create(TeamInsertion plan, float altitude)
        {
            var go = new GameObject("T-70");
            var heli = go.AddComponent<Helicopter>();
            for (var i = 0; i < heli._seats.Length; i++)
            {
                heli._seats[i] = new GameObject("Seat" + i).transform;
                heli._seats[i].SetParent(go.transform, false);
            }
            return heli;
        }

        public override Transform GetSeat(int index) => _seats[Mathf.Abs(index) % _seats.Length];
        public override Transform PassengerViewPoint(int index) => GetSeat(index);
        public override Vector3 GetDisembarkPoint(int index) => GetSeat(index).position;
        public override void Begin() { }
        public override void ReleasePassengers() { }
    }
}
