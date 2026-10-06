using System;
using System.Collections.Generic;

namespace Project.Application.Services
{
    /// <summary>Bir girdinin geçerli olduğu bağlam(lar). Aynı tuş yalnızca ayrık bağlamlarda iki işleve atanabilir.</summary>
    [Flags]
    public enum ControlContext
    {
        None = 0,
        /// <summary>Yaya, oynanış girdisi açık.</summary>
        OnFoot = 1,
        /// <summary>Araç sürücü koltuğu.</summary>
        Driver = 2,
        /// <summary>Kirpi taret (nişancı) koltuğu.</summary>
        Gunner = 4,
        /// <summary>Helikopter / araç yolcusu (intikal).</summary>
        Passenger = 8,
        /// <summary>Ölü oyuncu izleme kamerası.</summary>
        Spectator = 16,
        /// <summary>Tam harita / envanter açık (oyun girdisi kapalı).</summary>
        Overlay = 32,
        /// <summary>Geliştirici konsolu açık (metin girişi).</summary>
        Console = 64,
        /// <summary>Komut çarkı açık.</summary>
        Wheel = 128,
        /// <summary>Eğitim görevi çalışıyor / özet kartı açık.</summary>
        Tutorial = 256,
        /// <summary>Duraklatma menüsü / ayarlar.</summary>
        Menu = 512,

        /// <summary>Canlı oyuncunun oynanış bağlamları.</summary>
        Alive = OnFoot | Driver | Gunner | Passenger,
        /// <summary>Maç içi tüm bağlamlar (izleyici dahil).</summary>
        InGame = Alive | Spectator,
        Always = InGame | Overlay | Console | Wheel | Tutorial | Menu
    }

    /// <summary>Kontrol tablosu satırı.</summary>
    public readonly struct ControlEntry
    {
        public readonly ControlContext Context;
        /// <summary>Tuş adı (Input System Key enum adı) ya da "Mouse.Left" gibi fare girdisi.</summary>
        public readonly string Input;
        public readonly string Label;
        /// <summary>Ayarlardan yeniden atanabilir mi?</summary>
        public readonly bool Rebindable;

        public ControlEntry(ControlContext context, string input, string label, bool rebindable)
        {
            Context = context;
            Input = input;
            Label = label;
            Rebindable = rebindable;
        }
    }

    /// <summary>
    /// Tüm girdi bağlamlarının tek yerden tablosu (saf C#): her tuşun hangi bağlamda hangi işleve ait olduğunu söyler.
    /// Yeni sabit tuş eklerken buraya da yazılır; test aynı tuşun örtüşen bağlamlarda iki işleve atanmasını yakalar.
    /// <see cref="Reserved"/> tuşlar ayarlardan atanamaz (sabit sistem tuşları).
    /// </summary>
    public static class ControlScheme
    {
        /// <summary>Ayarlardan yeniden atanamayan sistem/komut tuşları (Key enum adları).</summary>
        public static readonly string[] Reserved =
        {
            "Escape", "F1", "F2", "F3", "F4", "F9", "F10", "Backquote", "CapsLock", "Y", "U", "V", "N", "Enter"
        };

        public static bool IsReserved(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            for (var i = 0; i < Reserved.Length; i++)
                if (string.Equals(Reserved[i], key, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Yeniden atanabilir eylemlerin bağlamı.</summary>
        public static ControlContext ContextOf(BindAction a)
        {
            switch (a)
            {
                case BindAction.MoveForward:
                case BindAction.MoveBack:
                case BindAction.MoveLeft:
                case BindAction.MoveRight:
                    return ControlContext.OnFoot | ControlContext.Driver; // sürücü: gaz/direksiyon
                case BindAction.Jump:
                    return ControlContext.OnFoot | ControlContext.Driver; // sürücü: el freni
                case BindAction.Reload:
                    return ControlContext.OnFoot | ControlContext.Gunner; // taret mermi yükleme
                case BindAction.Interact:
                    return ControlContext.OnFoot | ControlContext.Driver | ControlContext.Gunner | ControlContext.Passenger;
                case BindAction.Slot1:
                case BindAction.Slot2:
                    return ControlContext.OnFoot | ControlContext.Driver | ControlContext.Gunner; // araçta koltuk değiştirir
                case BindAction.Inventory:
                    return ControlContext.Alive | ControlContext.Overlay;
                case BindAction.Map:
                    return ControlContext.InGame | ControlContext.Overlay;
                case BindAction.AttackHeli:
                    return ControlContext.Alive | ControlContext.Overlay; // tim komutu (V/U gibi; harita açıkken işaret hedef)
                default:
                    return ControlContext.OnFoot;
            }
        }

        /// <summary>Sabit (atanamaz) girdiler. Çarkın Y'si ve fare orta tuşu iki ayrı satırdır.</summary>
        public static IReadOnlyList<ControlEntry> FixedEntries()
        {
            const ControlContext tim = ControlContext.Alive | ControlContext.Overlay;
            return new[]
            {
                new ControlEntry(ControlContext.OnFoot | ControlContext.Gunner, "Mouse.Left", "Ateş", false),
                new ControlEntry(ControlContext.OnFoot, "Mouse.Right", "Nişan", false),
                new ControlEntry(ControlContext.OnFoot, "Mouse.Scroll", "Silah değiştir", false),
                new ControlEntry(ControlContext.Alive & ~ControlContext.Passenger, "Mouse.Middle", "Ping (dokun) / komut çarkı (basılı)", false),
                new ControlEntry(ControlContext.Alive & ~ControlContext.Passenger, "Y", "Komut çarkı (basılı)", false),
                new ControlEntry(ControlContext.Wheel, "Escape", "Çarkı kapat", false),
                new ControlEntry(ControlContext.Alive | ControlContext.Spectator | ControlContext.Overlay | ControlContext.Menu, "Escape", "Kapat / duraklat (en üstteki katman)", false),
                new ControlEntry(tim, "F1", "Emir: Takip", false),
                new ControlEntry(tim, "F2", "Emir: Mevzi", false),
                new ControlEntry(tim, "F3", "Emir: Taarruz", false),
                new ControlEntry(tim, "F4", "Emir: Toplan", false),
                new ControlEntry(tim, "V", "Topçu atışı", false),
                new ControlEntry(tim, "U", "İHA keşfi", false),
                new ControlEntry(ControlContext.InGame, "CapsLock", "Skor tablosu (basılı)", false),
                new ControlEntry(ControlContext.Always, "Backquote", "Geliştirici konsolu", false),
                new ControlEntry(ControlContext.Always, "F10", "Performans göstergesi", false),
                new ControlEntry(ControlContext.Spectator, "E", "İzleyici: sonraki hedef", false),
                new ControlEntry(ControlContext.Spectator, "Q", "İzleyici: önceki hedef", false),
                new ControlEntry(ControlContext.Spectator, "Mouse.Scroll", "İzleyici: yakınlaştır", false),
                new ControlEntry(ControlContext.Spectator, "Mouse.Right", "İzleyici: kamerayı döndür (basılı)", false),
                new ControlEntry(ControlContext.Overlay, "Space", "Harita: oyuncuya odakla", false),
                new ControlEntry(ControlContext.Overlay, "Equals", "Harita: yakınlaştır", false),
                new ControlEntry(ControlContext.Overlay, "Minus", "Harita: uzaklaştır", false),
                new ControlEntry(ControlContext.Overlay, "Mouse.Right", "Harita: işaret koy", false),
                new ControlEntry(ControlContext.Tutorial, "N", "Eğitim: adımı atla", false),
                new ControlEntry(ControlContext.Tutorial, "F9", "Eğitim: tümünü atla", false),
                new ControlEntry(ControlContext.Tutorial, "Enter", "Eğitim özetini kapat", false),
                new ControlEntry(ControlContext.Console, "Enter", "Konsol: komutu çalıştır", false),
                new ControlEntry(ControlContext.Console, "Tab", "Konsol: tamamla", false),
                new ControlEntry(ControlContext.Console, "UpArrow", "Konsol: önceki komut", false),
                new ControlEntry(ControlContext.Console, "DownArrow", "Konsol: sonraki komut", false),
                new ControlEntry(ControlContext.Console, "Escape", "Konsolu kapat", false),
            };
        }

        /// <summary>Yeniden atanabilir eylemlerin (verilen haritadaki) satırları + sabit satırlar.</summary>
        public static List<ControlEntry> BuildEntries(InputBindingMap map)
        {
            var list = new List<ControlEntry>();
            foreach (var a in InputBindingMap.All)
            {
                var keys = map != null ? map.Get(a) : InputBindingMap.DefaultKeys(a);
                for (var i = 0; i < keys.Count; i++)
                    list.Add(new ControlEntry(ContextOf(a), keys[i], InputBindingMap.Label(a), true));
            }

            list.AddRange(FixedEntries());
            return list;
        }

        /// <summary>
        /// Aynı girdiyi örtüşen bağlamlarda farklı işlevlere atayan satır çiftleri. Aynı etiketli satırlar (aynı işlevin
        /// takma adı) çakışma sayılmaz.
        /// </summary>
        public static List<(ControlEntry a, ControlEntry b)> FindConflicts(IReadOnlyList<ControlEntry> entries)
        {
            var result = new List<(ControlEntry, ControlEntry)>();
            for (var i = 0; i < entries.Count; i++)
            for (var j = i + 1; j < entries.Count; j++)
            {
                var x = entries[i];
                var y = entries[j];
                if (!string.Equals(x.Input, y.Input, StringComparison.Ordinal)) continue;
                if ((x.Context & y.Context) == ControlContext.None) continue;
                if (string.Equals(x.Label, y.Label, StringComparison.Ordinal)) continue;
                result.Add((x, y));
            }

            return result;
        }
    }
}
