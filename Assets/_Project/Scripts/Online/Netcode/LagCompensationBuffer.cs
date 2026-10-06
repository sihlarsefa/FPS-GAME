using Project.Online.Sim;
using UnityEngine;

namespace Project.Online.Netcode
{
    /// <summary>
    /// Son 1 saniyelik hitbox geçmişi (lag compensation). 30 Hz × 1 sn ≈ 30 örnek.
    /// Saf halka tampon <see cref="PositionHistory"/> üzerinde ince sarmalayıcı; örnekler interpolasyonludur.
    /// </summary>
    public sealed class LagCompensationBuffer
    {
        private readonly PositionHistory _history;

        public LagCompensationBuffer(float seconds = 1f, float tickRate = 30f)
        {
            _history = new PositionHistory(seconds, tickRate);
        }

        public PositionHistory History => _history;
        public int Capacity => _history.Capacity;
        public int Count => _history.Count;

        public void Record(uint tick, Vector3 position, Quaternion rotation, float height, float radius)
        {
            _history.Record(tick, Time.time, position, rotation, height, radius);
        }

        /// <summary>serverTick verilirse atış tick'i 1 sn geri sarma sınırına çekilir.</summary>
        public bool TrySample(uint tick, out CapsuleHitbox hitbox, uint serverTick = 0)
        {
            if (serverTick != 0)
                tick = _history.ClampTick(tick, serverTick);
            return ToHitbox(_history.TrySampleTick(tick, out var s), s, out hitbox);
        }

        public bool TrySampleAtTime(float worldTime, out CapsuleHitbox hitbox)
        {
            return ToHitbox(_history.TrySampleTime(worldTime, out var s), s, out hitbox);
        }

        private static bool ToHitbox(bool ok, PositionHistory.Sample s, out CapsuleHitbox hitbox)
        {
            hitbox = ok ? new CapsuleHitbox(s.Position, s.Rotation, s.Height, s.Radius) : default;
            return ok;
        }
    }
}
