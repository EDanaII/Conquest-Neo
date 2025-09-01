// Conquest.CLI/CRandRng.cs
using Conquest.Core;

namespace Conquest.CLI;

/// Emulates C's srand/rand from MSVCRT (LCG).
/// rand(): seed = seed * 214013 + 2531011; return (seed >> 16) & 0x7FFF (0..32767)
class CRandRng : IRng {
    private uint _seed;

    public CRandRng(int seed = 1) => Reseed(seed);

    public void Reseed(int seed) {
        // C's srand takes unsigned int; allow any int and cast
        _seed = unchecked((uint)seed);
        if (_seed == 0) _seed = 1; // avoid degenerate all-zero path
    }

    // Equivalent to C's rand() scaled to [min, max)
    public int Next(int minInclusive, int maxExclusive) {
        if (maxExclusive <= minInclusive) return minInclusive;
        var r = NextRaw( ); // 0..32767
        // scale without bias using 64-bit
        long range = (long)maxExclusive - minInclusive;
        long scaled = (long)r * range / 32768L;
        return (int)(minInclusive + scaled);
    }

    // C rand(): 0..32767
    private int NextRaw( ) {
        _seed = unchecked(_seed * 214013u + 2531011u);
        return (int)((_seed >> 16) & 0x7FFFu);
    }
}