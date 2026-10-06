using System;
using System.Collections.Generic;
using Project.Application.Services;
using Project.Core.Interfaces;

namespace Project.Presentation.Tutorial
{
    /// <summary>İlk karşılaşmada gösterilen bağlamsal ipucu kimlikleri.</summary>
    public enum TutorialTipId { FirstLoot, FirstAds, FirstZoneWarning, FirstDownedTeammate, FirstVehicle }

    /// <summary>Bir ipucunun metni; Action alanı giriş ipucu (klavye/gamepad) için kullanılır.</summary>
    public readonly struct TutorialTipDef
    {
        public readonly TutorialTipId Id;
        public readonly string Title;
        public readonly string Body;
        public readonly string Action;
        public TutorialTipDef(TutorialTipId id, string title, string body, string action)
        { Id = id; Title = title; Body = body; Action = action; }
    }

    /// <summary>Saf mantık: ipucu kataloğu + "bir daha gösterme" kalıcılığı (ISettingsStore).</summary>
    public sealed class TutorialTipTracker
    {
        public const string KeyPrefix = "tut.tip.";
        public const int MaxShows = 3;

        private static readonly TutorialTipDef[] Catalog =
        {
            new TutorialTipDef(TutorialTipId.FirstLoot, "YAĞMA", "Yerdeki eşyayı almak için yaklaş ve al tuşuna bas. Zırh ve cephane önceliklidir.", "Interact"),
            new TutorialTipDef(TutorialTipId.FirstAds, "NİŞAN ALMA", "Nişan alırken isabet artar ama hareket yavaşlar. Dürbünlü silahlarda tekerle yakınlaştır.", "Aim"),
            new TutorialTipDef(TutorialTipId.FirstZoneWarning, "BÖLGE DARALIYOR", "Güvenli bölge küçülüyor. Dışarıda kalırsan can kaybedersin; haritayı aç ve yön bul.", "Map"),
            new TutorialTipDef(TutorialTipId.FirstDownedTeammate, "YARALI ARKADAŞ", "Tim arkadaşın yere düştü. Yanına git ve kaldırma tuşunu basılı tut; ateş altında önce siper al.", "Interact"),
            new TutorialTipDef(TutorialTipId.FirstVehicle, "ARAÇ", "Araca binmek için yaklaşıp bin tuşuna bas. Motor sesi düşmanı çeker, dikkat.", "Interact"),
        };

        private readonly ISettingsStore _store;
        private readonly HashSet<TutorialTipId> _shownThisSession = new HashSet<TutorialTipId>();

        public TutorialTipTracker(ISettingsStore store) { _store = store; }

        public static IReadOnlyList<TutorialTipDef> All => Catalog;

        public static TutorialTipDef Get(TutorialTipId id)
        {
            for (var i = 0; i < Catalog.Length; i++)
                if (Catalog[i].Id == id) return Catalog[i];
            return default;
        }

        private static string CountKey(TutorialTipId id) => KeyPrefix + "n." + id;
        private static string OffKey(TutorialTipId id) => KeyPrefix + "off." + id;

        /// <summary>Bu oturumda (maçta) gösterilen ipucu sayısı.</summary>
        public int ShownThisSession => _shownThisSession.Count;

        /// <summary>Maç sayacına göre bir maçta en çok kaç ipucu: 1. maç 5, 2. maç 3, 3. maç 2, sonra 1.</summary>
        public static int MaxTipsPerMatch(int matchIndex)
            => matchIndex <= 1 ? 5 : matchIndex == 2 ? 3 : matchIndex == 3 ? 2 : 1;

        /// <summary>Kalıcı maç sayacı (TutorialProgress).</summary>
        public int MatchIndex { get; set; }

        public int ShowCount(TutorialTipId id) => _store != null ? _store.GetInt(CountKey(id), 0) : 0;
        public bool IsMuted(TutorialTipId id) => _store != null && _store.GetInt(OffKey(id), 0) != 0;

        /// <summary>İpucu şimdi gösterilmeli mi? Kapatılmamış, oturumda gösterilmemiş ve sınırı aşmamışsa.</summary>
        public bool ShouldShow(TutorialTipId id)
            => !IsMuted(id) && !_shownThisSession.Contains(id) && ShowCount(id) < MaxShows
               && (MatchIndex <= 0 || _shownThisSession.Count < MaxTipsPerMatch(MatchIndex));

        /// <summary>Gösterildi olarak işaretler (sayaç + oturum).</summary>
        public void MarkShown(TutorialTipId id)
        {
            _shownThisSession.Add(id);
            if (_store == null) return;
            _store.SetInt(CountKey(id), ShowCount(id) + 1);
            _store.Save();
        }

        /// <summary>"Bir daha gösterme": kalıcı olarak kapatır.</summary>
        public void MuteForever(TutorialTipId id)
        {
            _shownThisSession.Add(id);
            if (_store == null) return;
            _store.SetInt(OffKey(id), 1);
            _store.Save();
        }

        /// <summary>Tüm ipuçlarını tekrar açar (ayarlar/sıfırlama için).</summary>
        public void ResetAll()
        {
            _shownThisSession.Clear();
            if (_store == null) return;
            foreach (var d in Catalog)
            {
                _store.SetInt(CountKey(d.Id), 0);
                _store.SetInt(OffKey(d.Id), 0);
            }
            _store.Save();
        }
    }

    /// <summary>Eylem → tuş/düğme etiketi; gamepad aktifken gamepad etiketleri.</summary>
    public static class TutorialInputHints
    {
        private static readonly Dictionary<string, (string kb, string pad)> Map = new Dictionary<string, (string, string)>
        {
            { "Move", ("W A S D", "Sol Çubuk") },
            { "Look", ("Fare", "Sağ Çubuk") },
            { "Fire", ("Sol Tık", "RT") },
            { "Aim", ("Sağ Tık", "LT") },
            { "Reload", ("R", "X") },
            { "Grenade", ("G", "RB") },
            { "Heal", ("H", "Y Yön Tuşu") },
            { "Interact", ("F", "A") },
            { "Map", ("M", "Geri/Görünüm") },
            { "Inventory", ("TAB", "Menü") },
        };

        // Klavye etiketi sabit yazı yerine aktif tuş atamasından (InputBindings) çözülür.
        private static readonly Dictionary<string, BindAction> Bound = new Dictionary<string, BindAction>
        {
            { "Reload", BindAction.Reload }, { "Grenade", BindAction.Grenade }, { "Heal", BindAction.Heal },
            { "Interact", BindAction.Interact }, { "Map", BindAction.Map }, { "Inventory", BindAction.Inventory },
        };

        /// <summary>Testler için: eylem adından klavye etiketi çözücüsünü değiştirir (null: InputBindings).</summary>
        public static Func<BindAction, string> KeyResolver { get; set; }

        private static string KeyboardLabel(string action, string fallback)
        {
            if (action == "Move")
            {
                var f = Resolve(BindAction.MoveForward); var l = Resolve(BindAction.MoveLeft);
                var b = Resolve(BindAction.MoveBack); var r = Resolve(BindAction.MoveRight);
                return f == null || l == null || b == null || r == null ? fallback : f + " " + l + " " + b + " " + r;
            }
            return Bound.TryGetValue(action, out var ba) ? Resolve(ba) ?? fallback : fallback;
        }

        private static string Resolve(BindAction a)
        {
            try
            {
                var s = KeyResolver != null ? KeyResolver(a) : Project.Infrastructure.Input.InputBindings.KeyLabel(a);
                return string.IsNullOrEmpty(s) || s == "-" ? null : s;
            }
            catch (Exception) { return null; }
        }

        /// <summary>Gamepad son kullanılan girdi mi (Update ile güncellenir).</summary>
        public static bool GamepadActive { get; set; }

        public static string Label(string action, bool gamepad)
        {
            if (action == null || !Map.TryGetValue(action, out var v)) return string.Empty;
            return gamepad ? v.pad : KeyboardLabel(action, v.kb);
        }

        public static string Label(string action) => Label(action, GamepadActive);

        /// <summary>"[F] Etkileşim" gibi biçimli metin.</summary>
        public static string Format(string body, string action, bool gamepad)
        {
            var l = Label(action, gamepad);
            return string.IsNullOrEmpty(l) ? body : body + "\n[" + l + "]";
        }
    }

    /// <summary>Poligon temel eğitim kontrol listesi: hareket → atış → şarjör → bomba → iyileşme.</summary>
    public sealed class TutorialChecklist
    {
        public readonly struct Item
        {
            public readonly string Id, Label, Action, StepId;
            public Item(string id, string label, string action, string stepId) { Id = id; Label = label; Action = action; StepId = stepId; }
        }

        public static readonly Item[] Items =
        {
            new Item("move", "Hareket et", "Move", "poly_01_move"),
            new Item("shoot", "Ateş et", "Fire", "poly_05_fire"),
            new Item("reload", "Şarjör değiştir", "Reload", "poly_08_reload"),
            new Item("grenade", "El bombası at", "Grenade", "poly_10_frag"),
            new Item("heal", "İyileş", "Heal", "poly_12_heal"),
        };

        private readonly HashSet<string> _done = new HashSet<string>();
        public event Action<string> ItemDone;

        public bool IsDone(string id) => _done.Contains(id);
        public int DoneCount => _done.Count;
        public bool AllDone => _done.Count >= Items.Length;

        public bool MarkByStep(string stepId)
        {
            for (var i = 0; i < Items.Length; i++)
                if (Items[i].StepId == stepId) return Mark(Items[i].Id);
            return false;
        }

        public bool Mark(string id)
        {
            var known = false;
            for (var i = 0; i < Items.Length; i++) if (Items[i].Id == id) known = true;
            if (!known || !_done.Add(id)) return false;
            ItemDone?.Invoke(id);
            return true;
        }

        /// <summary>Sıradaki yapılmamış madde (yoksa null).</summary>
        public Item? Next()
        {
            for (var i = 0; i < Items.Length; i++)
                if (!_done.Contains(Items[i].Id)) return Items[i];
            return null;
        }

        public string Render(bool gamepad)
        {
            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < Items.Length; i++)
            {
                var it = Items[i];
                sb.Append(_done.Contains(it.Id) ? "[x] " : "[ ] ").Append(it.Label);
                if (!_done.Contains(it.Id)) sb.Append("  (").Append(TutorialInputHints.Label(it.Action, gamepad)).Append(')');
                if (i < Items.Length - 1) sb.Append('\n');
            }
            return sb.ToString();
        }
    }

    /// <summary>Türkçe seslendirme kancası; diyalog sistemi (ENTEGRASYON) bu olaya abone olur.</summary>
    public static class TutorialVoiceHooks
    {
        public static event Action<string> Cue;
        public static void Raise(string cueId) { if (!string.IsNullOrEmpty(cueId)) Cue?.Invoke(cueId); }
        public static string StepCue(string stepId) => "tutorial." + stepId;
    }

    /// <summary>Eğitim ilerlemesi (ISettingsStore): "eğitim tamamlandı" bayrağı ve maç sayacı. TİM sayfası bayrağı okur.</summary>
    public static class TutorialProgress
    {
        public const string DoneKey = "tut.done";
        public const string MatchesKey = "tut.matches";
        public const string AchievementMetric = "tutorial_complete";

        public static bool IsCompleted(ISettingsStore store) => store != null && store.GetInt(DoneKey, 0) != 0;
        public static int MatchesStarted(ISettingsStore store) => store != null ? store.GetInt(MatchesKey, 0) : 0;

        /// <summary>Yeni maç başladı: sayacı artırır ve yeni değeri döndürür.</summary>
        public static int RegisterMatch(ISettingsStore store)
        {
            if (store == null) return 0;
            var n = store.GetInt(MatchesKey, 0) + 1;
            store.SetInt(MatchesKey, n);
            store.Save();
            return n;
        }

        /// <summary>Eğitim bitti: bayrağı yazar. İlk kez ise true (ödül/başarım bir kez ilerlesin).</summary>
        public static bool MarkCompleted(ISettingsStore store)
        {
            if (store == null) return false;
            var first = store.GetInt(DoneKey, 0) == 0;
            store.SetInt(DoneKey, 1);
            store.Save();
            return first;
        }
    }
}
