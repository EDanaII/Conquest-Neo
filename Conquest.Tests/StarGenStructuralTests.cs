using Conquest.Core;
using Conquest.Core.Random;
using Conquest.Core.Stars;
using Xunit;

namespace Conquest.Tests;

public sealed class StarGenStructuralTests {
    [Fact]
    public void StarGen_StructuralRulesHold_ForSeed12345( ) {
        var rng = new CRandRng( ); rng.Seed(12345);
        int W = 64, H = 64, spacing = 3, count = 20;
        var gen = new StarGenerator(W, H, spacing);
        var stars = gen.Generate(count, rng);

        Assert.Equal(count, stars.Count);

        var seen = new HashSet<(int, int)>( );
        foreach (var (x, y) in stars) {
            Assert.InRange(x, 0, W - 1);
            Assert.InRange(y, 0, H - 1);
            Assert.True(seen.Add((x, y)), $"Duplicate star at ({x},{y})");
        }

        for (int i = 0; i < stars.Count; i++)
            for (int j = i + 1; j < stars.Count; j++) {
                var (x1, y1) = stars[i];
                var (x2, y2) = stars[j];
                int dx = x1 - x2, dy = y1 - y2;
                int d2 = dx * dx + dy * dy;
                Assert.True(d2 >= spacing * spacing,
                    $"Stars too close: ({x1},{y1}) and ({x2},{y2})");
            }

        var rng2 = new CRandRng( ); rng2.Seed(12345);
        var stars2 = gen.Generate(count, rng2);
        Assert.Equal(stars, stars2);
    }
}
