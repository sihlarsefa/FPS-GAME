using System;
using Project.Core.Domain;

namespace Project.Application.AI
{
    /// <summary>
    /// V-formasyon yürüyüş (saf mantık): lider önde, üyeler arkada iki kanatta açılır.
    /// Slot 0 = lider; tek indeksler sol, çift indeksler sağ kanat.
    /// </summary>
    public static class TeammateFormation
    {
        public const float RowSpacing = 2.5f;
        public const float LateralSpacing = 2.2f;

        /// <summary>Lider yerel koordinatında slot ofseti: X sağ, Z ileri (negatif = arkada).</summary>
        public static Float3 LocalOffset(int slot)
        {
            if (slot <= 0) return Float3.Zero;
            var row = (slot + 1) / 2;
            var side = (slot & 1) == 1 ? -1f : 1f;
            return new Float3(side * row * LateralSpacing, 0f, -row * RowSpacing);
        }

        /// <summary>Dünya konumu: lider pozisyonu ve yönelimi (derece, Unity yaw) verilince slotun hedefi.</summary>
        public static Float3 WorldSlot(Float3 leader, float leaderYawDegrees, int slot)
        {
            var o = LocalOffset(slot);
            var r = leaderYawDegrees * (float)Math.PI / 180f;
            var sin = (float)Math.Sin(r);
            var cos = (float)Math.Cos(r);
            // Yaw: ileri = (sin, cos), sağ = (cos, -sin)
            var x = o.X * cos + o.Z * sin;
            var z = -o.X * sin + o.Z * cos;
            return new Float3(leader.X + x, leader.Y, leader.Z + z);
        }

        /// <summary>Hız: slottan uzaksa koşarak yetiş, yakınsa lider hızına uy, çok yakınsa bekle.</summary>
        public static float SpeedFor(float distanceToSlot, float leaderSpeed, float maxSpeed)
        {
            if (distanceToSlot < 0.5f) return 0f;
            if (distanceToSlot < 2f) return Math.Min(leaderSpeed, maxSpeed);
            return maxSpeed;
        }
    }
}
