using UnityEngine;

namespace Project.Infrastructure.Characters
{
    /// <summary>
    /// Asker iskeleti ayakta duruş yerleşimi (saf mantık, test edilebilir). SoldierModel ölçüleri buradan okur;
    /// parça ağlarının kemik-yerel yükseklik aralıkları BuildBody ile aynı tutulur (test: boşluk &lt; 0,25 m, sıralama baş &gt; göğüs &gt; kalça &gt; diz &gt; ayak).
    /// </summary>
    public static class SoldierRigMath
    {
        public const float StandHipHeight = 0.955f;
        public const float HipJointY = -0.07f;
        public const float ThighLength = 0.42f;
        public const float ShinLength = 0.40f;
        public const float AnkleHeight = 0.08f;

        public const float SpineY = 0.08f;
        public const float ChestY = 0.2f;
        public const float NeckY = 0.27f;
        public const float HeadY = 0.07f;

        /// <summary>Bir parçanın dünya (zemin = 0) dikey kapsamı.</summary>
        public readonly struct Segment
        {
            public readonly string Name;
            public readonly float Min;
            public readonly float Max;

            public Segment(string name, float min, float max)
            {
                Name = name;
                Min = min;
                Max = max;
            }
        }

        /// <summary>Ayakta (hız 0) parça dikey kapsamları, yukarıdan aşağıya: baş, boyun, göğüs, karın, kalça, uyluk, baldır.</summary>
        public static Segment[] StandingSegments()
        {
            var hips = StandHipHeight;
            var spine = hips + SpineY;
            var chest = spine + ChestY;
            var neck = chest + NeckY;
            var head = neck + HeadY;
            var hipJoint = hips + HipJointY;
            var knee = hipJoint - ThighLength;
            return new[]
            {
                new Segment("Head", head - 0.021f, head + 0.215f),
                new Segment("Neck", neck - 0.05f, neck + 0.09f),
                new Segment("Chest", chest, chest + 0.285f),
                new Segment("Abdomen", spine, spine + 0.21f),
                new Segment("Pelvis", hips - 0.12f, hips + 0.08f),
                new Segment("Thigh", hipJoint - 0.435f, hipJoint + 0.03f),
                new Segment("Shin", knee - 0.38f, knee + 0.02f)
            };
        }

        /// <summary>Ayak bileği yüksekliği (ayakta, zemin = 0): hedef AnkleHeight, bacak erişimiyle sınırlı.</summary>
        public static float StandingAnkleY()
        {
            var hipJoint = StandHipHeight + HipJointY;
            var drop = Mathf.Min(hipJoint - AnkleHeight, (ThighLength + ShinLength) * 0.9995f);
            return hipJoint - drop;
        }

        public static bool IsFinite(Quaternion q)
        {
            return IsFinite(q.x) && IsFinite(q.y) && IsFinite(q.z) && IsFinite(q.w);
        }

        private static bool IsFinite(float f)
        {
            return !float.IsNaN(f) && !float.IsInfinity(f);
        }
    }
}
