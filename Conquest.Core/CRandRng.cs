// File: Conquest.Core/Random/CRandRng.cs
using System;

// Alias the interface so we don't fight namespaces
using ICRandRng = Conquest.Core.Random.ICRandRng;

namespace Conquest.Core {
    /// <summary>
    /// Simple deterministic RNG (LCG). Implements both your app's IRng/ISeedableRng
    /// and the StarGenerator's Conquest.Core.Random.ICRandRng.
    /// </summary>
    public sealed class CRandRng : ISeedableRng, ICRandRng {
        private uint _state;

        public CRandRng( ) : this(1) { }
        public CRandRng(int seed) { Seed(seed); }

        // ---- ISeedableRng / your code path ----
        public void Seed(int seed) {
            unchecked {
                _state = (uint)seed;
                if (_state == 0) _state = 1u; // avoid zero state
            }
        }

        // Bridge your IRng reseed (if IRng has it)
        void IRng.Reseed(int seed) => Seed(seed);

        // Your app's IRng.Next(min,max)
        public int Next(int minInclusive, int maxExclusive) {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be > minInclusive");

            // LCG step: X = (aX + c) mod 2^32
            _state = unchecked(1103515245u * _state + 12345u);

            // Non-negative 31-bit value
            int value31 = (int)(_state >> 1);
            int range = maxExclusive - minInclusive;
            return minInclusive + (value31 % range);
        }

        // ---- StarGenerator's interface (explicit impls) ----
        // ICRandRng.Seed(uint)
        void ICRandRng.Seed(uint seed) {
            _state = seed == 0 ? 1u : seed;
        }

        // ICRandRng.NextInt(int maxExclusive) -> [0, maxExclusive)
        int ICRandRng.NextInt(int maxExclusive) {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be > 0");
            return Next(0, maxExclusive);
        }
    }
}
