using System;
using System.Collections.Generic;
using Cartridge.Timing;

namespace Cartridge.Scoring
{
    /// <summary>
    /// Determines how multiple active temporary score buffs combine.
    /// </summary>
    public enum BuffStackMode
    {
        /// <summary>
        /// Multiplies active buffs together (e.g., 2.0x and 1.5x -> 3.0x).
        /// </summary>
        Multiplicative,

        /// <summary>
        /// Adds bonus multipliers above 1.0x together (e.g., 2.0x (+100%) and 1.5x (+50%) -> 2.5x).
        /// </summary>
        Additive
    }

    /// <summary>
    /// Represents a timed score multiplier buff (e.g., "Double Points Powerup for 10s").
    /// </summary>
    public sealed class ScoreBuff
    {
        public string Id { get; }
        public string Tag { get; }
        public float Multiplier { get; }
        public float Duration { get; }
        public float TimeRemaining { get; internal set; }

        public float NormalizedTimeRemaining =>
            Duration > 0f ? Math.Clamp(TimeRemaining / Duration, 0f, 1f) : 0f;

        public ScoreBuff(string id, string tag, float multiplier, float durationSeconds)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Tag = tag ?? string.Empty;
            Multiplier = Math.Max(0f, multiplier);
            Duration = Math.Max(0f, durationSeconds);
            TimeRemaining = Duration;
        }
    }

    /// <summary>
    /// Optional style/variety configuration to reward varied actions and penalize repetitive move spamming.
    /// </summary>
    public sealed class StyleSettings
    {
        /// <summary>
        /// Whether style variety / anti-spam calculation is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Number of recent scoring reasons to track in the sliding window.
        /// </summary>
        public int HistoryWindowSize { get; set; } = 5;

        /// <summary>
        /// Penalty subtracted from the style multiplier for each consecutive repetition of the exact same reason.
        /// For example, 0.2f reduces repeated moves to 0.8x -> 0.6x -> 0.4x down to MinStaleMultiplier.
        /// </summary>
        public float StaleMoveDecayPerRepeat { get; set; } = 0.2f;

        /// <summary>
        /// Minimum floor multiplier when spamming the same move repeatedly.
        /// </summary>
        public float MinStaleMultiplier { get; set; } = 0.25f;

        /// <summary>
        /// Bonus multiplier granted when the recent history window is full and all reasons in the window are unique.
        /// </summary>
        public float FullVarietyBonusMultiplier { get; set; } = 1.25f;
    }

    /// <summary>
    /// Represents a single score event with base points, applied multipliers, and reason/tag.
    /// </summary>
    public readonly struct ScoreEvent
    {
        public int BasePoints { get; }
        public float Multiplier { get; }
        public float StyleMultiplier { get; }
        public int FinalPoints { get; }
        public string Reason { get; }
        public DateTime Timestamp { get; }

        public ScoreEvent(
            int basePoints,
            float multiplier,
            float styleMultiplier,
            int finalPoints,
            string reason,
            DateTime timestamp)
        {
            BasePoints = basePoints;
            Multiplier = multiplier;
            StyleMultiplier = styleMultiplier;
            FinalPoints = finalPoints;
            Reason = reason ?? string.Empty;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Manages player score, high score, timed score buffs, style/variety modifiers, and round summaries.
    /// Implements ITimer so CartridgeHost can tick active score buffs automatically.
    /// </summary>
    public sealed class ScoreManager : ITimer
    {
        private readonly List<ScoreEvent> _history = new List<ScoreEvent>();
        private readonly List<ScoreBuff> _activeBuffs = new List<ScoreBuff>();
        private readonly List<string> _recentReasons = new List<string>();
        private float _baseMultiplier = 1f;
        private int _nextBuffId = 1;

        public int CurrentScore { get; private set; }
        public int HighScore { get; private set; }
        public int BasePointsEarned { get; private set; }
        public IReadOnlyList<ScoreEvent> History => _history;
        public IReadOnlyList<ScoreBuff> ActiveBuffs => _activeBuffs;

        public bool IsRunning => _activeBuffs.Count > 0;

        /// <summary>
        /// Optional linked ComboTracker whose multiplier is automatically factored into score additions.
        /// </summary>
        public ComboTracker? ComboTracker { get; set; }

        /// <summary>
        /// Controls whether multiple temporary buffs combine multiplicatively or additively.
        /// </summary>
        public BuffStackMode BuffStackMode { get; set; } = BuffStackMode.Multiplicative;

        /// <summary>
        /// Optional style / move-variety settings.
        /// </summary>
        public StyleSettings Style { get; } = new StyleSettings();

        /// <summary>
        /// Optional rank evaluator for end-of-round S/A/B/C/D grading.
        /// </summary>
        public RankEvaluator RankEvaluator { get; set; } = RankEvaluator.CreateDefault();

        public float BaseMultiplier
        {
            get => _baseMultiplier;
            set => _baseMultiplier = Math.Max(0f, value);
        }

        public float ActiveBuffMultiplier => ComputeBuffMultiplier();

        public float EffectiveMultiplier =>
            _baseMultiplier * (ComboTracker?.CurrentMultiplier ?? 1f) * ActiveBuffMultiplier;

        public event Action<ScoreEvent>? OnPointsAdded;
        public event Action<int>? OnScoreChanged;
        public event Action<int>? OnNewHighScore;
        public event Action<ScoreBuff>? OnBuffStarted;
        public event Action<ScoreBuff>? OnBuffExpired;

        public ScoreManager(int initialHighScore = 0, ComboTracker? comboTracker = null)
        {
            HighScore = Math.Max(0, initialHighScore);
            ComboTracker = comboTracker;
        }

        /// <summary>
        /// Adds a timed multiplier buff (e.g., 2x Double Points for 10 seconds).
        /// </summary>
        public ScoreBuff AddTemporaryMultiplier(float multiplier, float durationSeconds, string tag = "")
        {
            if (multiplier <= 0f)
                throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be positive.");
            if (durationSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Duration must be positive.");

            string id = "buff_" + (_nextBuffId++);
            var buff = new ScoreBuff(id, tag, multiplier, durationSeconds);
            _activeBuffs.Add(buff);
            OnBuffStarted?.Invoke(buff);
            return buff;
        }

        public bool RemoveBuff(string buffId)
        {
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].Id == buffId)
                {
                    var buff = _activeBuffs[i];
                    _activeBuffs.RemoveAt(i);
                    OnBuffExpired?.Invoke(buff);
                    return true;
                }
            }
            return false;
        }

        public void ClearBuffs()
        {
            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                var buff = _activeBuffs[i];
                _activeBuffs.RemoveAt(i);
                OnBuffExpired?.Invoke(buff);
            }
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || _activeBuffs.Count == 0) return;

            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                var buff = _activeBuffs[i];
                buff.TimeRemaining -= deltaTime;
                if (buff.TimeRemaining <= 0f)
                {
                    buff.TimeRemaining = 0f;
                    _activeBuffs.RemoveAt(i);
                    OnBuffExpired?.Invoke(buff);
                }
            }
        }

        /// <summary>
        /// Adds base points, optionally advancing the linked ComboTracker and applying effective + style multipliers.
        /// </summary>
        public ScoreEvent AddPoints(int basePoints, string reason = "", bool registerComboHit = true)
        {
            if (basePoints <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(basePoints), "Base points must be positive.");
            }

            if (registerComboHit && ComboTracker != null)
            {
                ComboTracker.RegisterHit();
            }

            float styleMultiplier = EvaluateAndRecordStyleMultiplier(reason);
            float totalMultiplier = EffectiveMultiplier * styleMultiplier;
            int finalPoints = (int)Math.Round(basePoints * totalMultiplier, MidpointRounding.AwayFromZero);

            BasePointsEarned += basePoints;
            CurrentScore += finalPoints;

            var scoreEvent = new ScoreEvent(
                basePoints,
                totalMultiplier,
                styleMultiplier,
                finalPoints,
                reason,
                DateTime.UtcNow);
            _history.Add(scoreEvent);

            OnPointsAdded?.Invoke(scoreEvent);
            OnScoreChanged?.Invoke(CurrentScore);

            if (CurrentScore > HighScore)
            {
                HighScore = CurrentScore;
                OnNewHighScore?.Invoke(HighScore);
            }

            return scoreEvent;
        }

        /// <summary>
        /// Deducts points from the current score (clamped at 0 by default).
        /// </summary>
        public void SubtractPoints(int points, bool clampToZero = true)
        {
            if (points <= 0) return;

            CurrentScore -= points;
            if (clampToZero && CurrentScore < 0)
            {
                CurrentScore = 0;
            }

            OnScoreChanged?.Invoke(CurrentScore);
        }

        /// <summary>
        /// Builds an itemized end-of-round summary with points breakdown by reason and S/A/B/C/D grade evaluation.
        /// </summary>
        public RoundSummary BuildRoundSummary(float elapsedSeconds = 0f)
        {
            var breakdown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _history.Count; i++)
            {
                var evt = _history[i];
                string key = string.IsNullOrWhiteSpace(evt.Reason) ? "Unclassified" : evt.Reason;
                breakdown[key] = breakdown.TryGetValue(key, out int existing)
                    ? existing + evt.FinalPoints
                    : evt.FinalPoints;
            }

            int maxCombo = ComboTracker?.MaxComboReached ?? 0;
            int bonusPoints = Math.Max(0, CurrentScore - BasePointsEarned);
            string grade = RankEvaluator != null
                ? RankEvaluator.Evaluate(CurrentScore, maxCombo, elapsedSeconds)
                : "N/A";

            return new RoundSummary(
                totalScore: CurrentScore,
                basePointsEarned: BasePointsEarned,
                bonusPointsFromMultipliers: bonusPoints,
                maxCombo: maxCombo,
                totalScoringEvents: _history.Count,
                elapsedSeconds: elapsedSeconds,
                grade: grade,
                pointsByReason: breakdown);
        }

        /// <summary>
        /// Restores high score from a saved profile snapshot.
        /// </summary>
        public void RestoreHighScore(int savedHighScore)
        {
            if (savedHighScore > HighScore)
            {
                HighScore = savedHighScore;
            }
        }

        /// <summary>
        /// Resets current score, history, active buffs, and style history for a new round while preserving HighScore.
        /// </summary>
        public void ResetRound()
        {
            CurrentScore = 0;
            BasePointsEarned = 0;
            _history.Clear();
            _recentReasons.Clear();
            ClearBuffs();
            ComboTracker?.Reset();
            OnScoreChanged?.Invoke(CurrentScore);
        }

        private float ComputeBuffMultiplier()
        {
            if (_activeBuffs.Count == 0) return 1f;

            if (BuffStackMode == BuffStackMode.Multiplicative)
            {
                float product = 1f;
                for (int i = 0; i < _activeBuffs.Count; i++)
                {
                    product *= _activeBuffs[i].Multiplier;
                }
                return product;
            }
            else
            {
                // Additive: 1.0 + sum of (multiplier - 1.0)
                float bonusSum = 0f;
                for (int i = 0; i < _activeBuffs.Count; i++)
                {
                    bonusSum += (_activeBuffs[i].Multiplier - 1f);
                }
                return Math.Max(0f, 1f + bonusSum);
            }
        }

        private float EvaluateAndRecordStyleMultiplier(string reason)
        {
            if (!Style.Enabled || string.IsNullOrWhiteSpace(reason))
            {
                return 1f;
            }

            // Count consecutive identical reasons at the tail of _recentReasons
            int consecutiveRepeats = 0;
            for (int i = _recentReasons.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_recentReasons[i], reason, StringComparison.OrdinalIgnoreCase))
                {
                    consecutiveRepeats++;
                }
                else
                {
                    break;
                }
            }

            // Record current reason into sliding window
            int windowSize = Math.Max(2, Style.HistoryWindowSize);
            _recentReasons.Add(reason);
            while (_recentReasons.Count > windowSize)
            {
                _recentReasons.RemoveAt(0);
            }

            if (consecutiveRepeats > 0)
            {
                float decayed = 1f - (consecutiveRepeats * Style.StaleMoveDecayPerRepeat);
                return Math.Max(Style.MinStaleMultiplier, decayed);
            }

            // Check if window is full and all entries are distinct
            if (_recentReasons.Count == windowSize)
            {
                var unique = new HashSet<string>(_recentReasons, StringComparer.OrdinalIgnoreCase);
                if (unique.Count == windowSize)
                {
                    return Style.FullVarietyBonusMultiplier;
                }
            }

            return 1f;
        }
    }
}
