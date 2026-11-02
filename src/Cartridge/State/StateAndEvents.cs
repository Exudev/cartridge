using System;
using System.Collections.Generic;

namespace Cartridge.State
{
    public interface IState
    {
        void OnEnter();
        void OnTick(float deltaTime);
        void OnExit();
    }

    /// <summary>
    /// Delegate-friendly state implementation so users don't have to create a subclass per state if they don't want to.
    /// </summary>
    public sealed class ActionState : IState
    {
        private readonly Action? _onEnter;
        private readonly Action<float>? _onTick;
        private readonly Action? _onExit;

        public ActionState(Action? onEnter = null, Action<float>? onTick = null, Action? onExit = null)
        {
            _onEnter = onEnter;
            _onTick = onTick;
            _onExit = onExit;
        }

        public void OnEnter() => _onEnter?.Invoke();
        public void OnTick(float deltaTime) => _onTick?.Invoke(deltaTime);
        public void OnExit() => _onExit?.Invoke();
    }

    /// <summary>
    /// Generic finite state machine keyed by an enum or custom identifier.
    /// </summary>
    public sealed class StateMachine<TStateId> where TStateId : notnull
    {
        private readonly Dictionary<TStateId, IState> _states = new Dictionary<TStateId, IState>();
        private IState? _currentState;

        public TStateId? CurrentStateId { get; private set; }
        public bool HasCurrentState => _currentState != null;

        public event Action<TStateId?, TStateId>? OnStateChanged;

        public StateMachine<TStateId> AddState(TStateId id, IState state)
        {
            _states[id] = state ?? throw new ArgumentNullException(nameof(state));
            return this;
        }

        public StateMachine<TStateId> AddState(
            TStateId id,
            Action? onEnter = null,
            Action<float>? onTick = null,
            Action? onExit = null)
        {
            return AddState(id, new ActionState(onEnter, onTick, onExit));
        }

        public bool ChangeState(TStateId nextStateId)
        {
            if (!_states.TryGetValue(nextStateId, out var nextState))
            {
                throw new KeyNotFoundException($"State '{nextStateId}' is not registered.");
            }

            if (_currentState != null && EqualityComparer<TStateId>.Default.Equals(CurrentStateId!, nextStateId))
            {
                return false;
            }

            TStateId? previousId = CurrentStateId;
            _currentState?.OnExit();

            CurrentStateId = nextStateId;
            _currentState = nextState;
            _currentState.OnEnter();

            OnStateChanged?.Invoke(previousId, nextStateId);
            return true;
        }

        public void Tick(float deltaTime)
        {
            _currentState?.OnTick(deltaTime);
        }
    }

    /// <summary>
    /// Lightweight, zero-dependency publish/subscribe event bus for decoupling game systems.
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _subscribers = new Dictionary<Type, List<Delegate>>();

        public void Subscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var type = typeof(TEvent);
            if (!_subscribers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                _subscribers[type] = list;
            }
            list.Add(handler);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler == null) return;
            var type = typeof(TEvent);
            if (_subscribers.TryGetValue(type, out var list))
            {
                list.Remove(handler);
            }
        }

        public void Publish<TEvent>(TEvent eventData)
        {
            var type = typeof(TEvent);
            if (!_subscribers.TryGetValue(type, out var list) || list.Count == 0) return;

            // Snapshot to allow safe subscribe/unsubscribe during callbacks
            var snapshot = list.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                ((Action<TEvent>)snapshot[i]).Invoke(eventData);
            }
        }

        public void Clear() => _subscribers.Clear();
    }
}
