namespace Project.Core.Domain
{
    /// <summary>Arayüz tuşları (oyun simülasyonuna gitmez, sadece yerel).</summary>
    public readonly struct UiInputState
    {
        public bool Pause { get; }
        public bool ToggleInventory { get; }
        public bool ToggleMap { get; }
        public bool HoldScoreboard { get; }

        public UiInputState(bool pause, bool toggleInventory, bool toggleMap, bool holdScoreboard)
        {
            Pause = pause;
            ToggleInventory = toggleInventory;
            ToggleMap = toggleMap;
            HoldScoreboard = holdScoreboard;
        }

        public static UiInputState Zero => new(false, false, false, false);
    }
}
