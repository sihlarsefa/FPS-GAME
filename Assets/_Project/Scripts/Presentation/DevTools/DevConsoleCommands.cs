using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure;
using Project.Infrastructure.AI;
using Project.Infrastructure.Combat;
using Project.Infrastructure.World;
using Project.Presentation.Bootstrap;
using Project.Presentation.Player;
using UnityEngine;
using UnityEngine.Profiling;

namespace Project.Presentation.DevTools
{
    /// <summary>Geliştirici konsol komutlarının uygulanması ve çalışma zamanı durum bayrakları.</summary>
    public static class DevConsoleCommands
    {
        public const int MaxLogLines = 200;

        private static readonly Dictionary<string, Command> Commands = new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> Names = new(32);
        private static readonly StringBuilder Shared = new(256);

        private static bool _god;
        private static bool _noclip;
        private static bool _aiEnabled = true;
        private static bool _zonePaused;
        private static bool _showFps;
        private static bool _showNet;
        private static int _nextBotId = 9000;
        private static float _fpsAccum;
        private static int _fpsFrames;
        private static float _fpsValue = 60f;
        private static Action<Combatant, DamageInfo> _godDamagedHandler;

        private sealed class Command
        {
            public string Name;
            public string Usage;
            public string Help;
            public Func<string[], string> Execute;
        }

        static DevConsoleCommands()
        {
            Register("help", "help [komut]", "Komut listesi veya bir komutun yardımı.", CmdHelp);
            Register("give", "give <silahId>", "Yerel oyuncuya silah verir (ör. ar_mpt76).", CmdGive);
            Register("ammo", "ammo", "Tüm mermi türlerini doldurur ve şarjörleri yeniler.", CmdAmmo);
            Register("heal", "heal", "Canı tam doldurur.", CmdHeal);
            Register("god", "god", "Ölümsüzlük aç/kapa (yerel oyuncu).", CmdGod);
            Register("noclip", "noclip", "Çarpışmasız uçuş aç/kapa.", CmdNoclip);
            Register("spawn_bots", "spawn_bots <n> [tim]", "Oyuncu yakınına bot doğurur.", CmdSpawnBots);
            Register("kill_team", "kill_team <tim>", "Verilen timdeki tüm botları öldürür.", CmdKillTeam);
            Register("ai", "ai off|on", "Tüm bot yapay zekasını aç/kapa.", CmdAi);
            Register("zone", "zone next|pause", "Bölge aşamasını ilerletir veya duraklatır.", CmdZone);
            Register("timescale", "timescale <x>", "Time.timeScale ayarlar (0.1–8).", CmdTimescale);
            Register("tp", "tp <lokasyon>", "WorldMetadata.Locations adına ışınlar.", CmdTp);
            Register("artillery", "artillery here", "Bulunduğun noktaya topçu çağırır.", CmdArtillery);
            Register("rank", "rank <rütbe>", "Kariyer rütbesini/XP eşiğini ayarlar.", CmdRank);
            Register("xp", "xp <miktar>", "Kariyer tecrübe puanını ayarlar.", CmdXp);
            Register("fps", "fps", "FPS göstergesini aç/kapa.", CmdFps);
            Register("netstats", "netstats", "Ağ oturumu özetini göster / HUD aç-kapa.", CmdNetstats);
            Register("screenshot", "screenshot", "Ekran görüntüsü kaydeder (Logs/screens).", CmdScreenshot);
            Register("loadscene", "loadscene <ad>", "Sahne yükler (MainMenu, KuzgunVadisi, TrainingRange).", CmdLoadScene);
            Register("clear", "clear", "Konsol günlüğünü temizler.", _ => { DevConsole.Instance?.ClearLog(); return "Temizlendi."; });
            Register("weapons", "weapons", "Verilebilir silah kimliklerini listeler.", CmdWeapons);
            Register("locations", "locations", "Işınlanılabilir lokasyonları listeler.", CmdLocations);
        }

        public static bool GodMode => _god;
        public static bool NoclipActive => _noclip;
        public static bool AiEnabled => _aiEnabled;
        public static bool ZonePaused => _zonePaused;
        public static bool ShowFps => _showFps;
        public static bool ShowNetStats => _showNet;

        public static IReadOnlyList<string> CommandNames => Names;

        public static string Execute(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            var parts = Tokenize(line);
            if (parts.Length == 0)
                return null;

            if (!Commands.TryGetValue(parts[0], out var cmd))
                return "Bilinmeyen komut: " + parts[0] + "  (help)";

            try
            {
                var args = new string[parts.Length - 1];
                Array.Copy(parts, 1, args, 0, args.Length);
                return cmd.Execute(args) ?? "Tamam.";
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return "Hata: " + e.Message;
            }
        }

        public static IReadOnlyList<string> Suggest(string partial)
        {
            var result = new List<string>(8);
            if (string.IsNullOrEmpty(partial))
            {
                for (var i = 0; i < Names.Count && result.Count < 12; i++)
                    result.Add(Names[i]);
                return result;
            }

            var tokens = Tokenize(partial);
            if (tokens.Length == 0)
                return result;

            if (tokens.Length == 1 && !partial.EndsWith(" ", StringComparison.Ordinal))
            {
                var prefix = tokens[0];
                for (var i = 0; i < Names.Count; i++)
                {
                    if (Names[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        result.Add(Names[i]);
                }

                return result;
            }

            var cmdName = tokens[0];
            if (!Commands.TryGetValue(cmdName, out _))
                return result;

            var argPartial = tokens.Length > 1 ? tokens[tokens.Length - 1] : string.Empty;
            if (partial.EndsWith(" ", StringComparison.Ordinal))
                argPartial = string.Empty;

            switch (cmdName.ToLowerInvariant())
            {
                case "give":
                    SuggestWeapons(argPartial, result);
                    break;
                case "tp":
                    SuggestLocations(argPartial, result);
                    break;
                case "rank":
                    SuggestRanks(argPartial, result);
                    break;
                case "ai":
                case "zone":
                    SuggestTokens(argPartial, result, cmdName.Equals("ai", StringComparison.OrdinalIgnoreCase)
                        ? new[] { "on", "off" }
                        : new[] { "next", "pause" });
                    break;
                case "artillery":
                    SuggestTokens(argPartial, result, "here");
                    break;
                case "loadscene":
                    SuggestTokens(argPartial, result, SceneNames.MainMenu, SceneNames.Operation, SceneNames.AyazGecidi, SceneNames.MaviLiman, SceneNames.Training);
                    break;
                case "help":
                    for (var i = 0; i < Names.Count; i++)
                    {
                        if (string.IsNullOrEmpty(argPartial) || Names[i].StartsWith(argPartial, StringComparison.OrdinalIgnoreCase))
                            result.Add(Names[i]);
                    }
                    break;
            }

            return result;
        }

        public static string Complete(string current)
        {
            var suggestions = Suggest(current ?? string.Empty);
            if (suggestions.Count == 0)
                return current;

            var tokens = Tokenize(current ?? string.Empty);
            var endsWithSpace = (current ?? string.Empty).EndsWith(" ", StringComparison.Ordinal);

            if (tokens.Length == 0 || (tokens.Length == 1 && !endsWithSpace))
            {
                var match = LongestCommonPrefix(suggestions);
                return string.IsNullOrEmpty(match) ? suggestions[0] : match;
            }

            Shared.Clear();
            Shared.Append(tokens[0]);
            for (var i = 1; i < tokens.Length - (endsWithSpace ? 0 : 1); i++)
            {
                Shared.Append(' ');
                Shared.Append(tokens[i]);
            }

            if (Shared.Length > 0)
                Shared.Append(' ');
            Shared.Append(suggestions[0]);
            return Shared.ToString();
        }

        /// <summary>Her kare: god heal, noclip uçuş, zone pause, FPS ölçümü.</summary>
        public static void Tick(float unscaledDelta)
        {
            SampleFps(unscaledDelta);

            if (_zonePaused && GameContext.TryGet<ZoneService>(out var zone))
                HoldZone(zone);

            var player = FindPlayer();
            if (player == null)
                return;

            if (_god)
            {
                var c = player.Combatant;
                if (c != null && c.Health != null)
                {
                    if (!c.IsAlive)
                        c.Revive();
                    else if (c.Health.Current < c.Health.Max)
                        c.Health.ResetToFull();
                }
            }

            if (_noclip && (DevConsole.Instance == null || !DevConsole.Instance.IsOpen))
                TickNoclip(player, unscaledDelta);
        }

        public static string OverlayText()
        {
            Shared.Clear();
            if (_showFps)
                Shared.Append("FPS ").Append(_fpsValue.ToString("0.0", CultureInfo.InvariantCulture));

            if (_showNet)
            {
                if (Shared.Length > 0)
                    Shared.Append("  |  ");
                Shared.Append(BuildNetSummary());
            }

            if (_god)
            {
                if (Shared.Length > 0)
                    Shared.Append("  |  ");
                Shared.Append("GOD");
            }

            if (_noclip)
            {
                if (Shared.Length > 0)
                    Shared.Append("  |  ");
                Shared.Append("NOCLIP");
            }

            if (!_aiEnabled)
            {
                if (Shared.Length > 0)
                    Shared.Append("  |  ");
                Shared.Append("AI OFF");
            }

            if (_zonePaused)
            {
                if (Shared.Length > 0)
                    Shared.Append("  |  ");
                Shared.Append("ZONE PAUSE");
            }

            return Shared.ToString();
        }

        public static void Shutdown()
        {
            DetachGodHandler();
            _god = false;
            _noclip = false;
            _zonePaused = false;
            _showFps = false;
            _showNet = false;
            _aiEnabled = true;
        }

        // ------------------------------------------------------------------ komutlar

        private static string CmdHelp(string[] args)
        {
            if (args.Length > 0 && Commands.TryGetValue(args[0], out var one))
                return one.Usage + " — " + one.Help;

            Shared.Clear();
            Shared.Append("Komutlar:");
            for (var i = 0; i < Names.Count; i++)
            {
                if (!Commands.TryGetValue(Names[i], out var cmd))
                    continue;
                Shared.Append('\n').Append("  ").Append(cmd.Usage);
            }

            return Shared.ToString();
        }

        private static string CmdGive(string[] args)
        {
            if (args.Length < 1)
                return "Kullanım: give <silahId>  (weapons)";

            var id = args[0].Trim();
            if (!WeaponCatalog.Contains(id))
                return "Bilinmeyen silah: " + id + "  (weapons)";

            var inv = FindInventory();
            if (inv == null)
                return "Oyuncu envanteri yok.";

            var weapon = inv.GiveWeapon(id, true);
            return weapon != null
                ? "Verildi: " + WeaponCatalog.GetDisplayName(id) + " (" + id + ")"
                : "Verilemedi: " + id;
        }

        private static string CmdAmmo(string[] args)
        {
            var inv = FindInventory();
            if (inv == null)
                return "Oyuncu envanteri yok.";

            inv.GiveItem(ItemIds.Ammo9, 300);
            inv.GiveItem(ItemIds.Ammo556, 300);
            inv.GiveItem(ItemIds.Ammo762, 300);
            inv.GiveItem(ItemIds.Ammo12, 80);

            for (var i = 0; i < inv.SlotCount; i++)
            {
                var w = inv.GetWeapon(i);
                w?.ResetState(true);
            }

            return "Mermi dolduruldu.";
        }

        private static string CmdHeal(string[] args)
        {
            var c = FindCombatant();
            if (c == null)
                return "Oyuncu yok.";

            if (!c.IsAlive)
                c.Revive();
            else
                c.Health?.ResetToFull();

            return "Can dolu (" + Mathf.CeilToInt(c.Health != null ? c.Health.Current : 0f) + ").";
        }

        private static string CmdGod(string[] args)
        {
            _god = !_god;
            var player = FindPlayer();
            var combatant = player != null ? player.Combatant : null;

            DetachGodHandler();
            if (_god && combatant != null)
            {
                _godDamagedHandler = (_, __) =>
                {
                    if (!_god || combatant == null)
                        return;
                    if (!combatant.IsAlive)
                        combatant.Revive();
                    else
                        combatant.Health?.ResetToFull();
                };
                combatant.Damaged += _godDamagedHandler;
                if (combatant.Health != null)
                    combatant.Health.ResetToFull();
            }

            return _god ? "God: AÇIK (kalıcı koruma için Combatant.Invulnerable kancası bekleniyor)." : "God: KAPALI.";
        }

        private static string CmdNoclip(string[] args)
        {
            var player = FindPlayer();
            if (player == null || player.Motor == null)
                return "Oyuncu motoru yok.";

            _noclip = !_noclip;
            if (_noclip)
            {
                player.Motor.ControlEnabled = false;
                return "Noclip: AÇIK (WASD + Q/E, Shift hızlanır).";
            }

            player.Motor.ControlEnabled = true;
            var pos = player.transform.position;
            var yaw = player.transform.eulerAngles.y;
            player.Teleport(BootstrapUtility.GroundPoint(WorldMetadata.Instance, pos), yaw);
            return "Noclip: KAPALI.";
        }

        private static string CmdSpawnBots(string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
                return "Kullanım: spawn_bots <n> [tim]";

            count = Mathf.Clamp(count, 1, 40);
            var team = 1;
            if (args.Length >= 2)
                int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out team);
            team = Mathf.Clamp(team, 0, 15);

            var player = FindPlayer();
            var origin = player != null ? player.transform.position : Vector3.zero;
            var yaw = player != null ? player.transform.eulerAngles.y : 0f;
            var world = WorldMetadata.Instance;
            var parent = player != null ? player.transform.parent : null;
            var difficulty = BotDifficulty.Normal;

            var spawned = 0;
            for (var i = 0; i < count; i++)
            {
                var angle = (i / (float)Mathf.Max(1, count)) * Mathf.PI * 2f;
                var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (4f + i * 0.35f);
                var ground = BootstrapUtility.GroundPoint(world, origin + offset);
                var id = new PlayerId(_nextBotId++);
                var bot = BotController.Create(new BotSpawnArgs
                {
                    Id = id,
                    Name = "DevBot " + id.Value,
                    Team = team,
                    Role = TeamRole.Rifleman,
                    Difficulty = difficulty,
                    SpawnOnGround = true,
                    GroundPosition = ground,
                    GroundYaw = yaw + i * 15f,
                    Seed = id.Value * 17 + 3,
                    Slot = 1 + (i % 9),
                    Parent = parent,
                    RegisterWithMatch = true,
                    GiveLoadout = true
                });
                if (bot != null)
                {
                    bot.enabled = _aiEnabled;
                    spawned++;
                }
            }

            return spawned + " bot doğuruldu (tim " + team + ").";
        }

        private static string CmdKillTeam(string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var team))
                return "Kullanım: kill_team <tim>";

            var killed = 0;
            var bots = BotController.All;
            for (var i = 0; i < bots.Count; i++)
            {
                var bot = bots[i];
                if (bot == null || bot.Team != team)
                    continue;

                var c = bot.Combatant;
                if (c == null || !c.IsAlive)
                    continue;

                c.ApplyDamage(new DamageInfo(9999f, PlayerId.Invalid, DamageSourceIds.Artillery));
                killed++;
            }

            return "Tim " + team + ": " + killed + " asker öldürüldü.";
        }

        private static string CmdAi(string[] args)
        {
            if (args.Length < 1)
                return "Kullanım: ai off|on  (şu an: " + (_aiEnabled ? "on" : "off") + ")";

            var token = args[0].Trim().ToLowerInvariant();
            if (token == "off" || token == "0" || token == "false")
                _aiEnabled = false;
            else if (token == "on" || token == "1" || token == "true")
                _aiEnabled = true;
            else
                return "Kullanım: ai off|on";

            var bots = BotController.All;
            for (var i = 0; i < bots.Count; i++)
            {
                if (bots[i] != null)
                    bots[i].enabled = _aiEnabled;
            }

            return "AI: " + (_aiEnabled ? "AÇIK" : "KAPALI") + " (" + bots.Count + " bot). " + BotLod.StatsLine;
        }

        private static string CmdZone(string[] args)
        {
            if (args.Length < 1)
                return "Kullanım: zone next|pause";

            if (!GameContext.TryGet<ZoneService>(out var zone) || zone == null)
                return "ZoneService yok.";

            var token = args[0].Trim().ToLowerInvariant();
            if (token == "pause")
            {
                _zonePaused = !_zonePaused;
                if (!_zonePaused)
                    SetZoneActive(zone, true);
                return "Zone: " + (_zonePaused ? "DURAKLATILDI" : "DEVAM");
            }

            if (token != "next")
                return "Kullanım: zone next|pause";

            if (!zone.IsActive)
                zone.Start();

            _zonePaused = false;
            SetZoneActive(zone, true);

            var remaining = zone.StageRemainingSeconds;
            if (remaining <= 0f && zone.Stage != ZoneStage.Finished)
                remaining = 0.05f;

            zone.Tick(remaining + 0.05f);
            return "Zone aşama: " + zone.Stage + "  faz " + zone.PhaseIndex + "/" + zone.PhaseCount
                   + "  kalan " + zone.StageRemainingSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s";
        }

        private static string CmdTimescale(string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
                return "Kullanım: timescale <x>  (şu an " + Time.timeScale.ToString("0.##", CultureInfo.InvariantCulture) + ")";

            scale = Mathf.Clamp(scale, 0.05f, 8f);
            Time.timeScale = scale;
            return "timeScale = " + scale.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string CmdTp(string[] args)
        {
            if (args.Length < 1)
                return "Kullanım: tp <lokasyon>  (locations)";

            var player = FindPlayer();
            if (player == null)
                return "Oyuncu yok.";

            var world = WorldMetadata.Instance;
            if (world == null || world.Locations == null || world.Locations.Count == 0)
                return "Lokasyon listesi boş.";

            var query = string.Join(" ", args).Trim();
            NamedLocation match = null;
            for (var i = 0; i < world.Locations.Count; i++)
            {
                var loc = world.Locations[i];
                if (loc == null || string.IsNullOrEmpty(loc.Name))
                    continue;
                if (string.Equals(loc.Name, query, StringComparison.OrdinalIgnoreCase))
                {
                    match = loc;
                    break;
                }
            }

            if (match == null)
            {
                for (var i = 0; i < world.Locations.Count; i++)
                {
                    var loc = world.Locations[i];
                    if (loc == null || string.IsNullOrEmpty(loc.Name))
                        continue;
                    if (loc.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        match = loc;
                        break;
                    }
                }
            }

            if (match == null)
                return "Lokasyon bulunamadı: " + query;

            var ground = BootstrapUtility.GroundPoint(world, new Vector3(match.Center.x, 0f, match.Center.y));
            if (_noclip)
            {
                player.Motor.ControlEnabled = false;
                player.transform.position = ground + Vector3.up * 1.2f;
            }
            else
            {
                player.Teleport(ground, player.transform.eulerAngles.y);
            }

            return "Işınlandı: " + match.Name;
        }

        private static string CmdArtillery(string[] args)
        {
            if (args.Length < 1 || !string.Equals(args[0], "here", StringComparison.OrdinalIgnoreCase))
                return "Kullanım: artillery here";

            if (!GameContext.TryGet<ArtilleryService>(out var artillery) || artillery == null)
                return "ArtilleryService yok.";

            var player = FindPlayer();
            var combatant = player != null ? player.Combatant : null;
            if (combatant == null)
                return "Oyuncu yok.";

            ClearArtilleryCooldown(artillery, combatant.Team);
            var p = player.transform.position;
            var ok = artillery.TryCall(combatant.Team, combatant.Id, new Float3(p.x, p.y, p.z));
            return ok ? "Topçu çağrıldı." : "Topçu çağrılamadı (hazır değil).";
        }

        private static string CmdRank(string[] args)
        {
            if (args.Length < 1)
                return "Kullanım: rank <rütbe>  (ör. Yuzbasi, Tegmen, Er)";

            if (!TryParseRank(string.Join(" ", args), out var rank))
                return "Bilinmeyen rütbe. Örnek: Er, Onbasi, Cavus, Tegmen, Yuzbasi, Albay";

            var career = ResolveCareer();
            if (career == null)
                return "CareerStatsService yok.";

            var xp = RankCatalog.RequiredExperience(rank);
            ApplyCareerExperience(career, xp);
            return "Rütbe: " + RankCatalog.GetName(rank) + "  (XP " + xp + ")";
        }

        private static string CmdXp(string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var xp))
                return "Kullanım: xp <miktar>";

            xp = Mathf.Max(0, xp);
            var career = ResolveCareer();
            if (career == null)
                return "CareerStatsService yok.";

            ApplyCareerExperience(career, xp);
            var rank = RankCatalog.RankForExperience(xp);
            return "XP = " + xp + "  →  " + RankCatalog.GetName(rank);
        }

        private static string CmdFps(string[] args)
        {
            _showFps = !_showFps;
            return "FPS göstergesi: " + (_showFps ? "AÇIK" : "KAPALI");
        }

        private static string CmdNetstats(string[] args)
        {
            _showNet = !_showNet;
            return BuildNetSummary() + "\nGösterge: " + (_showNet ? "AÇIK" : "KAPALI");
        }

        private static string CmdScreenshot(string[] args)
        {
            try
            {
                var dir = Path.Combine(UnityEngine.Application.persistentDataPath, "Logs", "screens");
                Directory.CreateDirectory(dir);
                var name = "shot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".png";
                var path = Path.Combine(dir, name);
                ScreenCapture.CaptureScreenshot(path);
                return "Kayıt: " + path;
            }
            catch (Exception e)
            {
                return "Screenshot hatası: " + e.Message;
            }
        }

        private static string CmdLoadScene(string[] args)
        {
            if (args.Length < 1)
                return "Kullanım: loadscene <ad>  (MainMenu, KuzgunVadisi, TrainingRange)";

            var name = args[0].Trim();
            if (string.Equals(name, "menu", StringComparison.OrdinalIgnoreCase))
                name = SceneNames.MainMenu;
            else if (string.Equals(name, "match", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(name, "op", StringComparison.OrdinalIgnoreCase))
                name = SceneNames.OperationSceneFor(GameSession.SelectedMap);
            else if (string.Equals(name, "train", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(name, "range", StringComparison.OrdinalIgnoreCase))
                name = SceneNames.Training;

            GameSession.LoadScene(name);
            return "Yükleniyor: " + name;
        }

        private static string CmdWeapons(string[] args)
        {
            Shared.Clear();
            var all = WeaponCatalog.All;
            for (var i = 0; i < all.Count; i++)
            {
                if (i > 0)
                    Shared.Append(", ");
                Shared.Append(all[i].WeaponId);
            }

            return Shared.ToString();
        }

        private static string CmdLocations(string[] args)
        {
            var world = WorldMetadata.Instance;
            if (world == null || world.Locations == null || world.Locations.Count == 0)
                return "Lokasyon yok.";

            Shared.Clear();
            for (var i = 0; i < world.Locations.Count; i++)
            {
                var loc = world.Locations[i];
                if (loc == null || string.IsNullOrEmpty(loc.Name))
                    continue;
                if (Shared.Length > 0)
                    Shared.Append(", ");
                Shared.Append(loc.Name);
            }

            return Shared.Length > 0 ? Shared.ToString() : "Lokasyon yok.";
        }

        // ------------------------------------------------------------------ yardımcılar

        private static void Register(string name, string usage, string help, Func<string[], string> execute)
        {
            Commands[name] = new Command { Name = name, Usage = usage, Help = help, Execute = execute };
            Names.Add(name);
        }

        private static string[] Tokenize(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return Array.Empty<string>();

            var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return parts ?? Array.Empty<string>();
        }

        private static PlayerController FindPlayer()
        {
            return UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        }

        private static Combatant FindCombatant()
        {
            var player = FindPlayer();
            return player != null ? player.Combatant : null;
        }

        private static InventoryService FindInventory()
        {
            var player = FindPlayer();
            return player != null ? player.Inventory : null;
        }

        private static CareerStatsService ResolveCareer()
        {
            if (GameSession.Career != null)
                return GameSession.Career;
            GameContext.TryGet(out CareerStatsService career);
            return career;
        }

        private static void ApplyCareerExperience(CareerStatsService career, int xp)
        {
            var stats = career.Current;
            if (stats == null)
                return;

            stats.Experience = xp;
            stats.Rank = RankCatalog.RankForExperience(xp);

            var persist = typeof(CareerStatsService).GetMethod("Persist", BindingFlags.Instance | BindingFlags.NonPublic);
            persist?.Invoke(career, new object[] { stats });

            // Changed olayı: derleyici özel alan üretir — Action<CareerStats> alanını bulup tetikle.
            var fields = typeof(CareerStatsService).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            for (var i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType != typeof(Action<CareerStats>))
                    continue;
                if (fields[i].GetValue(career) is Action<CareerStats> handlers)
                    handlers.Invoke(stats);
                break;
            }
        }

        private static bool TryParseRank(string text, out MilitaryRank rank)
        {
            rank = MilitaryRank.Er;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var t = text.Trim().Replace(" ", "").Replace(".", "").Replace("ı", "i").Replace("İ", "I");
            if (Enum.TryParse(t, true, out rank))
                return true;

            // Türkçe ad / kısa ad
            var all = RankCatalog.All;
            for (var i = 0; i < all.Count; i++)
            {
                var r = all[i];
                var full = RankCatalog.GetName(r).Replace(" ", "").Replace(".", "");
                var shortName = RankCatalog.GetShortName(r).Replace(" ", "").Replace(".", "");
                if (string.Equals(full, text.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(shortName, text.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(NormalizeTr(full), NormalizeTr(text), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(NormalizeTr(shortName), NormalizeTr(text), StringComparison.OrdinalIgnoreCase))
                {
                    rank = r;
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeTr(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            return s.Trim()
                .Replace(" ", "")
                .Replace(".", "")
                .Replace("â", "a").Replace("Â", "A")
                .Replace("ı", "i").Replace("İ", "I")
                .Replace("ğ", "g").Replace("Ğ", "G")
                .Replace("ü", "u").Replace("Ü", "U")
                .Replace("ş", "s").Replace("Ş", "S")
                .Replace("ö", "o").Replace("Ö", "O")
                .Replace("ç", "c").Replace("Ç", "C");
        }

        private static void SuggestWeapons(string prefix, List<string> result)
        {
            var all = WeaponCatalog.All;
            for (var i = 0; i < all.Count; i++)
            {
                var id = all[i].WeaponId;
                if (string.IsNullOrEmpty(prefix) || id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    result.Add(id);
            }
        }

        private static void SuggestLocations(string prefix, List<string> result)
        {
            var world = WorldMetadata.Instance;
            if (world?.Locations == null)
                return;

            for (var i = 0; i < world.Locations.Count; i++)
            {
                var name = world.Locations[i]?.Name;
                if (string.IsNullOrEmpty(name))
                    continue;
                if (string.IsNullOrEmpty(prefix) || name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    result.Add(name);
            }
        }

        private static void SuggestRanks(string prefix, List<string> result)
        {
            var all = RankCatalog.All;
            for (var i = 0; i < all.Count; i++)
            {
                var name = all[i].ToString();
                if (string.IsNullOrEmpty(prefix) || name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    result.Add(name);
            }
        }

        private static void SuggestTokens(string prefix, List<string> result, params string[] tokens)
        {
            for (var i = 0; i < tokens.Length; i++)
            {
                if (string.IsNullOrEmpty(prefix) || tokens[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    result.Add(tokens[i]);
            }
        }

        private static string LongestCommonPrefix(IReadOnlyList<string> items)
        {
            if (items == null || items.Count == 0)
                return string.Empty;
            var prefix = items[0];
            for (var i = 1; i < items.Count; i++)
            {
                var s = items[i];
                var len = Mathf.Min(prefix.Length, s.Length);
                var n = 0;
                while (n < len && char.ToLowerInvariant(prefix[n]) == char.ToLowerInvariant(s[n]))
                    n++;
                prefix = prefix.Substring(0, n);
                if (prefix.Length == 0)
                    break;
            }

            return prefix;
        }

        private static void TickNoclip(PlayerController player, float dt)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null || dt <= 0f || UI.OverlayState.TextInputActive)
                return;

            var cam = Camera.main;
            var forward = cam != null ? cam.transform.forward : player.transform.forward;
            var right = cam != null ? cam.transform.right : player.transform.right;
            forward.Normalize();
            right.Normalize();

            var move = Vector3.zero;
            // Hareket tuşları InputBindings'ten (yeniden atamaya uyar); yukarı = Zıpla / sağa eğil, aşağı = Eğil (basılı) / sola eğil.
            Func<BindAction, bool> bind = Infrastructure.Input.InputBindings.Held;
            if (bind(BindAction.MoveForward)) move += forward;
            if (bind(BindAction.MoveBack)) move -= forward;
            if (bind(BindAction.MoveRight)) move += right;
            if (bind(BindAction.MoveLeft)) move -= right;
            if (bind(BindAction.Jump) || bind(BindAction.LeanRight)) move += Vector3.up;
            if (bind(BindAction.CrouchHold) || bind(BindAction.LeanLeft)) move += Vector3.down;

            if (move.sqrMagnitude < 1e-6f)
                return;

            var speed = bind(BindAction.Sprint) ? 36f : 12f;
            player.transform.position += move.normalized * (speed * dt);
        }

        private static void SampleFps(float unscaledDelta)
        {
            if (unscaledDelta <= 0f)
                return;

            _fpsAccum += unscaledDelta;
            _fpsFrames++;
            if (_fpsAccum >= 0.35f)
            {
                _fpsValue = _fpsFrames / _fpsAccum;
                _fpsAccum = 0f;
                _fpsFrames = 0;
            }
        }

        private static string BuildNetSummary()
        {
            var net = GameContext.Network;
            Shared.Clear();
            Shared.Append("net ");
            if (net == null)
            {
                Shared.Append("yok");
                return Shared.ToString();
            }

            Shared.Append(net.Role).Append(net.IsConnected ? " bağlı" : " kopuk")
                .Append(net.HasAuthority ? " auth" : " proxy")
                .Append(" id=").Append(net.LocalPlayerId);
            Shared.Append("  bots=").Append(BotController.All.Count);
            Shared.Append("  alloc=").Append((Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f)).ToString("0.0", CultureInfo.InvariantCulture)).Append("MB");
            return Shared.ToString();
        }

        private static void HoldZone(ZoneService zone)
        {
            // Tick'i etkisizleştir: aktif bayrağını kapat (zone next/pause açılınca geri gelir).
            SetZoneActive(zone, false);
        }

        private static void SetZoneActive(ZoneService zone, bool active)
        {
            var field = typeof(ZoneService).GetField("_active", BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(zone, active);
        }

        private static void ClearArtilleryCooldown(ArtilleryService artillery, int team)
        {
            var field = typeof(ArtilleryService).GetField("_readyAt", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(artillery) is Dictionary<int, float> map)
                map.Remove(team);
        }

        private static void DetachGodHandler()
        {
            if (_godDamagedHandler == null)
                return;

            var c = FindCombatant();
            if (c != null)
                c.Damaged -= _godDamagedHandler;
            _godDamagedHandler = null;
        }
    }
}
