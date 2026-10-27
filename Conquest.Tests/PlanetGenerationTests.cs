using Conquest.CLI;
using Conquest.Core;
using Conquest.Core.Model;
using Conquest.Tests.TestHelpers;

namespace Conquest.Tests;

public sealed class PlanetGenerationTests {
    private sealed class ScriptedRng : IRng {
        private readonly Queue<(int Min, int Max, int Value)> _draws = new();

        public void Add(int min, int max, int value) => _draws.Enqueue((min, max, value));

        public int Next(int minInclusive, int maxExclusive) {
            Assert.NotEmpty(_draws);
            var draw = _draws.Dequeue();
            Assert.Equal((draw.Min, draw.Max), (minInclusive, maxExclusive));
            Assert.InRange(draw.Value, minInclusive, maxExclusive - 1);
            return draw.Value;
        }

        public void Reseed(int seed) => throw new NotSupportedException();
        public void AssertExhausted() => Assert.Empty(_draws);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    public void GenerateStars_PreservesEveryPlanetCountOutcome(int countDraw, int expectedCount) {
        var rng = new ScriptedRng();
        rng.Add(0, 15, 0);
        rng.Add(0, 15, 0);
        rng.Add(1, 5, countDraw);
        for (int i = 0; i < expectedCount; i++) {
            rng.Add(1, 3, i + 1);
            rng.Add(1, 5, 3);
            rng.Add(1, 5, 1);
        }
        var state = new GameState(new GameConfig { NumStars = 1 });

        new Game(rng, state).GenerateStars();

        Assert.Equal(expectedCount, state.Stars[0].Planets.Count);
        Assert.Equal(new[] { 1, 4 }.Take(expectedCount), state.Stars[0].Planets.Select(p => p.Number));
        Assert.All(state.Stars[0].Planets, p => AssertInitialState(p, state.Stars[0], 30));
        rng.AssertExhausted();
    }

    [Theory]
    [InlineData(1, 1, 5)]
    [InlineData(2, 2, 10)]
    [InlineData(1, 3, 15)]
    [InlineData(2, 4, 20)]
    [InlineData(2, 5, 25)]
    [InlineData(3, 1, 30)]
    [InlineData(4, 2, 40)]
    [InlineData(3, 3, 50)]
    [InlineData(4, 4, 60)]
    public void GenerateStars_PreservesCapacityFormulasAndInitialState(int branch, int value, int capacity) {
        var rng = new ScriptedRng();
        rng.Add(0, 15, 0);
        rng.Add(0, 15, 14);
        rng.Add(1, 5, 3);
        rng.Add(1, 3, 2);
        rng.Add(1, 5, branch);
        rng.Add(1, branch > 2 ? 5 : 6, value);
        var state = new GameState(new GameConfig { NumStars = 1 });

        new Game(rng, state).GenerateStars();

        var planet = Assert.Single(state.Stars[0].Planets);
        Assert.Equal(2, planet.Number);
        AssertInitialState(planet, state.Stars[0], capacity);
        rng.AssertExhausted();
    }

    [Fact]
    public void Genstars_InterleavesPlanetsWithPlacement_AndRetriesOnlyDuplicateCoordinates() {
        var rng = new ScriptedRng();
        rng.Add(0, 15, 0); // First star, including a valid zero-axis position.
        rng.Add(0, 15, 14);
        rng.Add(1, 5, 4);
        rng.Add(1, 3, 2);
        rng.Add(1, 5, 2);
        rng.Add(1, 6, 5);
        rng.Add(1, 3, 1);
        rng.Add(1, 5, 4);
        rng.Add(1, 5, 4);
        rng.Add(0, 15, 0); // Second star initially collides.
        rng.Add(0, 15, 14);
        rng.Add(0, 15, 1); // Adjacent stars are allowed.
        rng.Add(0, 15, 14);
        rng.Add(1, 5, 2); // Zero planets: no further draws.
        var state = new GameState(new GameConfig { NumStars = 2 });
        var game = new Game(rng, state);
        var io = new DummyIO(new[] { "genstars", "quit" });
        var shell = new GameShell(game, io, rng, new SpyRenderer());

        Assert.Equal(0, shell.Run());

        Assert.Equal(new[] { (0, 14), (1, 14) }, state.Stars.Select(s => (s.X, s.Y)));
        Assert.Equal(new[] { 2, 3 }, state.Stars[0].Planets.Select(p => p.Number));
        AssertInitialState(state.Stars[0].Planets[0], state.Stars[0], 25);
        AssertInitialState(state.Stars[0].Planets[1], state.Stars[0], 60);
        Assert.Empty(state.Stars[1].Planets);
        for (int i = 0; i < state.Stars.Count; i++) {
            var star = state.Stars[i];
            Assert.Same(star, state.Board[star.X, star.Y].StarRef);
            Assert.Equal(i, state.Board[star.X, star.Y].Star);
        }
        Assert.Null(state.Board[2, 14].StarRef);
        Assert.Equal(-1, state.Board[2, 14].Star);
        rng.AssertExhausted();

        // A second generation replaces planets, including a populated-to-empty transition.
        rng.Add(0, 15, 2);
        rng.Add(0, 15, 0);
        rng.Add(1, 5, 2);
        rng.Add(0, 15, 3);
        rng.Add(0, 15, 0);
        rng.Add(1, 5, 2);
        game.GenerateStars();
        Assert.All(state.Stars, star => Assert.Empty(star.Planets));
        Assert.Null(state.Board[0, 14].StarRef);
        Assert.Equal(-1, state.Board[0, 14].Star);
        rng.AssertExhausted();
    }

    private static void AssertInitialState(Planet planet, Star star, int capacity) {
        Assert.Same(star, planet.Star);
        Assert.Equal(capacity, planet.Capacity);
        Assert.Equal(capacity, planet.PlayerSeenCapacity);
        Assert.Equal(Ownership.None, planet.Owner);
        Assert.Equal(Ownership.None, planet.EnemySeenOwner);
        Assert.Equal(1, planet.EnemySeenDefense);
        Assert.Equal(0, planet.Inhabitants);
        Assert.Equal(0, planet.IndustrialUnits);
        Assert.Equal(0, planet.MissileBases);
        Assert.Equal(0, planet.AdvancedMissileBases);
        Assert.False(planet.Conquered);
        Assert.False(planet.UnderAttack);
    }
}
