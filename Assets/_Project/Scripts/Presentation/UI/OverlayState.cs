using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Geçici katmanların (geliştirici konsolu, komut çarkı, kozmetik paneli) ortak durumu. Duraklatma/harita/envanter
    /// tuşlarının bu katmanlar açıkken ya da onları kapattığı karede tetiklenmemesi için tek kaynak:
    /// Esc her zaman en üstteki katmana gider (konsol/çark/kozmetik → harita/envanter → duraklatma). Çalışma sırası
    /// bilinmediğinden hem "açık" bayrağı hem de "bu karede tüketildi" damgası kontrol edilir.
    /// </summary>
    public static class OverlayState
    {
        private static int _escapeFrame = -1;

        /// <summary>Geliştirici konsolu açık (metin girişi: oyun/arayüz kısayolları çalışmaz).</summary>
        public static bool ConsoleOpen { get; set; }

        /// <summary>Komut çarkı açık (zaman yavaşlar, oyun girdisi kapalı).</summary>
        public static bool CommandWheelOpen { get; set; }

        /// <summary>Ana menüdeki kozmetik paneli açık.</summary>
        public static bool CosmeticsOpen { get; set; }

        /// <summary>Metin girişi ya da tuş yakalama sürüyor: harf/rakam kısayolları yok sayılmalı.</summary>
        public static bool TextInputActive => ConsoleOpen || KeyBindingsPanel.IsCapturing;

        /// <summary>Bu karede Esc bir katmanı kapatmak için kullanıldı.</summary>
        public static void ConsumeEscape() => _escapeFrame = Time.frameCount;

        /// <summary>Esc duraklatma/ana menü geri tuşuna gitmemeli (geçici katman açık ya da bu karede kapattı).</summary>
        public static bool EscapeOwnedByTransient =>
            ConsoleOpen || CommandWheelOpen || CosmeticsOpen || _escapeFrame == Time.frameCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _escapeFrame = -1;
            ConsoleOpen = false;
            CommandWheelOpen = false;
            CosmeticsOpen = false;
        }
    }
}
