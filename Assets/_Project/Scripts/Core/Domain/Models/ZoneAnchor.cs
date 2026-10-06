namespace Project.Core.Domain
{
    /// <summary>
    /// Arazi çıpası: final çemberlerinin tercih ettiği yer (tepe, kale, köy, nehir geçidi). Ağırlık göreli önemdir (&gt; 0).
    /// Harita yerleşimi (MapLayout*) bunları üretir ve ZoneService.SetAnchors'a verir.
    /// </summary>
    public readonly struct ZoneAnchor
    {
        public float X { get; }
        public float Z { get; }
        public float Weight { get; }

        public ZoneAnchor(float x, float z, float weight = 1f)
        {
            X = x;
            Z = z;
            Weight = weight;
        }
    }
}
