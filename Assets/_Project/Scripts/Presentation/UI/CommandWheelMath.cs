using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>Komut çarkı seçimleri (saat yönünde, ilki tepede).</summary>
    public enum CommandWheelItem
    {
        Follow = 0,
        HoldPosition = 1,
        Attack = 2,
        Regroup = 3,
        Artillery = 4,
        Smoke = 5
    }

    /// <summary>Komut çarkı saf geometrisi: imleç sapmasından dilim seçimi.</summary>
    public static class CommandWheelMath
    {
        public const int ItemCount = 6;

        public static string Label(CommandWheelItem item)
        {
            switch (item)
            {
                case CommandWheelItem.Follow: return "Takip";
                case CommandWheelItem.HoldPosition: return "Mevzi al";
                case CommandWheelItem.Attack: return "Taarruz";
                case CommandWheelItem.Regroup: return "Toplan";
                case CommandWheelItem.Artillery: return "Topçu iste";
                default: return "Sis at";
            }
        }

        /// <summary>Sapma (x sağ, y yukarı) → dilim indeksi; ölü bölgede -1. Dilim 0 tepede, saat yönünde artar.</summary>
        public static int SliceAt(Vector2 offset, int count, float deadZone)
        {
            if (count <= 0 || offset.magnitude < deadZone)
                return -1;

            var angle = Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg;
            if (angle < 0f)
                angle += 360f;

            var width = 360f / count;
            var index = Mathf.FloorToInt(((angle + width * 0.5f) % 360f) / width);
            return Mathf.Clamp(index, 0, count - 1);
        }

        /// <summary>Dilim merkezinin yön vektörü (birim; x sağ, y yukarı).</summary>
        public static Vector2 SliceDirection(int index, int count)
        {
            var rad = index * (360f / Mathf.Max(1, count)) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
        }
    }
}
