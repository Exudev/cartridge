using System;
using System.Collections.Generic;

namespace Cartridge.Scoring
{
    public readonly struct LeaderboardEntry : IComparable<LeaderboardEntry>
    {
        public string PlayerName { get; }
        public int Score { get; }
        public DateTime AchievedAt { get; }

        public LeaderboardEntry(string playerName, int score, DateTime achievedAt)
        {
            PlayerName = string.IsNullOrWhiteSpace(playerName) ? "AAA" : playerName;
            Score = score;
            AchievedAt = achievedAt;
        }

        public int CompareTo(LeaderboardEntry other)
        {
            int scoreCompare = other.Score.CompareTo(Score); // Descending by score
            return scoreCompare != 0 ? scoreCompare : AchievedAt.CompareTo(other.AchievedAt);
        }
    }

    /// <summary>
    /// Maintains a sorted top-N leaderboard.
    /// </summary>
    public sealed class Leaderboard
    {
        private readonly List<LeaderboardEntry> _entries = new List<LeaderboardEntry>();

        public int MaxEntries { get; }
        public IReadOnlyList<LeaderboardEntry> Entries => _entries;

        public Leaderboard(int maxEntries = 10, IEnumerable<LeaderboardEntry>? initialEntries = null)
        {
            if (maxEntries <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEntries));
            }

            MaxEntries = maxEntries;
            if (initialEntries != null)
            {
                _entries.AddRange(initialEntries);
                SortAndTrim();
            }
        }

        public bool Qualifies(int score)
        {
            if (_entries.Count < MaxEntries) return true;
            return score > _entries[_entries.Count - 1].Score;
        }

        /// <summary>
        /// Submits a score and returns its 1-based rank, or -1 if it did not make the leaderboard.
        /// </summary>
        public int TrySubmit(string playerName, int score)
        {
            var entry = new LeaderboardEntry(playerName, score, DateTime.UtcNow);
            _entries.Add(entry);
            _entries.Sort();

            int index = _entries.IndexOf(entry);
            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
            }

            return index >= 0 && index < MaxEntries ? index + 1 : -1;
        }

        public void Clear() => _entries.Clear();

        public void ReplaceEntries(IEnumerable<LeaderboardEntry> entries)
        {
            _entries.Clear();
            if (entries != null)
            {
                _entries.AddRange(entries);
                SortAndTrim();
            }
        }

        private void SortAndTrim()
        {
            _entries.Sort();
            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
            }
        }
    }
}
