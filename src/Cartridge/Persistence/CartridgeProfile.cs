using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Cartridge.Persistence
{
    public sealed class LeaderboardEntrySnapshot
    {
        public string PlayerName { get; set; } = "AAA";
        public int Score { get; set; }
        public long AchievedAtTicks { get; set; }
    }

    public sealed class AchievementSnapshot
    {
        public string Id { get; set; } = string.Empty;
        public int CurrentProgress { get; set; }
        public long? UnlockedAtTicks { get; set; }
    }

    /// <summary>
    /// Complete serializable snapshot of a player's Cartridge profile (high score, leaderboard, XP/level, wallet, achievements, and custom game keys).
    /// </summary>
    public sealed class CartridgeProfileData
    {
        public int Version { get; set; } = 1;
        public int HighScore { get; set; }
        public int Level { get; set; } = 1;
        public int CurrentXp { get; set; }
        public List<LeaderboardEntrySnapshot> Leaderboard { get; set; } = new List<LeaderboardEntrySnapshot>();
        public Dictionary<string, int> WalletBalances { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public List<AchievementSnapshot> Achievements { get; set; } = new List<AchievementSnapshot>();
        public Dictionary<string, string> CustomData { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Zero-dependency JSON serializer/deserializer for CartridgeProfileData.
    /// Works identically in Unity (netstandard2.1), Godot (.NET), and standalone C# runtimes with no external NuGet packages required.
    /// </summary>
    public static class CartridgeProfileJson
    {
        public static string Serialize(CartridgeProfileData profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            var sb = new StringBuilder(512);
            sb.Append('{');
            sb.Append("\"version\":").Append(profile.Version).Append(',');
            sb.Append("\"highScore\":").Append(profile.HighScore).Append(',');
            sb.Append("\"level\":").Append(profile.Level).Append(',');
            sb.Append("\"currentXp\":").Append(profile.CurrentXp).Append(',');

            // Leaderboard
            sb.Append("\"leaderboard\":[");
            for (int i = 0; i < profile.Leaderboard.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var e = profile.Leaderboard[i];
                sb.Append('{');
                sb.Append("\"playerName\":").Append(Quote(e.PlayerName)).Append(',');
                sb.Append("\"score\":").Append(e.Score).Append(',');
                sb.Append("\"achievedAtTicks\":").Append(e.AchievedAtTicks);
                sb.Append('}');
            }
            sb.Append("],");

            // WalletBalances
            sb.Append("\"walletBalances\":{");
            bool first = true;
            foreach (var kvp in profile.WalletBalances)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(Quote(kvp.Key)).Append(':').Append(kvp.Value);
            }
            sb.Append("},");

            // Achievements
            sb.Append("\"achievements\":[");
            for (int i = 0; i < profile.Achievements.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var a = profile.Achievements[i];
                sb.Append('{');
                sb.Append("\"id\":").Append(Quote(a.Id)).Append(',');
                sb.Append("\"currentProgress\":").Append(a.CurrentProgress).Append(',');
                sb.Append("\"unlockedAtTicks\":").Append(a.UnlockedAtTicks.HasValue ? a.UnlockedAtTicks.Value.ToString(CultureInfo.InvariantCulture) : "null");
                sb.Append('}');
            }
            sb.Append("],");

            // CustomData
            sb.Append("\"customData\":{");
            first = true;
            foreach (var kvp in profile.CustomData)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(Quote(kvp.Key)).Append(':').Append(Quote(kvp.Value ?? string.Empty));
            }
            sb.Append('}');

            sb.Append('}');
            return sb.ToString();
        }

        public static CartridgeProfileData Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new CartridgeProfileData();

            int index = 0;
            var root = ParseValue(json, ref index) as Dictionary<string, object?>;
            var data = new CartridgeProfileData();
            if (root == null) return data;

            if (TryGetLong(root, "version", out long version)) data.Version = (int)version;
            if (TryGetLong(root, "highScore", out long highScore)) data.HighScore = (int)highScore;
            if (TryGetLong(root, "level", out long level)) data.Level = (int)level;
            if (TryGetLong(root, "currentXp", out long xp)) data.CurrentXp = (int)xp;

            if (root.TryGetValue("leaderboard", out var lbObj) && lbObj is List<object?> lbList)
            {
                foreach (var item in lbList)
                {
                    if (item is Dictionary<string, object?> entryObj)
                    {
                        var snap = new LeaderboardEntrySnapshot
                        {
                            PlayerName = TryGetString(entryObj, "playerName") ?? "AAA",
                            Score = TryGetLong(entryObj, "score", out long s) ? (int)s : 0,
                            AchievedAtTicks = TryGetLong(entryObj, "achievedAtTicks", out long t) ? t : 0L
                        };
                        data.Leaderboard.Add(snap);
                    }
                }
            }

            if (root.TryGetValue("walletBalances", out var walletObj) && walletObj is Dictionary<string, object?> walletDict)
            {
                foreach (var kvp in walletDict)
                {
                    if (kvp.Value is long val)
                    {
                        data.WalletBalances[kvp.Key] = (int)val;
                    }
                }
            }

            if (root.TryGetValue("achievements", out var achObj) && achObj is List<object?> achList)
            {
                foreach (var item in achList)
                {
                    if (item is Dictionary<string, object?> aObj)
                    {
                        var snap = new AchievementSnapshot
                        {
                            Id = TryGetString(aObj, "id") ?? string.Empty,
                            CurrentProgress = TryGetLong(aObj, "currentProgress", out long p) ? (int)p : 0,
                            UnlockedAtTicks = TryGetLong(aObj, "unlockedAtTicks", out long ut) ? ut : (long?)null
                        };
                        data.Achievements.Add(snap);
                    }
                }
            }

            if (root.TryGetValue("customData", out var customObj) && customObj is Dictionary<string, object?> customDict)
            {
                foreach (var kvp in customDict)
                {
                    if (kvp.Value is string strVal)
                    {
                        data.CustomData[kvp.Key] = strVal;
                    }
                }
            }

            return data;
        }

        private static bool TryGetLong(Dictionary<string, object?> dict, string key, out long result)
        {
            if (dict.TryGetValue(key, out var val) && val is long l)
            {
                result = l;
                return true;
            }
            result = 0;
            return false;
        }

        private static string? TryGetString(Dictionary<string, object?> dict, string key)
        {
            return dict.TryGetValue(key, out var val) ? val as string : null;
        }

        private static string Quote(string value)
        {
            if (value == null) return "\"\"";
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static object? ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) return null;

            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't' || c == 'f') return ParseBoolean(s, ref i);
            if (c == 'n') { i += 4; return null; }
            return ParseNumber(s, ref i);
        }

        private static Dictionary<string, object?> ParseObject(string s, ref int i)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            i++; // '{'
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return dict; }

            while (i < s.Length)
            {
                SkipWhitespace(s, ref i);
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                object? val = ParseValue(s, ref i);
                dict[key] = val;

                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; break; }
                break;
            }
            return dict;
        }

        private static List<object?> ParseArray(string s, ref int i)
        {
            var list = new List<object?>();
            i++; // '['
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }

            while (i < s.Length)
            {
                list.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; break; }
                break;
            }
            return list;
        }

        private static string ParseString(string s, ref int i)
        {
            var sb = new StringBuilder();
            if (s[i] == '"') i++;
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\' && i < s.Length)
                {
                    char esc = s[i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 <= s.Length &&
                                int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex))
                            {
                                sb.Append((char)hex);
                                i += 4;
                            }
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static bool ParseBoolean(string s, ref int i)
        {
            if (s[i] == 't') { i += 4; return true; }
            i += 5;
            return false;
        }

        private static long ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
            string token = s.Substring(start, i - start);
            if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                return l;
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                return (long)d;
            return 0L;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }
    }
}
