using System;
using System.Collections.Generic;

namespace Cartridge.Scoring
{
    /// <summary>
    /// Defines a combo threshold and the multiplier granted when reaching that combo count.
    /// </summary>
    public readonly struct ComboTier
    {
        public int MinHits { get; }
        public float Multiplier { get; }

        public ComboTier(int minHits, float multiplier)
        {
            MinHits = minHits;
            Multiplier = multiplier;
        }
    }

    /// <summary>
    /// Tracks timed combo chains and computes multiplier tiers.
    /// Call Tick(deltaTime) every frame from your engine loop.
    /// </summary>
    public sealed class ComboTracker
    {
        private readonly List<ComboTier> _tiers = new List<ComboTier>();
        private readonly float _decayTimeSeconds;

        public int CurrentCombo { get; private set; }
        public int MaxComboReached { get; private set; }
        public float TimeRemaining { get; private set; }
        public float CurrentMultiplier { get; private set; } = 1f;

        public float NormalizedTimeRemaining =>
            _decayTimeSeconds > 0f && CurrentCombo > 0
                ? Math.Clamp(TimeRemaining / _decayTimeSeconds, 0f, 1f)
                : 0f;

        public event Action<int, float>? OnComboChanged;
        public event Action<int>? OnComboBroken;

        public ComboTracker(float decayTimeSeconds = 3.0f, IEnumerable<ComboTier>? tiers = null)
        {
            if (decayTimeSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(decayTimeSeconds), "Decay time must be greater than zero.");
            }

            _decayTimeSeconds = decayTimeSeconds;

            if (tiers != null)
            {
                _tiers.AddRange(tiers);
                _tiers.Sort((a, b) => a.MinHits.CompareTo(b.MinHits));
            }
            else
            {
                // Default arcade tiers: 5 hits -> 2x, 15 hits -> 3x, 30 hits -> 4x
                _tiers.Add(new ComboTier(5, 2f));
                _tiers.Add(new ComboTier(15, 3f));
                _tiers.Add(new ComboTier(30, 4f));
            }
        }

        public void RegisterHit(int hits = 1)
        {
            if (hits <= 0) return;

            CurrentCombo += hits;
            if (CurrentCombo > MaxComboReached)
            {
                MaxComboReached = CurrentCombo;
            }

            TimeRemaining = _decayTimeSeconds;
            CurrentMultiplier = ResolveMultiplier(CurrentCombo);
            OnComboChanged?.Invoke(CurrentCombo, CurrentMultiplier);
        }

        public void Tick(float deltaTime)
        {
            if (CurrentCombo <= 0 || deltaTime <= 0f) return;

            TimeRemaining -= deltaTime;
            if (TimeRemaining <= 0f)
            {
                BreakCombo();
            }
        }

        public void BreakCombo()
        {
            if (CurrentCombo <= 0) return;

            int lostCombo = CurrentCombo;
            CurrentCombo = 0;
            TimeRemaining = 0f;
            CurrentMultiplier = 1f;

            OnComboBroken?.Invoke(lostCombo);
            OnComboChanged?.Invoke(CurrentCombo, CurrentMultiplier);
        }

        public void Reset()
        {
            CurrentCombo = 0;
            MaxComboReached = 0;
            TimeRemaining = 0f;
            CurrentMultiplier = 1f;
            OnComboChanged?.Invoke(CurrentCombo, CurrentMultiplier);
        }

        private float ResolveMultiplier(int combo)
        {
            float mult = 1f;
            for (int i = 0; i < _tiers.Count; i++)
            {
                if (combo >= _tiers[i].MinHits)
                {
                    mult = _tiers[i].Multiplier;
                }
                else
                {
                    break;
                }
            }
            return mult;
        }
    }
}
