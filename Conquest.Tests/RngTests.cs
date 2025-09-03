using Conquest.Core;
using Conquest.Core.Random;
using Xunit;

namespace Conquest.Tests;

public sealed class RngTests {
    [Fact]
    public void Next_Respects_Bounds( ) {
        var r = new CRandRng( ); r.Seed(123);
        for (int i = 0; i < 10_000; i++) {
            var v = r.Next(5, 17);
            Assert.InRange(v, 5, 16);
        }
    }

    [Fact]
    public void Seed_Zero_Becomes_NonZero( ) {
        var r = new CRandRng( );
        r.Seed(0); // should map to 1 internally
        var a = r.Next(0, 10);
        r.Seed(0);
        var b = r.Next(0, 10);
        Assert.Equal(a, b); // deterministic after consistent seeding
    }
}
