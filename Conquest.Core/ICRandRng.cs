public interface ICRandRng {
    void Seed(uint seed);
    // Return 0..maxExclusive-1 using C’s modulus behavior
    int NextInt(int maxExclusive);
}
