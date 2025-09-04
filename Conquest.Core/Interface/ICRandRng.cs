namespace Conquest.Core.Random;
public interface ICRandRng {
    void Seed(uint seed);
    int NextInt(int maxExclusive);
}
