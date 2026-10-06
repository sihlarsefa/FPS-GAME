using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>Takım listesi satır durumu.</summary>
    public enum SquadRowState
    {
        Alive = 0,
        Downed = 1,
        Dead = 2,
        Disconnected = 3
    }

    /// <summary>
    /// Takım listesi saf sıralama/yerleşim kuralları (EditMode testli): komutan en üstte, sonra canlı → yaralı → ölü,
    /// kendi içinde slot sırası (sıra kaymaz, oyuncu satırı yer değiştirmez). Satır opaklığı ve sağlık rengi buradan.
    /// </summary>
    public static class SquadListLayout
    {
        public const float RowHeight = 26f;
        public const float RowGap = 3f;
        public const int MaxRows = 10;

        public struct Row
        {
            public int Slot;
            public SquadRowState State;
            public bool IsCommander;
            public bool IsLocal;
            public float Health01;
            public float Distance;
        }

        public static int Rank(Row r)
        {
            if (r.IsLocal) return -2;
            if (r.IsCommander) return -1;
            return (int)r.State;
        }

        /// <summary>Kararlı sıralama: yerel oyuncu, komutan, sonra durum, sonra slot.</summary>
        public static void Sort(List<Row> rows)
        {
            if (rows == null)
                return;
            rows.Sort((a, b) =>
            {
                var c = Rank(a).CompareTo(Rank(b));
                return c != 0 ? c : a.Slot.CompareTo(b.Slot);
            });
            if (rows.Count > MaxRows)
                rows.RemoveRange(MaxRows, rows.Count - MaxRows);
        }

        public static float RowY(int index) => -index * (RowHeight + RowGap);

        public static float PanelHeight(int rowCount) =>
            rowCount <= 0 ? 0f : rowCount * RowHeight + (rowCount - 1) * RowGap;

        /// <summary>Ölü/bağlantısı kopan satır soluk; yaralı nabızlı; canlı tam opak.</summary>
        public static float RowAlpha(SquadRowState state, float time)
        {
            switch (state)
            {
                case SquadRowState.Dead: return 0.38f;
                case SquadRowState.Disconnected: return 0.3f;
                case SquadRowState.Downed: return 0.7f + 0.3f * (0.5f + 0.5f * Mathf.Sin(time * 2f * Mathf.PI * 1.5f));
                default: return 1f;
            }
        }

        public static Color HealthColor(Row r)
        {
            if (r.State == SquadRowState.Downed) return HudRules.SignatureRed;
            if (r.State != SquadRowState.Alive) return new Color(0.5f, 0.5f, 0.5f, 1f);
            return VitalBarModel.HealthColor(r.Health01);
        }

        /// <summary>Mesafe etiketi: 1000 m altında "NNm" (5'e yuvarlı), üstünde "N.Nkm"; 0 veya geçersizse boş.</summary>
        public static string DistanceLabel(float meters)
        {
            if (float.IsNaN(meters) || meters < 1f)
                return string.Empty;
            if (meters < 1000f)
                return (Mathf.RoundToInt(meters / 5f) * 5).ToString() + "m";
            return (meters / 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "km";
        }
    }
}
