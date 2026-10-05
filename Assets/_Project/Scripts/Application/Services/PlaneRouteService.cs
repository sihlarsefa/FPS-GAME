using System;
using Project.Core.Domain;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>
    /// Rastgele uçuş hattı: rastgele yön, merkezden en fazla mapHalfSize*0.45 kaydırılmış bir doğru;
    /// başlangıç/bitiş harita kenarının ~350 m dışında, Y = altitude.
    /// Ayrıca helikopter intikali için kenardan LZ'ye uçuş rotası üretir.
    /// </summary>
    public static class PlaneRouteService
    {
        public const float MaxCenterOffsetFraction = 0.45f;
        public const float OutsideMargin = 350f;
        public const float DefaultSpeed = 38f;
        public const float DefaultHalfSize = 512f;

        public static PlaneRoute CreateRandom(IRandom random, float mapHalfSize, float altitude, float speed)
        {
            if (mapHalfSize <= 1f || float.IsNaN(mapHalfSize) || float.IsInfinity(mapHalfSize))
                mapHalfSize = DefaultHalfSize;
            if (float.IsNaN(altitude) || float.IsInfinity(altitude))
                altitude = 0f;
            speed = SanitizeSpeed(speed);

            var angle = random != null ? random.NextFloat() * Math.PI * 2.0 : 0.0;
            var offsetFraction = random != null ? random.Range(-MaxCenterOffsetFraction, MaxCenterOffsetFraction) : 0f;

            var dirX = (float)Math.Sin(angle);
            var dirZ = (float)Math.Cos(angle);

            // Uçuş yönüne dik eksende kaydırılmış merkez noktası.
            var offset = offsetFraction * mapHalfSize;
            var centerX = dirZ * offset;
            var centerZ = -dirX * offset;

            var forward = DistanceToSquareEdge(centerX, centerZ, dirX, dirZ, mapHalfSize);
            var backward = DistanceToSquareEdge(centerX, centerZ, -dirX, -dirZ, mapHalfSize);

            var start = new Float3(centerX - dirX * (backward + OutsideMargin), altitude, centerZ - dirZ * (backward + OutsideMargin));
            var end = new Float3(centerX + dirX * (forward + OutsideMargin), altitude, centerZ + dirZ * (forward + OutsideMargin));
            return new PlaneRoute(start, end, speed);
        }

        /// <summary>Helikopter intikal rotası: Start = harita kenarındaki başlangıç (irtifada), End = LZ üstü (irtifada).</summary>
        public static PlaneRoute CreateForInsertion(TeamInsertion insertion, float altitude, float speed)
        {
            if (float.IsNaN(altitude) || float.IsInfinity(altitude))
                altitude = 0f;

            var start = new Float3(insertion.Start.X, altitude, insertion.Start.Z);
            var end = new Float3(insertion.LandingZone.X, altitude, insertion.LandingZone.Z);
            return new PlaneRoute(start, end, SanitizeSpeed(speed));
        }

        /// <summary>(x,z) noktasından (dx,dz) yönünde kare harita kenarına mesafe (nokta içerideyse).</summary>
        private static float DistanceToSquareEdge(float x, float z, float dx, float dz, float halfSize)
        {
            var best = float.MaxValue;
            if (Math.Abs(dx) > 1e-6f)
            {
                var t = ((dx > 0f ? halfSize : -halfSize) - x) / dx;
                if (t >= 0f && t < best)
                    best = t;
            }

            if (Math.Abs(dz) > 1e-6f)
            {
                var t = ((dz > 0f ? halfSize : -halfSize) - z) / dz;
                if (t >= 0f && t < best)
                    best = t;
            }

            return best == float.MaxValue ? halfSize : best;
        }

        private static float SanitizeSpeed(float speed) => speed > 0.01f && !float.IsNaN(speed) && !float.IsInfinity(speed) ? speed : DefaultSpeed;
    }
}
