using System;
using System.Collections.Generic;

namespace Cartridge.Progression
{
    /// <summary>
    /// Tracks XP and levels using a customizable XP curve function (level -> XP required for next level).
    /// </summary>
    public sealed class ExperienceSystem
    {
        private readonly Func<int, int> _xpToNextLevelCurve;

        public int CurrentLevel { get; private set; }
        public int CurrentXp { get; private set; }
        public int MaxLevel { get; }
        public bool IsMaxLevel => CurrentLevel >= MaxLevel;

        public int XpRequiredForNextLevel => IsMaxLevel ? 0 : Math.Max(1, _xpToNextLevelCurve(CurrentLevel));

        public float NormalizedProgress =>
            IsMaxLevel ? 1f : Math.Clamp((float)CurrentXp / XpRequiredForNextLevel, 0f, 1f);

        public event Action<int, int>? OnXpGained; // (amountAdded, currentXp)
        public event Action<int>? OnLevelUp;       // (newLevel)

        public ExperienceSystem(
            int startingLevel = 1,
            int maxLevel = 100,
            Func<int, int>? xpToNextLevelCurve = null)
        {
            if (startingLevel < 1) throw new ArgumentOutOfRangeException(nameof(startingLevel));
            if (maxLevel < startingLevel) throw new ArgumentOutOfRangeException(nameof(maxLevel));

            CurrentLevel = startingLevel;
            MaxLevel = maxLevel;
            // Default curve: 100 * level^1.5
            _xpToNextLevelCurve = xpToNextLevelCurve ?? (level => (int)Math.Round(100 * Math.Pow(level, 1.5)));
        }

        public int AddXp(int amount)
        {
            if (amount <= 0 || IsMaxLevel) return 0;

            CurrentXp += amount;
            OnXpGained?.Invoke(amount, CurrentXp);

            int levelsGained = 0;
            while (!IsMaxLevel && CurrentXp >= XpRequiredForNextLevel)
            {
                CurrentXp -= XpRequiredForNextLevel;
                CurrentLevel++;
                levelsGained++;
                OnLevelUp?.Invoke(CurrentLevel);
            }

            if (IsMaxLevel)
            {
                CurrentXp = 0;
            }

            return levelsGained;
        }

        public void RestoreState(int level, int currentXp)
        {
            CurrentLevel = Math.Clamp(level, 1, MaxLevel);
            CurrentXp = IsMaxLevel ? 0 : Math.Max(0, currentXp);
        }
    }

    /// <summary>
    /// Multi-currency wallet supporting safe deposits, withdrawals, and balance change notifications.
    /// </summary>
    public sealed class Wallet
    {
        private readonly Dictionary<string, int> _balances = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public event Action<string, int, int>? OnBalanceChanged; // (currencyId, oldBalance, newBalance)

        public IReadOnlyDictionary<string, int> Balances => _balances;

        public void RestoreBalances(IEnumerable<KeyValuePair<string, int>> balances)
        {
            _balances.Clear();
            if (balances == null) return;
            foreach (var kvp in balances)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Key) && kvp.Value >= 0)
                {
                    _balances[kvp.Key] = kvp.Value;
                }
            }
        }

        public int GetBalance(string currencyId)
        {
            if (string.IsNullOrWhiteSpace(currencyId)) return 0;
            return _balances.TryGetValue(currencyId, out int balance) ? balance : 0;
        }

        public bool CanAfford(string currencyId, int cost)
        {
            if (cost < 0) return false;
            return GetBalance(currencyId) >= cost;
        }

        public int Deposit(string currencyId, int amount)
        {
            if (string.IsNullOrWhiteSpace(currencyId)) throw new ArgumentException("Currency ID required.", nameof(currencyId));
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));

            int oldBalance = GetBalance(currencyId);
            int newBalance = oldBalance + amount;
            _balances[currencyId] = newBalance;

            OnBalanceChanged?.Invoke(currencyId, oldBalance, newBalance);
            return newBalance;
        }

        public bool TrySpend(string currencyId, int cost)
        {
            if (cost < 0 || !CanAfford(currencyId, cost)) return false;
            if (cost == 0) return true;

            int oldBalance = GetBalance(currencyId);
            int newBalance = oldBalance - cost;
            _balances[currencyId] = newBalance;

            OnBalanceChanged?.Invoke(currencyId, oldBalance, newBalance);
            return true;
        }
    }

    public sealed class Achievement
    {
        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public int TargetProgress { get; }
        public int CurrentProgress { get; internal set; }
        public bool IsUnlocked => CurrentProgress >= TargetProgress;
        public DateTime? UnlockedAt { get; internal set; }

        public Achievement(string id, string title, string description, int targetProgress = 1)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Title = title ?? id;
            Description = description ?? string.Empty;
            TargetProgress = Math.Max(1, targetProgress);
        }
    }

    /// <summary>
    /// Tracks incremental and instant achievements.
    /// </summary>
    public sealed class AchievementTracker
    {
        private readonly Dictionary<string, Achievement> _achievements = new Dictionary<string, Achievement>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, Achievement> Achievements => _achievements;

        public event Action<Achievement>? OnAchievementUnlocked;
        public event Action<Achievement>? OnProgressUpdated;

        public void Register(Achievement achievement)
        {
            if (achievement == null) throw new ArgumentNullException(nameof(achievement));
            _achievements[achievement.Id] = achievement;
        }

        public bool AddProgress(string id, int amount = 1)
        {
            if (amount <= 0 || !_achievements.TryGetValue(id, out var achievement) || achievement.IsUnlocked)
            {
                return false;
            }

            achievement.CurrentProgress = Math.Min(achievement.TargetProgress, achievement.CurrentProgress + amount);
            OnProgressUpdated?.Invoke(achievement);

            if (achievement.IsUnlocked)
            {
                achievement.UnlockedAt = DateTime.UtcNow;
                OnAchievementUnlocked?.Invoke(achievement);
                return true;
            }

            return false;
        }

        public bool Unlock(string id)
        {
            if (!_achievements.TryGetValue(id, out var achievement) || achievement.IsUnlocked)
            {
                return false;
            }

            return AddProgress(id, achievement.TargetProgress - achievement.CurrentProgress);
        }

        public void RestoreProgress(string id, int progress, DateTime? unlockedAt)
        {
            if (_achievements.TryGetValue(id, out var achievement))
            {
                achievement.CurrentProgress = Math.Clamp(progress, 0, achievement.TargetProgress);
                achievement.UnlockedAt = achievement.IsUnlocked ? (unlockedAt ?? DateTime.UtcNow) : null;
            }
        }
    }
}
