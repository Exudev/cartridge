using System;
using System.Collections.Generic;

namespace Cartridge.Random
{
    /// <summary>
    /// Deterministic seeded random number generator (SplitMix64 / XorShift based) for repeatable gameplay and procedural generation.
    /// </summary>
    public sealed class SeededRng
    {
        private ulong _state;

        public ulong Seed { get; }

        public SeededRng(ulong seed)
        {
            Seed = seed == 0 ? 0xCAFEBABE12345678UL : seed;
            _state = Seed;
        }

        public ulong NextUInt64()
        {
            ulong z = (_state += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>
        /// Returns a random integer in [minInclusive, maxExclusive).
        /// </summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
                throw new ArgumentException("minInclusive must be less than maxExclusive.");

            ulong range = (ulong)((long)maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt64() % range);
        }

        /// <summary>
        /// Returns a float in [0.0, 1.0).
        /// </summary>
        public float NextFloat()
        {
            return (NextUInt64() >> 40) * (1.0f / (1UL << 24));
        }

        public bool Chance(float probability)
        {
            if (probability <= 0f) return false;
            if (probability >= 1f) return true;
            return NextFloat() < probability;
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }
    }

    /// <summary>
    /// Weighted loot table for item drops, enemy spawns, or gacha rolls.
    /// </summary>
    public sealed class LootTable<T>
    {
        private readonly struct Entry
        {
            public T Item { get; }
            public float Weight { get; }

            public Entry(T item, float weight)
            {
                Item = item;
                Weight = weight;
            }
        }

        private readonly List<Entry> _entries = new List<Entry>();
        public float TotalWeight { get; private set; }

        public LootTable<T> Add(T item, float weight)
        {
            if (weight <= 0f) throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be positive.");
            _entries.Add(new Entry(item, weight));
            TotalWeight += weight;
            return this;
        }

        public T Roll(SeededRng rng)
        {
            if (_entries.Count == 0) throw new InvalidOperationException("Cannot roll an empty LootTable.");

            float roll = rng.NextFloat() * TotalWeight;
            float cumulative = 0f;

            for (int i = 0; i < _entries.Count; i++)
            {
                cumulative += _entries[i].Weight;
                if (roll < cumulative)
                {
                    return _entries[i].Item;
                }
            }

            return _entries[_entries.Count - 1].Item;
        }
    }

    /// <summary>
    /// Shufflable deck of items (cards, tiles, tetromino bags) that automatically reshuffles the discard pile when empty.
    /// </summary>
    public sealed class Deck<T>
    {
        private readonly List<T> _drawPile = new List<T>();
        private readonly List<T> _discardPile = new List<T>();
        private readonly SeededRng _rng;

        public int RemainingCount => _drawPile.Count;
        public int DiscardCount => _discardPile.Count;

        public Deck(IEnumerable<T> items, SeededRng rng, bool shuffleImmediately = true)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _drawPile.AddRange(items ?? throw new ArgumentNullException(nameof(items)));
            if (shuffleImmediately)
            {
                Shuffle();
            }
        }

        public void Shuffle() => _rng.Shuffle(_drawPile);

        public T Draw()
        {
            if (_drawPile.Count == 0)
            {
                if (_discardPile.Count == 0)
                {
                    throw new InvalidOperationException("Deck and discard pile are both empty.");
                }
                _drawPile.AddRange(_discardPile);
                _discardPile.Clear();
                Shuffle();
            }

            int lastIndex = _drawPile.Count - 1;
            T item = _drawPile[lastIndex];
            _drawPile.RemoveAt(lastIndex);
            _discardPile.Add(item);
            return item;
        }
    }
}
