using Conquest.Core;
namespace Conquest.CLI;

class SystemRng : IRng {
    private Random _r = new( );
    public int Next(int minInclusive, int maxExclusive) => _r.Next(minInclusive, maxExclusive);
    public void Reseed(int seed) => _r = new Random(seed);
}