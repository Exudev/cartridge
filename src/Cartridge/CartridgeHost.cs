using System;
using System.Collections.Generic;
using Cartridge.Persistence;
using Cartridge.Progression;
using Cartridge.Random;
using Cartridge.Scoring;
using Cartridge.State;
using Cartridge.Timing;

namespace Cartridge
{
    /// <summary>
    /// Central coordinator ("console") that owns and ticks Cartridge core systems with built-in Pause, TimeScale,
    /// UnscaledTimers, and one-line Profile Save/Load serialization.
    /// </summary>
    public sealed class CartridgeHost
    {
        private float _timeScale = 1f;
        private readonly List<Action<float>> _customTickables = new List<Action<float>>();
        private readonly Dictionary<string, string> _customData = new Dictionary<string, string>(StringComparer.Ordinal);

        public ComboTracker Combo { get; }
        public ScoreManager Score { get; }
        public Leaderboard Leaderboard { get; }
        public ExperienceSystem Experience { get; }
        public Wallet Wallet { get; }
        public AchievementTracker Achievements { get; }
        public TimerManager Timers { get; }
        public TimerManager UnscaledTimers { get; }
        public EventBus Events { get; }
        public SeededRng Rng { get; set; }
        public ISaveStorage Storage { get; set; }

        /// <summary>
        /// Elapsed scaled gameplay time in the current round/session.
        /// </summary>
        public float ElapsedRoundTime { get; private set; }

        /// <summary>
        /// When true, scaled systems (Combo decay, Score buffs, Timers, custom tickables) freeze while UnscaledTimers continue ticking.
        /// </summary>
        public bool IsPaused { get; set; }

        /// <summary>
        /// Time multiplier applied to scaled deltaTime (e.g., 0.5f for slow-motion, 2.0f for fast-forward).
        /// </summary>
        public float TimeScale
        {
            get => _timeScale;
            set => _timeScale = Math.Max(0f, value);
        }

        public CartridgeHost(
            ISaveStorage? storage = null,
            ulong rngSeed = 0,
            float comboDecaySeconds = 3.0f,
            int maxLeaderboardEntries = 10)
        {
            Storage = storage ?? new InMemorySaveStorage();
            Rng = new SeededRng(rngSeed);
            Combo = new ComboTracker(comboDecaySeconds);
            Score = new ScoreManager(0, Combo);
            Leaderboard = new Leaderboard(maxLeaderboardEntries);
            Experience = new ExperienceSystem();
            Wallet = new Wallet();
            Achievements = new AchievementTracker();
            Timers = new TimerManager();
            UnscaledTimers = new TimerManager();
            Events = new EventBus();
        }

        /// <summary>
        /// Registers a StateMachine or custom callback to be ticked automatically by CartridgeHost.Tick().
        /// </summary>
        public void RegisterStateMachine<TStateId>(StateMachine<TStateId> stateMachine) where TStateId : notnull
        {
            if (stateMachine == null) throw new ArgumentNullException(nameof(stateMachine));
            _customTickables.Add(stateMachine.Tick);
        }

        /// <summary>
        /// Single heartbeat method to call from Unity's Update(Time.deltaTime) or Godot's _Process(delta).
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            // Unscaled timers always run (even when paused)
            UnscaledTimers.Tick(deltaTime);

            if (IsPaused || _timeScale <= 0f) return;

            float scaledDelta = deltaTime * _timeScale;
            ElapsedRoundTime += scaledDelta;

            Combo.Tick(scaledDelta);
            Score.Tick(scaledDelta);
            Timers.Tick(scaledDelta);

            for (int i = 0; i < _customTickables.Count; i++)
            {
                _customTickables[i].Invoke(scaledDelta);
            }
        }

        /// <summary>
        /// Resets round-specific state (CurrentScore, Combo, ElapsedRoundTime) while preserving persistent profile data.
        /// </summary>
        public void ResetRound()
        {
            ElapsedRoundTime = 0f;
            Score.ResetRound();
        }

        /// <summary>
        ///Builds an end-of-round summary using the host's tracked ElapsedRoundTime.
        /// </summary>
        public RoundSummary BuildRoundSummary()
        {
            return Score.BuildRoundSummary(ElapsedRoundTime);
        }

        public void SetCustomData(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key required.", nameof(key));
            _customData[key] = value ?? string.Empty;
        }

        public string? GetCustomData(string key)
        {
            return _customData.TryGetValue(key, out string? val) ? val : null;
        }

        /// <summary>
        /// Exports the entire persistent state of all modules into a serializable CartridgeProfileData snapshot.
        /// </summary>
        public CartridgeProfileData ExportProfile()
        {
            var profile = new CartridgeProfileData
            {
                HighScore = Score.HighScore,
                Level = Experience.CurrentLevel,
                CurrentXp = Experience.CurrentXp
            };

            for (int i = 0; i < Leaderboard.Entries.Count; i++)
            {
                var entry = Leaderboard.Entries[i];
                profile.Leaderboard.Add(new LeaderboardEntrySnapshot
                {
                    PlayerName = entry.PlayerName,
                    Score = entry.Score,
                    AchievedAtTicks = entry.AchievedAt.Ticks
                });
            }

            foreach (var kvp in Wallet.Balances)
            {
                profile.WalletBalances[kvp.Key] = kvp.Value;
            }

            foreach (var kvp in Achievements.Achievements)
            {
                var ach = kvp.Value;
                profile.Achievements.Add(new AchievementSnapshot
                {
                    Id = ach.Id,
                    CurrentProgress = ach.CurrentProgress,
                    UnlockedAtTicks = ach.UnlockedAt?.Ticks
                });
            }

            foreach (var kvp in _customData)
            {
                profile.CustomData[kvp.Key] = kvp.Value;
            }

            return profile;
        }

        /// <summary>
        /// Restores persistent state across all modules from a CartridgeProfileData snapshot.
        /// </summary>
        public void ImportProfile(CartridgeProfileData profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            Score.RestoreHighScore(profile.HighScore);
            Experience.RestoreState(profile.Level, profile.CurrentXp);
            Wallet.RestoreBalances(profile.WalletBalances);

            var entries = new List<LeaderboardEntry>(profile.Leaderboard.Count);
            for (int i = 0; i < profile.Leaderboard.Count; i++)
            {
                var snap = profile.Leaderboard[i];
                var timestamp = snap.AchievedAtTicks > 0
                    ? new DateTime(snap.AchievedAtTicks, DateTimeKind.Utc)
                    : DateTime.UtcNow;
                entries.Add(new LeaderboardEntry(snap.PlayerName, snap.Score, timestamp));
            }
            Leaderboard.ReplaceEntries(entries);

            for (int i = 0; i < profile.Achievements.Count; i++)
            {
                var achSnap = profile.Achievements[i];
                DateTime? unlockedAt = achSnap.UnlockedAtTicks.HasValue
                    ? new DateTime(achSnap.UnlockedAtTicks.Value, DateTimeKind.Utc)
                    : (DateTime?)null;
                Achievements.RestoreProgress(achSnap.Id, achSnap.CurrentProgress, unlockedAt);
            }

            _customData.Clear();
            foreach (var kvp in profile.CustomData)
            {
                _customData[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Serializes all persistent modules to JSON and writes to Storage under the given slot key.
        /// </summary>
        public string SaveProfile(string slotKey = "default_profile")
        {
            var snapshot = ExportProfile();
            string json = CartridgeProfileJson.Serialize(snapshot);
            Storage.WriteString(slotKey, json);
            return json;
        }

        /// <summary>
        /// Loads and restores all persistent modules from Storage for the given slot key. Returns false if no save exists.
        /// </summary>
        public bool LoadProfile(string slotKey = "default_profile")
        {
            if (!Storage.TryReadString(slotKey, out string json) || string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var snapshot = CartridgeProfileJson.Deserialize(json);
            ImportProfile(snapshot);
            return true;
        }
    }
}
