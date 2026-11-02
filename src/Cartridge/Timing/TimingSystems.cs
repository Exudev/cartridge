using System;
using System.Collections.Generic;

namespace Cartridge.Timing
{
    public interface ITimer
    {
        bool IsRunning { get; }
        void Tick(float deltaTime);
    }

    /// <summary>
    /// Counts down from a duration to zero and optionally loops.
    /// </summary>
    public sealed class CountdownTimer : ITimer
    {
        public float Duration { get; private set; }
        public float TimeRemaining { get; private set; }
        public bool IsRunning { get; private set; }
        public bool Loop { get; set; }

        public float NormalizedProgress =>
            Duration > 0f ? Math.Clamp(1f - (TimeRemaining / Duration), 0f, 1f) : 1f;

        public event Action? OnCompleted;

        public CountdownTimer(float durationSeconds, bool loop = false, bool autoStart = true)
        {
            Duration = Math.Max(0f, durationSeconds);
            TimeRemaining = Duration;
            Loop = loop;
            IsRunning = autoStart && Duration > 0f;
        }

        public void Start() => IsRunning = true;
        public void Pause() => IsRunning = false;

        public void Reset(float? newDuration = null, bool autoStart = true)
        {
            if (newDuration.HasValue)
            {
                Duration = Math.Max(0f, newDuration.Value);
            }
            TimeRemaining = Duration;
            IsRunning = autoStart && Duration > 0f;
        }

        public void Tick(float deltaTime)
        {
            if (!IsRunning || deltaTime <= 0f) return;

            TimeRemaining -= deltaTime;
            if (TimeRemaining <= 0f)
            {
                OnCompleted?.Invoke();
                if (Loop && Duration > 0f)
                {
                    TimeRemaining += Duration;
                }
                else
                {
                    TimeRemaining = 0f;
                    IsRunning = false;
                }
            }
        }
    }

    /// <summary>
    /// Tracks ability or action cooldowns.
    /// </summary>
    public sealed class Cooldown : ITimer
    {
        public float CooldownSeconds { get; set; }
        public float TimeRemaining { get; private set; }
        public bool IsReady => TimeRemaining <= 0f;
        public bool IsRunning => TimeRemaining > 0f;

        public Cooldown(float cooldownSeconds)
        {
            CooldownSeconds = Math.Max(0f, cooldownSeconds);
        }

        /// <summary>
        /// Attempts to trigger the action if off cooldown. Returns true and starts the cooldown if ready.
        /// </summary>
        public bool TryUse()
        {
            if (!IsReady) return false;
            TimeRemaining = CooldownSeconds;
            return true;
        }

        public void Reset() => TimeRemaining = 0f;

        public void Tick(float deltaTime)
        {
            if (TimeRemaining <= 0f || deltaTime <= 0f) return;
            TimeRemaining = Math.Max(0f, TimeRemaining - deltaTime);
        }
    }

    /// <summary>
    /// Central manager to tick multiple timers and cooldowns at once.
    /// </summary>
    public sealed class TimerManager
    {
        private readonly List<ITimer> _timers = new List<ITimer>();

        public T Register<T>(T timer) where T : ITimer
        {
            if (timer == null) throw new ArgumentNullException(nameof(timer));
            if (!_timers.Contains(timer))
            {
                _timers.Add(timer);
            }
            return timer;
        }

        public bool Unregister(ITimer timer) => _timers.Remove(timer);

        public void Tick(float deltaTime)
        {
            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                _timers[i].Tick(deltaTime);
            }
        }
    }
}
