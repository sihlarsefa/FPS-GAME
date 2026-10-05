using System;
using Project.Application.Services;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Transport
{
    /// <summary>
    /// İntikal planına göre araç üretir: <see cref="InsertionMethod.Helicopter"/> → T-70, <see cref="InsertionMethod.ArmoredVehicle"/>
    /// → Kirpi. WorldMetadata yoksa araçlar aşağı ışın / y = 0 ile çalışır. Bir aracın kurulumu hata verirse diğer türe
    /// düşülür; ikisi de başarısızsa null döner (çağıran timi yerde doğurmalıdır).
    /// </summary>
    public static class TransportFactory
    {
        /// <summary>Helikopter seyir irtifası (arazi üstü, m).</summary>
        public const float DefaultHelicopterAltitude = Helicopter.DefaultAltitude;

        public static TransportVehicle Create(TeamInsertion plan)
        {
            return Create(plan, DefaultHelicopterAltitude);
        }

        /// <summary>İrtifa belirterek (yalnızca helikopter için kullanılır) araç üretir.</summary>
        public static TransportVehicle Create(TeamInsertion plan, float helicopterAltitude)
        {
            var preferArmored = plan.Method == InsertionMethod.ArmoredVehicle;
            var vehicle = TryCreate(plan, preferArmored, helicopterAltitude);
            if (vehicle != null)
                return vehicle;

            Debug.LogWarning("[TransportFactory] Tim " + (plan.Team + 1) + " için " + (preferArmored ? "Kirpi" : "T-70")
                             + " kurulamadı; diğer araç türü deneniyor.");
            return TryCreate(plan, !preferArmored, helicopterAltitude);
        }

        private static TransportVehicle TryCreate(TeamInsertion plan, bool armored, float helicopterAltitude)
        {
            try
            {
                return armored ? ArmoredCarrier.Create(plan) : (TransportVehicle)Helicopter.Create(plan, helicopterAltitude);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return null;
            }
        }
    }
}
