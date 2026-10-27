using System.Text.Json;
using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.Tests;

public sealed class EnemyStartupResearchTests {
    private sealed class ScriptedRng : IRng {
        private readonly Queue<(int Min, int Max, int Value)> _draws = new();
        public int Calls { get; private set; }
        public int? FailOnCall { get; set; }
        public Action? BeforeDraw { get; set; }
        public void Add(int min, int max, int value) => _draws.Enqueue((min, max, value));
        public int Next(int minInclusive, int maxExclusive) {
            Calls++;
            if (Calls == FailOnCall) throw new InvalidOperationException("Scripted RNG failure");
            BeforeDraw?.Invoke();
            Assert.NotEmpty(_draws);
            var draw = _draws.Dequeue();
            Assert.Equal((draw.Min, draw.Max), (minInclusive, maxExclusive));
            Assert.InRange(draw.Value, minInclusive, maxExclusive - 1);
            return draw.Value;
        }
        public void Reseed(int seed) => throw new NotSupportedException();
        public void AssertExhausted() => Assert.Empty(_draws);
    }

    private static (Game Game, GameState State, ScriptedRng Rng) GeneratedGame() {
        var rng = new ScriptedRng();
        var state = new GameState(new GameConfig { NumStars = 2 });
        var game = new Game(rng, state);
        rng.Add(0, 15, 0);
        rng.Add(0, 15, 1);
        rng.Add(1, 5, 3);
        rng.Add(1, 3, 2);
        rng.Add(1, 5, 4);
        rng.Add(1, 5, 3);
        rng.Add(0, 15, 2);
        rng.Add(0, 15, 3);
        rng.Add(1, 5, 2);
        Assert.Null(state.EnemyResearchField);
        game.GenerateStars();
        Assert.Equal(9, rng.Calls);
        rng.AssertExhausted();
        return (game, state, rng);
    }

    [Theory]
    // Weapons 3: strict threshold 5; weapons 6: strict threshold 2.
    [InlineData(1, 1, 4, 0, ResearchField.Weapons)]
    [InlineData(1, 1, 5, 1, ResearchField.Velocity)]
    [InlineData(1, 1, 6, 3, ResearchField.Velocity)]
    [InlineData(1, 4, 1, 0, ResearchField.Weapons)]
    [InlineData(1, 4, 2, 2, ResearchField.Velocity)]
    [InlineData(1, 4, 3, 3, ResearchField.Velocity)]
    // Range/velocity uses integer division: 7, 3, 2 for velocities 1, 2, 3.
    [InlineData(2, 1, 10, 3, ResearchField.Velocity)]
    [InlineData(2, 2, 10, 1, ResearchField.Velocity)]
    [InlineData(2, 2, 10, 2, ResearchField.Velocity)]
    [InlineData(2, 2, 10, 3, ResearchField.Range)]
    [InlineData(2, 3, 10, 1, ResearchField.Velocity)]
    [InlineData(2, 3, 10, 2, ResearchField.Range)]
    [InlineData(2, 3, 10, 3, ResearchField.Range)]
    [InlineData(3, 1, 1, 0, ResearchField.Weapons)]
    public void StartupResearch_PreservesRulesAndDrawOrder(
        int advantage, int advantageValue, int weaponsDraw, int velocityDraw, ResearchField expected) {
        var (game, state, rng) = GeneratedGame();
        rng.Add(1, 4, advantage);
        rng.Add(1, advantage == 2 ? 4 : 5, advantageValue);
        game.InitializeClassicPlayers();
        Assert.Equal(11, rng.Calls);
        Assert.Null(state.EnemyResearchField);
        Assert.Equal(0, state.Players[0].VelocityResearchProgress);
        var before = OtherStateSnapshot(game);
        var stars = state.Stars.ToArray();
        var planet = Assert.Single(stars[0].Planets);
        rng.Add(1, 11, weaponsDraw);
        if (velocityDraw != 0) rng.Add(1, 4, velocityDraw);
        rng.BeforeDraw = () => {
            Assert.Equal(ResearchField.Velocity, state.EnemyResearchField);
            Assert.Equal(2, state.Players[0].VelocityResearchProgress);
            Assert.Equal(before, OtherStateSnapshot(game));
        };

        // A different facade can continue a successfully completed shared startup.
        new Game(rng, state).InitializeClassicEnemyResearch();

        Assert.Equal(expected, state.EnemyResearchField);
        Assert.Equal(2, state.Players[0].VelocityResearchProgress);
        Assert.Equal(velocityDraw == 0 ? 12 : 13, rng.Calls);
        rng.AssertExhausted();
        Assert.Equal(before, OtherStateSnapshot(game));
        for (int i = 0; i < stars.Length; i++) {
            Assert.Same(stars[i], state.Stars[i]);
            Assert.Same(stars[i], state.Board[stars[i].X, stars[i].Y].StarRef);
        }
        Assert.Same(planet, Assert.Single(state.Stars[0].Planets));
        Assert.Same(stars[0], planet.Star);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartupResearch_RejectsBeforePlayerInitialization(bool generateGalaxy) {
        var rng = new ScriptedRng();
        var game = new Game(rng, new GameState());
        if (generateGalaxy) (game, _, rng) = GeneratedGame();
        var before = OtherStateSnapshot(game);
        var calls = rng.Calls;

        Assert.Throws<InvalidOperationException>(() => game.InitializeClassicEnemyResearch());

        Assert.Equal(calls, rng.Calls);
        Assert.Null(game.State.EnemyResearchField);
        Assert.Equal(0, game.State.Players[0].VelocityResearchProgress);
        Assert.Equal(before, OtherStateSnapshot(game));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void FailedPlayerInitialization_DoesNotSatisfyPrerequisite(int failureDraw) {
        var (game, state, rng) = GeneratedGame();
        rng.Add(1, 4, 2);
        rng.Add(1, 4, 3);
        rng.FailOnCall = rng.Calls + failureDraw;
        Assert.Throws<InvalidOperationException>(() => game.InitializeClassicPlayers());
        var calls = rng.Calls;
        var before = OtherStateSnapshot(game);

        Assert.Throws<InvalidOperationException>(() => game.InitializeClassicEnemyResearch());
        Assert.Throws<InvalidOperationException>(() => new Game(rng, state).InitializeClassicEnemyResearch());

        Assert.Equal(calls, rng.Calls);
        Assert.Null(state.EnemyResearchField);
        Assert.Equal(0, state.Players[0].VelocityResearchProgress);
        Assert.Equal(before, OtherStateSnapshot(game));
    }

    [Fact]
    public void StartupResearch_RejectsRepeatAcrossFacadesWithoutMutationOrDraws() {
        var (game, state, rng) = GeneratedGame();
        rng.Add(1, 4, 2);
        rng.Add(1, 4, 3);
        game.InitializeClassicPlayers();
        rng.Add(1, 11, 5);
        rng.Add(1, 4, 2);
        game.InitializeClassicEnemyResearch();
        var calls = rng.Calls;
        var before = OtherStateSnapshot(game);

        Assert.Throws<InvalidOperationException>(() => game.InitializeClassicEnemyResearch());
        Assert.Throws<InvalidOperationException>(() => new Game(rng, state).InitializeClassicEnemyResearch());

        Assert.Equal(calls, rng.Calls);
        Assert.Equal(2, state.Players[0].VelocityResearchProgress);
        Assert.Equal(ResearchField.Range, state.EnemyResearchField);
        Assert.Equal(before, OtherStateSnapshot(game));
        rng.AssertExhausted();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void FailedResearch_CannotBeRetriedAcrossFacades(int failureDraw) {
        var (game, state, rng) = GeneratedGame();
        rng.Add(1, 4, 2);
        rng.Add(1, 4, 2);
        game.InitializeClassicPlayers();
        rng.Add(1, 11, 10);
        rng.Add(1, 4, 3);
        rng.FailOnCall = rng.Calls + failureDraw;
        Assert.Throws<InvalidOperationException>(() => game.InitializeClassicEnemyResearch());
        var calls = rng.Calls;

        Assert.Throws<InvalidOperationException>(() => game.InitializeClassicEnemyResearch());
        Assert.Throws<InvalidOperationException>(() => new Game(rng, state).InitializeClassicEnemyResearch());

        Assert.Equal(calls, rng.Calls);
        Assert.Equal(2, state.Players[0].VelocityResearchProgress);
        Assert.Equal(ResearchField.Velocity, state.EnemyResearchField);
    }

    // Include every gameplay field except the two changes authorized by this stage:
    // enemy velocity progress and the selected enemy research field.
    private static string OtherStateSnapshot(Game game) => JsonSerializer.Serialize(new {
        game.Turn,
        Players = game.State.Players.Select((p, team) => new {
            p.Velocity, p.Range, p.Weapons, p.PopulationGrowthRate,
            p.RangeResearchProgress, p.WeaponsResearchProgress,
            VelocityResearchProgress = team == 0 ? 0 : p.VelocityResearchProgress,
            p.Tf
        }),
        Stars = game.State.Stars.Select(s => new {
            s.Name, s.X, s.Y, s.ExploredBy,
            Planets = s.Planets.Select(p => new {
                p.Number, p.Capacity, p.Owner, p.Inhabitants, p.IndustrialUnits,
                p.MissileBases, p.AdvancedMissileBases, p.Conquered, p.UnderAttack,
                p.PlayerSeenCapacity, p.EnemySeenOwner, p.EnemySeenDefense
            })
        }),
        Board = game.State.Board.Cast<Sector>().Select(c => new {
            c.Star, c.Tf, c.Status, c.ViewingTeam, c.TaskForces,
            StarIndex = c.StarRef == null ? -1 : game.State.Stars.IndexOf(c.StarRef)
        }),
        game.State.FogEnabled, game.State.RevealStars,
        Visible = game.State.Visible?.Cast<bool>().ToArray()
    });
}
