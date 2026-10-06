using Project.Core.Domain;
using Project.Infrastructure.Vehicles;
using UnityEngine;
using UnityEngine.AI;

namespace Project.Infrastructure.AI
{
    /// <summary>Botun araç koltuğundaki durumunu tutar (BotController'a dokunmadan).</summary>
    public sealed class BotVehicleSeatTag : MonoBehaviour
    {
        public DrivableVehicle Vehicle;
        public int Seat;
        public Transform Home;
    }

    /// <summary>
    /// Botları sürülebilir araca yolcu olarak bindirir/indirir. Biniş: bot denetleyicisi, ajan ve çarpıştırıcı
    /// kapatılır, gövde koltuğa oturur (oturan model). İniş: araç yanına NavMesh'e oturtulur, her şey geri açılır.
    /// </summary>
    public static class BotVehicleBoarding
    {
        public static bool IsBoarded(BotController bot) => bot != null && bot.GetComponent<BotVehicleSeatTag>() != null;

        public static bool TryBoard(BotController bot, DrivableVehicle vehicle, out int seat)
        {
            seat = -1;
            if (bot == null || vehicle == null || bot.Combatant == null || bot.Combatant.IsDowned || !bot.Combatant.IsAlive)
                return false;
            if (!bot.HasLanded || bot.IsSeated || IsBoarded(bot) || !bot.isActiveAndEnabled)
                return false;
            if (!vehicle.TryEnterPassenger(bot.Combatant, out seat))
                return false;

            bot.PrepareForVehicle();

            var tag = bot.gameObject.AddComponent<BotVehicleSeatTag>();
            tag.Vehicle = vehicle;
            tag.Seat = seat;
            tag.Home = bot.transform.parent;

            bot.enabled = false;
            var agent = bot.GetComponent<NavMeshAgent>();
            if (agent != null)
                agent.enabled = false;
            var col = bot.GetComponent<CapsuleCollider>();
            if (col != null)
                col.enabled = false;

            var seatTransform = vehicle.GetPassengerSeat(seat);
            bot.transform.SetParent(seatTransform, false);
            bot.transform.localPosition = Vector3.zero;
            bot.transform.localRotation = Quaternion.identity;

            bot.Combatant.Velocity = Vector3.zero;
            bot.Combatant.Stance = Stance.Standing;
            if (bot.Model != null)
            {
                // Denetleyici kapalı: koşu animasyonu son hızda takılı kalmasın.
                bot.Model.SetLocomotion(Vector3.zero, Stance.Standing, false);
                bot.Model.SetSeated(true);
            }

            return true;
        }

        /// <summary>
        /// Botu indirir. vehicleSlotAlreadyFreed=true: araç koltuğu zaten bıraktı (patlama), yalnızca bot geri açılır.
        /// </summary>
        public static void Unboard(BotController bot, bool vehicleSlotAlreadyFreed = false)
        {
            if (bot == null)
                return;
            var tag = bot.GetComponent<BotVehicleSeatTag>();
            if (tag == null)
                return;

            var vehicle = tag.Vehicle;
            var seat = tag.Seat;
            var home = tag.Home;
            Object.Destroy(tag);

            Vector3 point = bot.transform.position;
            if (vehicle != null)
            {
                var vt = vehicle.transform;
                if (!vehicleSlotAlreadyFreed && vehicle.GetPassenger(seat) == bot.Combatant)
                    vehicle.ExitPassenger(seat);
                point = ExitPoint(vt.position, vt.right, vt.forward, seat);
            }

            bot.transform.SetParent(home, true);
            var yaw = vehicle != null ? vehicle.transform.eulerAngles.y : bot.transform.eulerAngles.y;

            // Ölü bot (araçta öldü): yalnızca araç dışına bırakılır; ajan/çarpıştırıcı/denetleyici kapalı kalır (ceset).
            if (bot.IsDeadBot || bot.Combatant == null || !bot.Combatant.IsAlive)
            {
                bot.transform.SetPositionAndRotation(SnapToGround(point), Quaternion.Euler(0f, yaw, 0f));
                return;
            }

            if (bot.Model != null)
                bot.Model.SetSeated(false);

            bot.enabled = true;
            bot.ResumeAfterVehicle(point, yaw);
        }

        /// <summary>Koltuk numarasına göre araç yanında dağılmış iniş noktası (saf hesap).</summary>
        public static Vector3 ExitPoint(Vector3 origin, Vector3 right, Vector3 forward, int seat)
        {
            var side = seat % 2 == 0 ? 1f : -1f;
            var row = seat / 2;
            return origin + right * (side * 2.6f) - forward * (row * 0.9f - 1.2f);
        }

        private static Vector3 SnapToGround(Vector3 p)
        {
            if (NavMesh.SamplePosition(p, out var nav, 5f, NavMesh.AllAreas))
                return nav.position;
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 10f, GameLayers.GroundMask,
                    QueryTriggerInteraction.Ignore))
                return hit.point;
            return p;
        }
    }
}
