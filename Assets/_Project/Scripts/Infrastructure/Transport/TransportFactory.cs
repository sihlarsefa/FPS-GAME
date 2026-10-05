using Project.Application.Services;
using Project.Core.Domain;

namespace Project.Infrastructure.Transport
{
    /// <summary>İntikal planına göre araç üretir (geçici iskelet).</summary>
    public static class TransportFactory
    {
        public const float DefaultHelicopterAltitude = 90f;

        public static TransportVehicle Create(TeamInsertion plan)
        {
            return plan.Method == InsertionMethod.ArmoredVehicle
                ? ArmoredCarrier.Create(plan)
                : Helicopter.Create(plan, DefaultHelicopterAltitude);
        }
    }
}
