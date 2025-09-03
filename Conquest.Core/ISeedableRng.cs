// Optional interface for RNGs that support seeding.
namespace Conquest.Core {
    public interface ISeedableRng : IRng {
        void Seed(int seed);
    }
}