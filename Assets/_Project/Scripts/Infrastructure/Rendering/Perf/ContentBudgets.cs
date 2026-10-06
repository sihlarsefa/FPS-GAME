using UnityEngine;

namespace Project.Infrastructure.Rendering.Perf
{
    /// <summary>Içerik sistemlerinin kademe (0=Low..3=Ultra) basina ust sinirlari; tek yerde toplanir. Saf veri.</summary>
    public enum ContentSystem { PoiLights, Interior, Roadside, Frontline, TreeVariety, Lobby }

    public static class ContentBudgets
    {
        private static int Pick(int tier, int a, int b, int c, int d)
        {
            switch (Mathf.Clamp(tier, 0, 3)) { case 0: return a; case 1: return b; case 2: return c; default: return d; }
        }

        // --- Isik (sahne basina, tum sistemler toplami degil; her sistemin kendi sinirlari) ---
        public static int PoiLights(int tier) => Pick(tier, 6, 12, 20, 32);
        public static int PoiShadowedLights(int tier) => Pick(tier, 0, 2, 4, 4);
        public static int LobbyLights(int tier) => Pick(tier, 2, 3, 4, 4);
        public static int LobbyShadowedLights(int tier) => Pick(tier, 0, 0, 0, 4);
        /// <summary>Bina basina isik huzmesi (renderer).</summary>
        public static int InteriorShafts(int tier) => Pick(tier, 0, 2, 4, 8);

        // --- Reflection probe ---
        public static int Probes(int tier) => Pick(tier, 2, 6, 12, 20);

        // --- Renderer ---
        /// <summary>Oda basina mobilya + yipranma toplam renderer ust siniri.</summary>
        public static int InteriorRoomRenderers(int tier) => Pick(tier, 28, 50, 72, 96); // olcum: en kotu oda (50 m2, yikik, 3 pencere) 21/42/62/83; cam kirigi baskin
        /// <summary>Harita basina yol kenari prop ust siniri (RoadsidePlan.TotalFor(kademe) toplami: ~%40/65/85/100).</summary>
        public static int RoadsideProps(int tier) => Pick(tier, 100, 160, 205, 240);
        /// <summary>Cephe hatti (300 m) toplam oge ust siniri.</summary>
        public static int FrontlineItems(int tier) => Pick(tier, 120, 200, 300, 400);
        /// <summary>Cesitlilik agaci/cali hedef sayisi ust siniri.</summary>
        public static int TreeVarietyInstances(int tier) => Pick(tier, 120, 280, 450, 650);
        public static int TreeInstances(int tier) => Pick(tier, 1400, 2400, 3200, 4000);
        public static int LobbyRenderers(int tier) => Pick(tier, 2, 3, 4, 4) * 2 + 4;

        // --- Parcacik ---
        public static int LobbyParticles(int tier) => Pick(tier, 22, 44, 76, 120);
        public static int LobbyFogParticles(int tier) => Pick(tier, 12, 24, 40, 64);
        public static int LobbyEmberParticles(int tier) => Pick(tier, 10, 20, 36, 56);
    }
}
