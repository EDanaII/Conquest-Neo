using Conquest.Core.Random;
using Conquest.Core.Stars;
using NUnit.Framework;

namespace Conquest.Tests;

public class StarParityTests
{
    [Test]
    public void Seed_12345_matches_C_output_for_20_stars()
    {
        // arrange
        var rng = new CRandRng();       // your C-parity RNG
        rng.Seed(12345);

        var gen = new StarGenerator(width: 64, height: 64, minSpacing: 3);
        var stars = gen.Generate(count: 20, rng);

        // expected snapshot from the C executable with the same seed/params
        // format: (x,y) in order of acceptance
        var expected = new (int x,int y)[]
        {
            // TODO: paste from your C run
            (5,34),(17,2),(61,61),(42,19),(8,9),
            (13,52),(59,3),(30,45),(24,23),(50,38),
            (2,60),(44,6),(9,27),(33,11),(20,59),
            (55,21),(6,46),(46,48),(14,37),(40,58)
        };

        Assert.That(stars.Count, Is.EqualTo(expected.Length), "Star count differs");
        for (int i=0; i<expected.Length; i++)
            Assert.That(stars[i], Is.EqualTo(expected[i]), $"Mismatch at index {i}");
    }
}
