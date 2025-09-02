namespace Conquest.Core.Random;

public sealed class CRandRng : IRng, ICRandRng {
    private uint _seed = 1;

    public CRandRng( ) { }
    public CRandRng(int seed) => Reseed(seed);

    // ICRandRng
    public void Seed(uint seed) => _seed = seed == 0 ? 1u : seed;
    public int NextInt(int maxExclusive) {
        if (maxExclusive <= 1) return 0;
        return NextRaw( ) % maxExclusive; // exact C rand()%max behavior
    }

    // IRng
    public int Next(int minInclusive, int maxExclusive) {
        if (maxExclusive <= minInclusive) return minInclusive;
        var r = NextRaw( ); // 0..32767
        long range = (long)maxExclusive - minInclusive;
        long scaled = (long)r * range / 32768L;
        return (int)(minInclusive + scaled);
    }

    public void Reseed(int seed) => Seed(unchecked((uint)seed));

    private int NextRaw( ) {
        _seed = unchecked(_seed * 214013u + 2531011u);
        return (int)((_seed >> 16) & 0x7FFFu);
    }
}
