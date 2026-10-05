using UnityEngine;

namespace Project.Infrastructure
{
    /// <summary>Fizik katmanları ve maskeler. İsimler editör kurulumunda TagManager'a yazılır (numaralar adsız da çalışır).</summary>
    public static class GameLayers
    {
        public const int Default = 0;
        public const int TransparentFX = 1;
        public const int IgnoreRaycast = 2;
        public const int Water = 4;
        public const int UI = 5;
        public const int Viewmodel = 8;
        public const int Player = 9;
        public const int Bot = 10;
        public const int Hitbox = 11;
        public const int Loot = 12;
        public const int Projectile = 13;
        public const int Vehicle = 14;

        public static readonly string[] CustomLayerNames =
        {
            null, null, null, null, null, null, null, null,
            "Viewmodel", "Player", "Bot", "Hitbox", "Loot", "Projectile", "Vehicle"
        };

        public static int WorldMask => 1 << Default;
        public static int BulletMask => (1 << Default) | (1 << Hitbox) | (1 << Vehicle);
        public static int LineOfSightMask => (1 << Default) | (1 << Vehicle);
        public static int GroundMask => (1 << Default) | (1 << Vehicle);
        public static int InteractMask => (1 << Loot) | (1 << Vehicle);
        public static int MovementBlockMask => (1 << Default) | (1 << Player) | (1 << Bot) | (1 << Vehicle);

        /// <summary>Gereksiz çarpışmaları kapatır (vuruş kutuları sadece ışın testleri içindir).</summary>
        public static void ConfigureCollisionMatrix()
        {
            for (var layer = 0; layer < 32; layer++)
            {
                Physics.IgnoreLayerCollision(Hitbox, layer, true);
                Physics.IgnoreLayerCollision(Viewmodel, layer, true);
            }

            Physics.IgnoreLayerCollision(Loot, Player, true);
            Physics.IgnoreLayerCollision(Loot, Bot, true);
            Physics.IgnoreLayerCollision(Loot, Loot, true);
            Physics.IgnoreLayerCollision(Loot, Projectile, true);
            Physics.IgnoreLayerCollision(Projectile, Projectile, true);
        }

        public static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
                return;

            root.layer = layer;
            var t = root.transform;
            for (var i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }
    }
}
