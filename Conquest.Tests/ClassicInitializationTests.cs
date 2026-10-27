using System.Text.Json;
using Conquest.Core;
using Conquest.Core.Model;
using Conquest.Tests.TestHelpers;

namespace Conquest.Tests;

public sealed class ClassicInitializationTests {
    private sealed class ScriptedRng : IRng {
        private readonly Queue<(int Min, int Max, int Value)> _draws = new();
        public Action? BeforeDraw { get; set; }
        public int Calls { get; private set; }

        public void Add(int min, int max, int value) => _draws.Enqueue((min, max, value));
        public int Next(int minInclusive, int maxExclusive) {
            BeforeDraw?.Invoke();
            Assert.NotEmpty(_draws);
            var draw = _draws.Dequeue();
            Assert.Equal((draw.Min, draw.Max), (minInclusive, maxExclusive));
            Assert.InRange(draw.Value, minInclusive, maxExclusive - 1);
            Calls++;
            return draw.Value;
        }
        public void Reseed(int seed) => throw new NotSupportedException();
        public void AssertExhausted() => Assert.Empty(_draws);
    }

    private static (Game Game, GameState State, ScriptedRng Rng) PreparedGame(int transports = 35) {
        var rng = new ScriptedRng();
        var state = new GameState(new GameConfig { NumStars = 2, StartingTransports = transports });
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
        game.GenerateStars();
        rng.AssertExhausted();
        return (game, state, rng);
    }

    [Theory]
    [InlineData(1, 1, 3, 1, 0.5)]
    [InlineData(1, 2, 4, 1, 0.5)]
    [InlineData(1, 3, 5, 1, 0.5)]
    [InlineData(1, 4, 6, 1, 0.5)]
    [InlineData(2, 1, 3, 1, 0.5)]
    [InlineData(2, 2, 3, 2, 0.5)]
    [InlineData(2, 3, 3, 3, 0.5)]
    [InlineData(3, 1, 3, 1, 0.4)]
    [InlineData(3, 2, 3, 1, 0.5)]
    [InlineData(3, 3, 3, 1, 0.6)]
    [InlineData(3, 4, 3, 1, 0.7)]
    public void Initialize_PreservesAllAdvantageOutcomesAndGalaxy(
        int branch, int value, int weapons, int velocity, double growth) {
        var (game, state, rng) = PreparedGame();
        var galaxyBefore = GalaxySnapshot(state);
        var starsBefore = state.Stars.ToArray();
        var planetBefore = Assert.Single(state.Stars[0].Planets);
        rng.Add(1, 4, branch);
        rng.Add(1, branch == 2 ? 4 : 5, value);
        var callsBefore = rng.Calls;
        // This callback also proves the entire galaxy exists before either advantage draw.
        rng.BeforeDraw = () => Assert.Equal(galaxyBefore, GalaxySnapshot(state));

        game.InitializeClassicPlayers();

        Assert.Equal(callsBefore + 2, rng.Calls);
        rng.AssertExhausted();
        Assert.Equal(weapons, state.Players[0].Weapons);
        Assert.Equal(velocity, state.Players[0].Velocity);
        Assert.Equal(growth, state.Players[0].PopulationGrowthRate);
        Assert.Equal(7, state.Players[0].Range);
        Assert.Equal(3, state.Players[1].Weapons);
        Assert.Equal(1, state.Players[1].Velocity);
        Assert.Equal(0.3, state.Players[1].PopulationGrowthRate);
        Assert.Equal(5, state.Players[1].Range);
        Assert.All(state.Players, p => {
            Assert.Equal(0, p.VelocityResearchProgress);
            Assert.Equal(0, p.RangeResearchProgress);
            Assert.Equal(0, p.WeaponsResearchProgress);
            Assert.Equal(35, p.Tf[1].Transports);
            Assert.All(p.Tf.Skip(2), tf => Assert.Equal(0, tf.Transports));
            Assert.All(p.Tf.Skip(1), AssertNoOtherFleetState);
        });
        Assert.Equal(0, game.Turn);
        Assert.Equal(galaxyBefore, GalaxySnapshot(state));
        for (int i = 0; i < starsBefore.Length; i++) {
            Assert.Same(starsBefore[i], state.Stars[i]);
            Assert.Same(starsBefore[i], state.Board[starsBefore[i].X, starsBefore[i].Y].StarRef);
        }
        Assert.Same(planetBefore, Assert.Single(state.Stars[0].Planets));
        Assert.Same(state.Stars[0], planetBefore.Star);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(12)]
    [InlineData(100)]
    public void Initialize_UsesConfiguredTransportsWithoutClamping(int transports) {
        var (game, state, rng) = PreparedGame(transports);
        rng.Add(1, 4, 1);
        rng.Add(1, 5, 1);
        game.InitializeClassicPlayers();
        Assert.All(state.Players, p => Assert.Equal(transports, p.Tf[1].Transports));
        rng.AssertExhausted();
    }

    [Fact]
    public void Initialize_RejectsRepeatThroughEitherFacadeWithoutMutationOrDraws() {
        var (game, state, rng) = PreparedGame();
        rng.Add(1, 4, 3);
        rng.Add(1, 5, 1);
        game.InitializeClassicPlayers();
        var playersBefore = JsonSerializer.Serialize(state.Players);
        var galaxyBefore = GalaxySnapshot(state);
        var callsBefore = rng.Calls;

        Assert.Throws<InvalidOperationException>(() => game.InitializeClassicPlayers());
        Assert.Throws<InvalidOperationException>(() => new Game(rng, state).InitializeClassicPlayers());

        Assert.Equal(playersBefore, JsonSerializer.Serialize(state.Players));
        Assert.Equal(galaxyBefore, GalaxySnapshot(state));
        Assert.Equal(callsBefore, rng.Calls);
        Assert.Equal(0, game.Turn);
    }

    [Fact]
    public void Initialize_RequiresGeneratedGalaxy_AndConstructorsDoNotInitialize() {
        var rng = new ScriptedRng();
        var state = new GameState();
        var game = new Game(rng, state);
        Assert.Equal(35, state.Config.StartingTransports);
        Assert.All(state.Players, p => {
            Assert.Equal(0, p.Velocity);
            Assert.Equal(0, p.Range);
            Assert.Equal(0, p.Weapons);
            Assert.Equal(0, p.PopulationGrowthRate);
            Assert.All(p.Tf.Skip(1), tf => Assert.Equal(0, tf.Transports));
        });
        Assert.Throws<InvalidOperationException>(() => game.InitializeClassicPlayers());
        Assert.Equal(0, rng.Calls);
    }

    [Theory]
    [InlineData("game")]
    [InlineData("shared facade")]
    [InlineData("explicit state")]
    [InlineData("state placement")]
    public void Regeneration_AfterInitialization_IsRejectedWithoutMutationOrDraws(string path) {
        var (game, state, rng) = PreparedGame();
        rng.Add(1, 4, 3);
        rng.Add(1, 5, 1);
        game.InitializeClassicPlayers();
        var starsBefore = state.Stars.ToArray();
        var planetBefore = Assert.Single(state.Stars[0].Planets);
        var playersBefore = JsonSerializer.Serialize(state.Players);
        var galaxyBefore = GalaxySnapshot(state);
        var callsBefore = rng.Calls;
        var sharedGame = new Game(rng, state);

        Assert.Throws<InvalidOperationException>(() => {
            switch (path) {
                case "game": game.GenerateStars(); break;
                case "shared facade": sharedGame.GenerateStars(); break;
                // The guard must inspect the supplied state, not the facade's state.
                case "explicit state": new Game(rng, new GameState()).GenerateStars(rng, state); break;
                case "state placement": state.PlaceStars(rng); break;
            }
        });
        Assert.Throws<InvalidOperationException>(() => game.InitializeClassicPlayers());
        Assert.Throws<InvalidOperationException>(() => sharedGame.InitializeClassicPlayers());

        Assert.Equal(playersBefore, JsonSerializer.Serialize(state.Players));
        Assert.Equal(galaxyBefore, GalaxySnapshot(state));
        Assert.Equal(callsBefore, rng.Calls);
        Assert.Equal(0, game.Turn);
        for (int i = 0; i < starsBefore.Length; i++) {
            Assert.Same(starsBefore[i], state.Stars[i]);
            Assert.Same(starsBefore[i], state.Board[starsBefore[i].X, starsBefore[i].Y].StarRef);
        }
        Assert.Same(planetBefore, Assert.Single(state.Stars[0].Planets));
        Assert.Same(state.Stars[0], planetBefore.Star);
        rng.AssertExhausted();
    }

    [Theory]
    [InlineData("fleet")]
    [InlineData("attributes")]
    [InlineData("research")]
    [InlineData("turn")]
    [InlineData("exploration")]
    [InlineData("colony")]
    public void Initialize_RejectsUsedStateBeforeMutationOrDraws(string mutation) {
        var (game, state, rng) = PreparedGame();
        switch (mutation) {
            case "fleet": state.FleetsSys.Place(1, 26, 0); break;
            case "attributes": state.Players[0].Range = 7; break;
            case "research": state.Players[1].WeaponsResearchProgress = 1; break;
            case "turn": game.Tick(); break;
            case "exploration": state.Stars[1].ExploredBy[0] = true; break;
            case "colony": state.Stars[0].Planets[0].Owner = Ownership.Player; break;
        }
        var playersBefore = JsonSerializer.Serialize(state.Players);
        var galaxyBefore = GalaxySnapshot(state);
        var callsBefore = rng.Calls;
        var turnBefore = game.Turn;

        Assert.Throws<InvalidOperationException>(() => game.InitializeClassicPlayers());

        Assert.Equal(playersBefore, JsonSerializer.Serialize(state.Players));
        Assert.Equal(galaxyBefore, GalaxySnapshot(state));
        Assert.Equal(callsBefore, rng.Calls);
        Assert.Equal(turnBefore, game.Turn);
    }

    [Fact]
    public void DebugCommands_DoNotInitializeClassicPlayers() {
        var g = TestGame.New();
        Assert.Equal(0, g.RunCommands("genstars", "place 1"));
        Assert.All(g.State.Players, p => {
            Assert.Equal(0, p.Velocity);
            Assert.Equal(0, p.Weapons);
            Assert.Equal(0, p.Range);
            Assert.Equal(0, p.PopulationGrowthRate);
            Assert.Equal(0, p.Tf[1].Transports);
        });
    }

    private static void AssertNoOtherFleetState(TaskForce tf) {
        Assert.Equal(0, tf.Scouts);
        Assert.Equal(0, tf.Cruisers);
        Assert.Equal(0, tf.BattleStars);
        Assert.Equal((0, 0), (tf.X, tf.Y));
        Assert.Null(tf.Destination);
        Assert.Equal(0, tf.ETA);
        Assert.Equal(0, tf.EtaOriginal);
        Assert.Equal(0, tf.Blasting);
        Assert.Equal(0, tf.Withdrew);
    }

    private static string GalaxySnapshot(GameState state) => JsonSerializer.Serialize(new {
        Stars = state.Stars.Select(s => new {
            s.Name, s.X, s.Y, s.ExploredBy,
            Planets = s.Planets.Select(p => new {
                p.Number, p.Capacity, p.Owner, p.Inhabitants, p.IndustrialUnits,
                p.MissileBases, p.AdvancedMissileBases, p.Conquered, p.UnderAttack,
                p.PlayerSeenCapacity, p.EnemySeenOwner, p.EnemySeenDefense
            })
        }),
        Board = state.Board.Cast<Sector>().Select(c => new {
            c.Star, c.Tf, c.Status, c.ViewingTeam,
            StarIndex = c.StarRef == null ? -1 : state.Stars.IndexOf(c.StarRef)
        }),
        state.FogEnabled, state.RevealStars
    });
}
