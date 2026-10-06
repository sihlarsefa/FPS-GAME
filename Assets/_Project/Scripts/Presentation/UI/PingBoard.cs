using System.Collections.Generic;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Tim işaretleri (ping) panosu: her tim için tek, süreli bir dünya işareti. Düşman ping'i kırmızı "Düşman!",
    /// konum ping'i amber. Dünya HUD'u (<see cref="PingWorldView"/>) ve mini harita okur; botlar BotDirector üzerinden haberdar edilir.
    /// Zaman dışarıdan verilir (Time.time) — saf mantık, test edilebilir.
    /// </summary>
    public static class PingBoard
    {
        public const float Duration = 8f;
        public const string EnemyLabel = "Düşman!";
        public const string LocationLabel = "Nokta";

        public struct Ping
        {
            public int Team;
            public Vector3 Position;
            public bool IsEnemy;
            public float PlacedTime;
            public float ExpireTime;

            public string Label => IsEnemy ? EnemyLabel : LocationLabel;

            /// <summary>Kalan ömür oranı (1 → 0).</summary>
            public float Remaining01(float now) => Mathf.Clamp01((ExpireTime - now) / Mathf.Max(0.01f, ExpireTime - PlacedTime));
        }

        private static readonly List<Ping> Items = new List<Ping>(4);

        /// <summary>Değişimde artar.</summary>
        public static int Version { get; private set; }

        public static IReadOnlyList<Ping> All => Items;

        /// <summary>Timin ping'ini koyar (öncekinin yerine).</summary>
        public static void Place(int team, Vector3 position, bool isEnemy, float now)
        {
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z))
                return;

            var ping = new Ping { Team = team, Position = position, IsEnemy = isEnemy, PlacedTime = now, ExpireTime = now + Duration };
            for (var i = 0; i < Items.Count; i++)
            {
                if (Items[i].Team == team)
                {
                    Items[i] = ping;
                    Version++;
                    return;
                }
            }

            Items.Add(ping);
            Version++;
        }

        /// <summary>Timin etkin ping'i (süresi dolmadıysa).</summary>
        public static bool TryGet(int team, float now, out Ping ping)
        {
            for (var i = 0; i < Items.Count; i++)
            {
                if (Items[i].Team == team && now < Items[i].ExpireTime)
                {
                    ping = Items[i];
                    return true;
                }
            }

            ping = default;
            return false;
        }

        /// <summary>Süresi dolanları atar.</summary>
        public static void Prune(float now)
        {
            for (var i = Items.Count - 1; i >= 0; i--)
            {
                if (now >= Items[i].ExpireTime)
                {
                    Items.RemoveAt(i);
                    Version++;
                }
            }
        }

        public static void Reset()
        {
            Items.Clear();
            Version++;
        }
    }
}
