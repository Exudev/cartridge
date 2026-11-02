using System;
using System.Collections.Generic;

namespace Cartridge.Scoring
{
    /// <summary>
    /// Defines a rank grade (e.g., "S", "A", "B", "C") and the requirements to achieve it.
    /// </summary>
    public readonly struct RankRequirement
    {
        public string Grade { get; }
        public int MinScore { get; }
        public int MinMaxCombo { get; }
        public float? MaxTimeSeconds { get; }

        public RankRequirement(string grade, int minScore, int minMaxCombo = 0, float? maxTimeSeconds = null)
        {
            Grade = grade ?? throw new ArgumentNullException(nameof(grade));
            MinScore = minScore;
            MinMaxCombo = minMaxCombo;
            MaxTimeSeconds = maxTimeSeconds;
        }

        public bool IsMet(int score, int maxCombo, float elapsedSeconds)
        {
            if (score < MinScore) return false;
            if (maxCombo < MinMaxCombo) return false;
            if (MaxTimeSeconds.HasValue && elapsedSeconds > 0f && elapsedSeconds > MaxTimeSeconds.Value)
                return false;
            return true;
        }
    }

    /// <summary>
    /// Evaluates end-of-round performance against ordered rank requirements (checked in order from highest to lowest).
    /// </summary>
    public sealed class RankEvaluator
    {
        private readonly List<RankRequirement> _requirements = new List<RankRequirement>();

        public string FallbackGrade { get; set; } = "D";
        public IReadOnlyList<RankRequirement> Requirements => _requirements;

        public RankEvaluator(string fallbackGrade = "D")
        {
            FallbackGrade = fallbackGrade;
        }

        public RankEvaluator AddTier(string grade, int minScore, int minMaxCombo = 0, float? maxTimeSeconds = null)
        {
            _requirements.Add(new RankRequirement(grade, minScore, minMaxCombo, maxTimeSeconds));
            return this;
        }

        public string Evaluate(int score, int maxCombo = 0, float elapsedSeconds = 0f)
        {
            for (int i = 0; i < _requirements.Count; i++)
            {
                if (_requirements[i].IsMet(score, maxCombo, elapsedSeconds))
                {
                    return _requirements[i].Grade;
                }
            }
            return FallbackGrade;
        }

        public static RankEvaluator CreateDefault()
        {
            return new RankEvaluator("D")
                .AddTier("S", minScore: 10000, minMaxCombo: 20)
                .AddTier("A", minScore: 5000, minMaxCombo: 10)
                .AddTier("B", minScore: 2500)
                .AddTier("C", minScore: 1000);
        }
    }

    /// <summary>
    /// End-of-round summary containing score totals, combo stats, points breakdown by reason, and rank grade.
    /// </summary>
    public sealed class RoundSummary
    {
        public int TotalScore { get; }
        public int BasePointsEarned { get; }
        public int BonusPointsFromMultipliers { get; }
        public int MaxCombo { get; }
        public int TotalScoringEvents { get; }
        public float ElapsedSeconds { get; }
        public string Grade { get; }
        public IReadOnlyDictionary<string, int> PointsByReason { get; }

        public RoundSummary(
            int totalScore,
            int basePointsEarned,
            int bonusPointsFromMultipliers,
            int maxCombo,
            int totalScoringEvents,
            float elapsedSeconds,
            string grade,
            IReadOnlyDictionary<string, int> pointsByReason)
        {
            TotalScore = totalScore;
            BasePointsEarned = basePointsEarned;
            BonusPointsFromMultipliers = bonusPointsFromMultipliers;
            MaxCombo = maxCombo;
            TotalScoringEvents = totalScoringEvents;
            ElapsedSeconds = elapsedSeconds;
            Grade = grade ?? "D";
            PointsByReason = pointsByReason ?? new Dictionary<string, int>();
        }
    }
}
