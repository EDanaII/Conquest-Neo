namespace Conquest.Core;

public interface IRng {
    int Next(int minInclusive, int maxExclusive);
    void Reseed(int seed);
}