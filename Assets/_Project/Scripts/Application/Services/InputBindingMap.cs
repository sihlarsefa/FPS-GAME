using System;
using System.Collections.Generic;
using System.Text;

namespace Project.Application.Services
{
    /// <summary>Yeniden atanabilir oyun eylemleri.</summary>
    public enum BindAction
    {
        MoveForward, MoveBack, MoveLeft, MoveRight, Sprint, Jump, CrouchHold, CrouchToggle, Prone,
        LeanLeft, LeanRight, Reload, Interact, Slot1, Slot2, Slot3, Slot4, FireMode, Heal, Boost,
        Grenade, Smoke, Lower, Inventory, Map, Inspect, NightVision, AttackHeli
    }

    /// <summary>
    /// Eylem -> tuş adı listesi. Saf C# (Unity yok): tuş adları Input System <c>Key</c> enum adlarıdır.
    /// Çakışan tuş başka eylemden alınır; serileştirme "Eylem=Tus,Tus" satırlarıdır.
    /// </summary>
    public sealed class InputBindingMap
    {
        private readonly Dictionary<BindAction, List<string>> _map = new();

        public InputBindingMap() { ResetToDefaults(); }

        public static readonly BindAction[] All = (BindAction[])Enum.GetValues(typeof(BindAction));

        public static IReadOnlyList<string> DefaultKeys(BindAction a)
        {
            switch (a)
            {
                case BindAction.MoveForward: return new[] { "W" };
                case BindAction.MoveBack: return new[] { "S" };
                case BindAction.MoveLeft: return new[] { "A" };
                case BindAction.MoveRight: return new[] { "D" };
                case BindAction.Sprint: return new[] { "LeftShift" };
                case BindAction.Jump: return new[] { "Space" };
                case BindAction.CrouchHold: return new[] { "LeftCtrl" };
                case BindAction.CrouchToggle: return new[] { "C" };
                case BindAction.Prone: return new[] { "Z" };
                case BindAction.LeanLeft: return new[] { "Q" };
                case BindAction.LeanRight: return new[] { "E" };
                case BindAction.Reload: return new[] { "R" };
                case BindAction.Interact: return new[] { "F" };
                case BindAction.Slot1: return new[] { "Digit1" };
                case BindAction.Slot2: return new[] { "Digit2" };
                case BindAction.Slot3: return new[] { "Digit3" };
                case BindAction.Slot4: return new[] { "Digit4" };
                case BindAction.FireMode: return new[] { "B" };
                case BindAction.Heal: return new[] { "H" };
                case BindAction.Boost: return new[] { "J" };
                case BindAction.Grenade: return new[] { "G" };
                case BindAction.Smoke: return new[] { "T" };
                case BindAction.Lower: return new[] { "X" };
                case BindAction.Inventory: return new[] { "Tab", "I" };
                case BindAction.Map: return new[] { "M" };
                case BindAction.Inspect: return new[] { "K" };
                case BindAction.NightVision: return new[] { "O" };
                case BindAction.AttackHeli: return new[] { "L" };
                default: return Array.Empty<string>();
            }
        }

        public static string Label(BindAction a)
        {
            switch (a)
            {
                case BindAction.MoveForward: return "İleri";
                case BindAction.MoveBack: return "Geri";
                case BindAction.MoveLeft: return "Sola";
                case BindAction.MoveRight: return "Sağa";
                case BindAction.Sprint: return "Koş";
                case BindAction.Jump: return "Zıpla";
                case BindAction.CrouchHold: return "Eğil (basılı)";
                case BindAction.CrouchToggle: return "Eğil (aç/kapa)";
                case BindAction.Prone: return "Yüzüstü";
                case BindAction.LeanLeft: return "Sola eğil";
                case BindAction.LeanRight: return "Sağa eğil";
                case BindAction.Reload: return "Şarjör değiştir";
                case BindAction.Interact: return "Etkileşim";
                case BindAction.Slot1: return "Silah 1";
                case BindAction.Slot2: return "Silah 2";
                case BindAction.Slot3: return "Silah 3";
                case BindAction.Slot4: return "Silah 4";
                case BindAction.FireMode: return "Ateş modu";
                case BindAction.Heal: return "İyileş";
                case BindAction.Boost: return "Güçlendirici";
                case BindAction.Grenade: return "El bombası";
                case BindAction.Smoke: return "Sis bombası";
                case BindAction.Lower: return "Silahı indir";
                case BindAction.Inventory: return "Envanter";
                case BindAction.Map: return "Harita";
                case BindAction.Inspect: return "Silahı incele";
                case BindAction.NightVision: return "Gece görüş gözlüğü";
                case BindAction.AttackHeli: return "T-129 ATAK desteği";
                default: return a.ToString();
            }
        }

        public void ResetToDefaults()
        {
            _map.Clear();
            foreach (var a in All) _map[a] = new List<string>(DefaultKeys(a));
        }

        public IReadOnlyList<string> Get(BindAction a) => _map[a];

        public bool Has(BindAction a, string key) => _map[a].Contains(key);

        /// <summary>Eylemi tek tuşa atar; aynı tuş başka eylemlerden kaldırılır.</summary>
        public void Set(BindAction a, string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            foreach (var kv in _map) kv.Value.Remove(key);
            _map[a].Clear();
            _map[a].Add(key);
        }

        /// <summary>Tuşu kullanan eylem (yoksa null).</summary>
        public BindAction? FindOwner(string key)
        {
            foreach (var kv in _map) if (kv.Value.Contains(key)) return kv.Key;
            return null;
        }

        public string Display(BindAction a) => _map[a].Count == 0 ? "—" : string.Join(" / ", _map[a]);

        public string Serialize()
        {
            var sb = new StringBuilder();
            foreach (var a in All) sb.Append(a).Append('=').Append(string.Join(",", _map[a])).Append('\n');
            return sb.ToString();
        }

        /// <summary>Bozuk/eksik satırlar yok sayılır; eksik eylemler varsayılanda kalır.</summary>
        public static InputBindingMap Deserialize(string text)
        {
            var map = new InputBindingMap();
            if (string.IsNullOrEmpty(text)) return map;
            foreach (var line in text.Split('\n'))
            {
                var i = line.IndexOf('=');
                if (i <= 0) continue;
                if (!Enum.TryParse(line.Substring(0, i).Trim(), out BindAction a) || !Enum.IsDefined(typeof(BindAction), a)) continue;
                var keys = new List<string>();
                foreach (var k in line.Substring(i + 1).Split(','))
                {
                    var t = k.Trim();
                    if (t.Length > 0 && !keys.Contains(t)) keys.Add(t);
                }
                if (keys.Count == 0) continue;
                map._map[a] = keys;
            }
            return map;
        }
    }
}
