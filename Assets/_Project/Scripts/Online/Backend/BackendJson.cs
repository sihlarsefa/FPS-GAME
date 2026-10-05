using System;
using System.Globalization;
using System.Text;

namespace Project.Online.Backend
{
    /// <summary>
    /// Hafif JSON okuyucu/yazıcı — Backend camelCase wire formatı.
    /// Unity JsonUtility üst düzey dizi ve Guid desteklemediği için özel ayrıştırma kullanılır.
    /// </summary>
    public static class BackendJson
    {
        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            var sb = new StringBuilder(value.Length + 8);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:x4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        public static string Quote(string value) => "\"" + Escape(value ?? "") + "\"";

        public static bool TryGetString(string json, string key, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
                return false;

            var pattern = "\"" + key + "\"";
            var idx = IndexOfKey(json, pattern);
            if (idx < 0)
                return false;

            idx = json.IndexOf(':', idx + pattern.Length);
            if (idx < 0)
                return false;
            idx++;

            while (idx < json.Length && char.IsWhiteSpace(json[idx]))
                idx++;

            if (idx >= json.Length)
                return false;

            if (json[idx] == 'n' && MatchLiteral(json, idx, "null"))
            {
                value = null;
                return true;
            }

            if (json[idx] != '"')
                return false;

            idx++;
            var sb = new StringBuilder();
            while (idx < json.Length)
            {
                var c = json[idx++];
                if (c == '\\' && idx < json.Length)
                {
                    var n = json[idx++];
                    switch (n)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (idx + 3 < json.Length &&
                                int.TryParse(json.Substring(idx, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                            {
                                sb.Append((char)code);
                                idx += 4;
                            }
                            break;
                        default: sb.Append(n); break;
                    }
                    continue;
                }

                if (c == '"')
                {
                    value = sb.ToString();
                    return true;
                }

                sb.Append(c);
            }

            return false;
        }

        public static bool TryGetInt(string json, string key, out int value)
        {
            value = 0;
            if (!TryGetRawNumber(json, key, out var raw))
                return false;
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryGetFloat(string json, string key, out float value)
        {
            value = 0f;
            if (!TryGetRawNumber(json, key, out var raw))
                return false;
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryGetBool(string json, string key, out bool value)
        {
            value = false;
            if (!TryGetRawToken(json, key, out var raw))
                return false;
            if (raw == "true") { value = true; return true; }
            if (raw == "false") { value = false; return true; }
            return false;
        }

        public static bool TryGetGuid(string json, string key, out Guid value)
        {
            value = Guid.Empty;
            if (!TryGetString(json, key, out var s) || string.IsNullOrEmpty(s))
                return false;
            return Guid.TryParse(s, out value);
        }

        public static bool TryGetDateTimeOffset(string json, string key, out DateTimeOffset value)
        {
            value = default;
            if (!TryGetString(json, key, out var s) || string.IsNullOrEmpty(s))
                return false;
            return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value);
        }

        public static string ExtractObject(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
                return null;

            var pattern = "\"" + key + "\"";
            var idx = IndexOfKey(json, pattern);
            if (idx < 0)
                return null;

            idx = json.IndexOf(':', idx + pattern.Length);
            if (idx < 0)
                return null;
            idx++;
            while (idx < json.Length && char.IsWhiteSpace(json[idx]))
                idx++;

            if (idx >= json.Length || json[idx] == 'n')
                return null;

            if (json[idx] != '{')
                return null;

            return ExtractBalanced(json, idx, '{', '}');
        }

        public static string ExtractArray(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
                return null;

            var pattern = "\"" + key + "\"";
            var idx = IndexOfKey(json, pattern);
            if (idx < 0)
                return null;

            idx = json.IndexOf(':', idx + pattern.Length);
            if (idx < 0)
                return null;
            idx++;
            while (idx < json.Length && char.IsWhiteSpace(json[idx]))
                idx++;

            if (idx >= json.Length || json[idx] != '[')
                return null;

            return ExtractBalanced(json, idx, '[', ']');
        }

        public static System.Collections.Generic.List<string> SplitArrayObjects(string arrayJson)
        {
            var list = new System.Collections.Generic.List<string>();
            if (string.IsNullOrEmpty(arrayJson))
                return list;

            var start = arrayJson.IndexOf('[');
            var end = arrayJson.LastIndexOf(']');
            if (start < 0 || end <= start)
                return list;

            var i = start + 1;
            while (i < end)
            {
                while (i < end && (char.IsWhiteSpace(arrayJson[i]) || arrayJson[i] == ','))
                    i++;
                if (i >= end)
                    break;

                if (arrayJson[i] == '{')
                {
                    var obj = ExtractBalanced(arrayJson, i, '{', '}');
                    if (obj == null)
                        break;
                    list.Add(obj);
                    i += obj.Length;
                    continue;
                }

                if (arrayJson[i] == '"')
                {
                    if (TryReadQuoted(arrayJson, i, out var quoted, out var next))
                    {
                        list.Add(quoted);
                        i = next;
                        continue;
                    }
                }

                // primitive
                var p = i;
                while (p < end && arrayJson[p] != ',' && arrayJson[p] != ']')
                    p++;
                list.Add(arrayJson.Substring(i, p - i).Trim());
                i = p;
            }

            return list;
        }

        public static System.Collections.Generic.List<Guid> ParseGuidArray(string arrayJson)
        {
            var result = new System.Collections.Generic.List<Guid>();
            foreach (var item in SplitArrayObjects(arrayJson ?? "[]"))
            {
                var raw = item != null ? item.Trim().Trim('"') : "";
                if (Guid.TryParse(raw, out var g))
                    result.Add(g);
            }

            return result;
        }

        public static AuthResult ParseAuthResult(string json)
        {
            var result = new AuthResult();
            TryGetString(json, "accessToken", out var access);
            TryGetString(json, "refreshToken", out var refresh);
            result.AccessToken = access ?? "";
            result.RefreshToken = refresh ?? "";
            if (TryGetDateTimeOffset(json, "expiresAt", out var exp))
                result.ExpiresAt = exp;

            var playerJson = ExtractObject(json, "player");
            if (!string.IsNullOrEmpty(playerJson))
                result.Player = ParsePlayerProfile(playerJson);

            return result;
        }

        public static PlayerProfile ParsePlayerProfile(string json)
        {
            var p = new PlayerProfile();
            if (TryGetGuid(json, "id", out var id))
                p.Id = id;
            TryGetString(json, "username", out var username);
            TryGetString(json, "email", out var email);
            p.Username = username ?? "";
            p.Email = email ?? "";
            if (TryGetInt(json, "rank", out var rank))
                p.Rank = rank;
            if (TryGetInt(json, "eloRating", out var elo))
                p.EloRating = elo;
            if (TryGetGuid(json, "squadId", out var squadId))
                p.SquadId = squadId;

            var statsJson = ExtractObject(json, "stats");
            if (!string.IsNullOrEmpty(statsJson))
                p.Stats = ParseCareerStats(statsJson);

            return p;
        }

        public static CareerStatsDto ParseCareerStats(string json)
        {
            var s = new CareerStatsDto();
            if (TryGetInt(json, "matches", out var matches)) s.Matches = matches;
            if (TryGetInt(json, "wins", out var wins)) s.Wins = wins;
            if (TryGetInt(json, "kills", out var kills)) s.Kills = kills;
            if (TryGetInt(json, "headshots", out var headshots)) s.Headshots = headshots;
            if (TryGetInt(json, "bestPlacement", out var best)) s.BestPlacement = best;
            if (TryGetFloat(json, "totalDamage", out var dmg)) s.TotalDamage = dmg;
            if (TryGetFloat(json, "longestSurvivalSeconds", out var surv)) s.LongestSurvivalSeconds = surv;
            if (TryGetInt(json, "experience", out var xp)) s.Experience = xp;
            if (TryGetInt(json, "rank", out var rank)) s.Rank = rank;
            return s;
        }

        public static SquadInfo ParseSquadInfo(string json)
        {
            var s = new SquadInfo();
            if (TryGetGuid(json, "id", out var id)) s.Id = id;
            TryGetString(json, "name", out var name);
            s.Name = name ?? "";
            if (TryGetGuid(json, "leaderId", out var leader)) s.LeaderId = leader;
            TryGetString(json, "inviteCode", out var code);
            s.InviteCode = code ?? "";
            TryGetString(json, "region", out var region);
            s.Region = region ?? "tr";
            if (TryGetInt(json, "openSlots", out var slots)) s.OpenSlots = slots;
            if (TryGetBool(json, "allReady", out var ready)) s.AllReady = ready;

            var members = ExtractArray(json, "memberIds");
            if (!string.IsNullOrEmpty(members))
                s.MemberIds = ParseGuidArray(members);

            var readyMembers = ExtractArray(json, "readyMemberIds");
            if (!string.IsNullOrEmpty(readyMembers))
                s.ReadyMemberIds = ParseGuidArray(readyMembers);

            return s;
        }

        public static QueueInfo ParseQueueInfo(string json)
        {
            var q = new QueueInfo();
            if (TryGetGuid(json, "ticketId", out var id)) q.TicketId = id;
            TryGetString(json, "status", out var status);
            q.Status = status ?? "";
            TryGetString(json, "region", out var region);
            q.Region = region ?? "";
            if (TryGetDateTimeOffset(json, "enqueuedAt", out var at))
                q.EnqueuedAt = at;
            return q;
        }

        public static LeaderboardRow ParseLeaderboardRow(string json)
        {
            var row = new LeaderboardRow();
            if (TryGetInt(json, "rank", out var rank)) row.Rank = rank;
            if (TryGetGuid(json, "playerId", out var pid)) row.PlayerId = pid;
            TryGetString(json, "username", out var username);
            row.Username = username ?? "";
            if (TryGetInt(json, "militaryRank", out var mr)) row.MilitaryRank = mr;
            if (TryGetInt(json, "value", out var value)) row.Value = value;
            if (TryGetInt(json, "elo", out var elo)) row.Elo = elo;
            return row;
        }

        public static AchievementDto ParseAchievement(string json)
        {
            var a = new AchievementDto();
            TryGetString(json, "id", out var id);
            TryGetString(json, "title", out var title);
            TryGetString(json, "description", out var desc);
            a.Id = id ?? "";
            a.Title = title ?? "";
            a.Description = desc ?? "";
            if (TryGetInt(json, "target", out var target)) a.Target = target;
            if (TryGetInt(json, "progress", out var progress)) a.Progress = progress;
            if (TryGetBool(json, "unlocked", out var unlocked)) a.Unlocked = unlocked;
            return a;
        }

        public static ReadyStatusDto ParseReadyStatus(string json)
        {
            var r = new ReadyStatusDto();
            if (TryGetGuid(json, "squadId", out var sid)) r.SquadId = sid;
            if (TryGetGuid(json, "playerId", out var pid)) r.PlayerId = pid;
            if (TryGetBool(json, "isReady", out var ready)) r.IsReady = ready;
            if (TryGetBool(json, "allReady", out var all)) r.AllReady = all;
            return r;
        }

        public static string BuildRegisterBody(string username, string email, string password, string region)
        {
            return "{"
                + "\"username\":" + Quote(username)
                + ",\"email\":" + Quote(email)
                + ",\"password\":" + Quote(password)
                + ",\"region\":" + Quote(region ?? "tr")
                + "}";
        }

        public static string BuildLoginBody(string username, string password)
        {
            return "{"
                + "\"username\":" + Quote(username)
                + ",\"password\":" + Quote(password)
                + "}";
        }

        public static string BuildRefreshBody(string refreshToken)
        {
            return "{\"refreshToken\":" + Quote(refreshToken) + "}";
        }

        public static string BuildCreateSquadBody(string name, string region)
        {
            return "{"
                + "\"name\":" + Quote(name)
                + ",\"region\":" + Quote(region ?? "tr")
                + "}";
        }

        public static string BuildJoinSquadBody(string inviteCode)
        {
            return "{\"inviteCode\":" + Quote(inviteCode) + "}";
        }

        public static string BuildQueueBody(string region, int? maxPingMs)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            var first = true;
            if (!string.IsNullOrEmpty(region))
            {
                sb.Append("\"region\":").Append(Quote(region));
                first = false;
            }

            if (maxPingMs.HasValue)
            {
                if (!first) sb.Append(',');
                sb.Append("\"maxPingMs\":").Append(maxPingMs.Value.ToString(CultureInfo.InvariantCulture));
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static bool TryGetRawNumber(string json, string key, out string raw)
        {
            raw = null;
            if (!TryGetRawToken(json, key, out raw))
                return false;
            if (raw == "null" || raw == "true" || raw == "false")
                return false;
            return true;
        }

        private static bool TryGetRawToken(string json, string key, out string raw)
        {
            raw = null;
            var pattern = "\"" + key + "\"";
            var idx = IndexOfKey(json, pattern);
            if (idx < 0)
                return false;

            idx = json.IndexOf(':', idx + pattern.Length);
            if (idx < 0)
                return false;
            idx++;
            while (idx < json.Length && char.IsWhiteSpace(json[idx]))
                idx++;
            if (idx >= json.Length)
                return false;

            if (json[idx] == '"')
                return false;

            var start = idx;
            while (idx < json.Length)
            {
                var c = json[idx];
                if (c == ',' || c == '}' || c == ']' || char.IsWhiteSpace(c))
                    break;
                idx++;
            }

            raw = json.Substring(start, idx - start);
            return raw.Length > 0;
        }

        private static int IndexOfKey(string json, string pattern)
        {
            var idx = 0;
            while (idx < json.Length)
            {
                idx = json.IndexOf(pattern, idx, StringComparison.Ordinal);
                if (idx < 0)
                    return -1;

                // ensure key boundary (start or non-word before quote already in pattern)
                var after = idx + pattern.Length;
                while (after < json.Length && char.IsWhiteSpace(json[after]))
                    after++;
                if (after < json.Length && json[after] == ':')
                    return idx;

                idx += pattern.Length;
            }

            return -1;
        }

        private static bool MatchLiteral(string json, int idx, string literal)
        {
            if (idx + literal.Length > json.Length)
                return false;
            return string.CompareOrdinal(json, idx, literal, 0, literal.Length) == 0;
        }

        private static string ExtractBalanced(string json, int start, char open, char close)
        {
            if (start < 0 || start >= json.Length || json[start] != open)
                return null;

            var depth = 0;
            var inString = false;
            var escape = false;
            for (var i = start; i < json.Length; i++)
            {
                var c = json[i];
                if (inString)
                {
                    if (escape)
                    {
                        escape = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escape = true;
                        continue;
                    }

                    if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == open)
                    depth++;
                else if (c == close)
                {
                    depth--;
                    if (depth == 0)
                        return json.Substring(start, i - start + 1);
                }
            }

            return null;
        }

        private static bool TryReadQuoted(string json, int start, out string value, out int next)
        {
            value = null;
            next = start;
            if (start >= json.Length || json[start] != '"')
                return false;

            var i = start + 1;
            var sb = new StringBuilder();
            while (i < json.Length)
            {
                var c = json[i++];
                if (c == '\\' && i < json.Length)
                {
                    sb.Append(json[i++]);
                    continue;
                }

                if (c == '"')
                {
                    value = sb.ToString();
                    next = i;
                    return true;
                }

                sb.Append(c);
            }

            return false;
        }
    }
}
