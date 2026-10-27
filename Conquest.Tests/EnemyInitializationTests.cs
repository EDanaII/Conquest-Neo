using System.Text.Json;
using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.Tests;

public sealed class EnemyInitializationTests {
    private sealed class ScriptedRng : IRng {
        private readonly Queue<(int Min, int Max, int Value)> _draws = new();
        public int Calls { get; private set; }
        public int? FailOnCall { get; set; }
        public Action<int, int>? BeforeDraw { get; set; }
        public void Add(int min, int max, int value) => _draws.Enqueue((min, max, value));
        public int Next(int minInclusive, int maxExclusive) {
            Calls++;
            if (Calls == FailOnCall) throw new InvalidOperationException("Scripted RNG failure");
            BeforeDraw?.Invoke(minInclusive, maxExclusive);
            Assert.NotEmpty(_draws);
            var draw = _draws.Dequeue();
            Assert.Equal((draw.Min, draw.Max), (minInclusive, maxExclusive));
            Assert.InRange(draw.Value, minInclusive, maxExclusive - 1);
            return draw.Value;
        }
        public void Reseed(int seed) => throw new NotSupportedException();
        public void AssertExhausted() => Assert.Empty(_draws);
    }

    private static (Game Game, ScriptedRng Rng) GeneratedGame((int X, int Y)[] positions, int transports = 35) {
        var rng = new ScriptedRng();
        var state = new GameState(new GameConfig { NumStars = positions.Length, StartingTransports = transports });
        for (int i = 0; i < positions.Length; i++) {
            rng.Add(0, 15, positions[i].X);
            rng.Add(0, 15, positions[i].Y);
            rng.Add(1, 5, i == 0 ? 3 : 2);
            if (i == 0) {
                rng.Add(1, 3, 2);
                rng.Add(1, 5, 4);
                rng.Add(1, 5, 3);
            }
        }
        var game = new Game(rng, state);
        game.GenerateStars();
        Assert.Equal(positions.Length * 3 + (positions.Length == 0 ? 0 : 3), rng.Calls);
        rng.AssertExhausted();
        return (game, rng);
    }

    private static void InitializePlayers(Game game, ScriptedRng rng) {
        rng.Add(1, 4, 2);
        rng.Add(1, 4, 3);
        game.InitializeClassicPlayers();
    }

    [Fact]
    public void Reachability_CountsSelf_InclusiveBoundary_AndEuclideanDiagonals() {
        var stars = new[] {
            new Star { X = 0, Y = 0 }, new Star { X = 7, Y = 0 },
            new Star { X = 5, Y = 4 }, new Star { X = 7, Y = 1 },
            new Star { X = 14, Y = 14 }
        };
        Assert.Equal(3, EnemyStartingStarRules.CountReachableStars(stars, 0, 7));
        Assert.Equal(1, EnemyStartingStarRules.CountReachableStars(stars, 0, 0));
        Assert.Equal(1, EnemyStartingStarRules.CountReachableStars(new[] { stars[0] }, 0, 7));
    }

    public static IEnumerable<object[]> SelectionScenarios() {
        yield return new object[] { new[] { (0, 0) }, new[] { 5 }, 0 };
        // Counts 2, 3, 2: center wins on reachability with equal bonuses.
        yield return new object[] { new[] { (0, 0), (7, 0), (14, 0) }, new[] { 1, 1, 1 }, 1 };
        // A larger bonus outweighs lower reachability; the last candidate wins.
        yield return new object[] { new[] { (0, 0), (7, 0), (14, 0) }, new[] { 1, 1, 5 }, 2 };
        // First wins tied scores, but every candidate still consumes a draw.
        yield return new object[] { new[] { (0, 0), (7, 0), (14, 0) }, new[] { 2, 1, 2 }, 0 };
        // Candidate 0 sees candidate 1 by Euclidean distance; Manhattan would tie all.
        yield return new object[] { new[] { (0, 0), (5, 4), (14, 14) }, new[] { 1, 1, 2 }, 0 };
        // Inclusive distance 7 makes the first two candidates tie the isolated last star.
        yield return new object[] { new[] { (0, 0), (7, 0), (14, 14) }, new[] { 1, 1, 2 }, 0 };
    }

    [Theory]
    [MemberData(nameof(SelectionScenarios))]
    public void InitializeEnemy_SelectsAndPlacesWithoutChangingOtherState(
        (int X, int Y)[] positions, int[] bonuses, int expectedWinner) {
        var (game, rng) = GeneratedGame(positions, transports: 17);
        var state = game.State;
        InitializePlayers(game, rng);
        // Visibility/debug flags must not be reset or reveal the enemy's star to the player.
        state.FogEnabled = true;
        state.Discover(14, 14);
        state.Board[14, 14].Status = 42;
        var before = Snapshot(game, includeStartupChanges: false);
        var stars = state.Stars.ToArray();
        var planet = Assert.Single(stars[0].Planets);
        rng.Add(1, 11, 5); // Strict threshold equality fails weapons.
        rng.Add(1, 4, 2); // Velocity 3, quotient 2: equality selects Range.
        foreach (int bonus in bonuses) rng.Add(1, 6, bonus);
        int callsBefore = rng.Calls;
        int scoringCalls = 0;
        rng.BeforeDraw = (min, max) => {
            Assert.Equal(1, state.Players[0].Tf[1].Cruisers);
            Assert.Equal(2, state.Players[0].Tf[1].Scouts);
            Assert.Equal(2, state.Players[0].VelocityResearchProgress);
            if (max == 6) {
                scoringCalls++;
                Assert.Equal(ResearchField.Range, state.EnemyResearchField);
            }
        };

        new Game(rng, state).InitializeClassicEnemy();

        var fleet = state.Players[0].Tf[1];
        Assert.Equal(1, fleet.Cruisers);
        Assert.Equal(2, fleet.Scouts);
        Assert.Equal(17, fleet.Transports);
        Assert.Equal(0, fleet.BattleStars);
        Assert.Equal(0, fleet.Blasting);
        Assert.Equal(0, fleet.Withdrew);
        Assert.Equal(expectedWinner, fleet.Destination);
        Assert.Equal(0, fleet.ETA);
        Assert.Equal(0, fleet.EtaOriginal);
        Assert.Equal(positions[expectedWinner], (fleet.X, fleet.Y));
        Assert.Equal(ResearchField.Range, state.EnemyResearchField);
        Assert.Equal(2, state.Players[0].VelocityResearchProgress);
        Assert.Equal(positions.Length, scoringCalls);
        Assert.Equal(callsBefore + 2 + positions.Length, rng.Calls);
        rng.AssertExhausted();
        Assert.Equal(before, Snapshot(game, includeStartupChanges: false));
        for (int i = 0; i < stars.Length; i++) {
            Assert.Same(stars[i], state.Stars[i]);
            Assert.Equal(i == expectedWinner, stars[i].ExploredBy[0]);
            Assert.False(stars[i].ExploredBy[1]);
            Assert.Same(stars[i], state.Board[stars[i].X, stars[i].Y].StarRef);
        }
        foreach (var cell in state.Board) {
            Assert.Equal(-1, cell.Tf);
            Assert.Null(cell.TaskForces[1]);
            if (ReferenceEquals(cell.StarRef, stars[expectedWinner])) Assert.Same(fleet, cell.TaskForces[0]);
            else Assert.Null(cell.TaskForces[0]);
        }
        Assert.Same(planet, Assert.Single(stars[0].Planets));
        Assert.Same(stars[0], planet.Star);
    }

    [Fact]
    public void InitializeEnemy_WeaponsResearchSkipsVelocityDraw_AndRejectsRepeatAcrossFacades() {
        var (game, rng) = GeneratedGame(new[] { (0, 0), (14, 14) });
        InitializePlayers(game, rng);
        rng.Add(1, 11, 4);
        rng.Add(1, 6, 5);
        rng.Add(1, 6, 1);
        var callsBefore = rng.Calls;
        game.InitializeClassicEnemy();
        Assert.Equal(ResearchField.Weapons, game.State.EnemyResearchField);
        Assert.Equal(35, game.State.Players[0].Tf[1].Transports);
        Assert.Equal(callsBefore + 3, rng.Calls);
        Assert.Equal(0, game.State.Players[0].Tf[1].Destination);
        AssertRejectedUnchanged(game, rng);
        rng.AssertExhausted();
    }

    [Theory]
    [InlineData("no players")]
    [InlineData("empty galaxy")]
    [InlineData("independent research")]
    [InlineData("failed independent research")]
    [InlineData("research progress")]
    [InlineData("fleet ships")]
    [InlineData("fleet transports")]
    [InlineData("fleet destination")]
    [InlineData("fleet coordinates")]
    [InlineData("fleet eta")]
    [InlineData("player fleet")]
    [InlineData("occupancy")]
    [InlineData("marker")]
    [InlineData("board link")]
    [InlineData("turn")]
    [InlineData("direct fleet tick")]
    [InlineData("separate fleet tick")]
    public void InitializeEnemy_RejectsInvalidStateBeforeMutationOrDraws(string invalid) {
        var (game, rng) = GeneratedGame(invalid == "empty galaxy" ? Array.Empty<(int, int)>() : new[] { (1, 1) });
        if (invalid != "no players") InitializePlayers(game, rng);
        var state = game.State;
        switch (invalid) {
            case "independent research":
                rng.Add(1, 11, 1);
                game.InitializeClassicEnemyResearch();
                break;
            case "failed independent research":
                rng.FailOnCall = rng.Calls + 1;
                Assert.Throws<InvalidOperationException>(() => game.InitializeClassicEnemyResearch());
                break;
            case "research progress": state.Players[0].RangeResearchProgress = 1; break;
            case "fleet ships": state.Players[0].Tf[1].Cruisers = 1; break;
            case "fleet transports": state.Players[0].Tf[1].Transports++; break;
            case "fleet destination": state.Players[0].Tf[1].Destination = 0; break;
            case "fleet coordinates": state.Players[0].Tf[1].X = 1; break;
            case "fleet eta": state.Players[0].Tf[1].ETA = 1; break;
            case "player fleet": state.FleetsSys.Place(1, 26, 0); break;
            case "occupancy": state.Board[0, 0].TaskForces[0] = state.Players[0].Tf[1]; break;
            case "marker": state.Board[0, 0].Tf = 1; break;
            case "board link": state.Board[1, 1].StarRef = null; break;
            case "turn": game.Tick(); break;
            case "direct fleet tick": state.FleetsSys.Tick(); break;
            case "separate fleet tick": new FleetSystem(state).Tick(); break;
        }
        AssertRejectedUnchanged(game, rng);
    }

    [Theory]
    [InlineData(1)] // Research weapons draw
    [InlineData(2)] // Conditional research velocity draw
    [InlineData(3)] // First candidate score
    [InlineData(4)] // Last candidate score
    public void InitializeEnemy_RngFailureBlocksRetriesWithoutFurtherMutation(int failingDraw) {
        var (game, rng) = GeneratedGame(new[] { (0, 0), (14, 14) });
        InitializePlayers(game, rng);
        rng.Add(1, 11, 10);
        rng.Add(1, 4, 3);
        rng.Add(1, 6, 1);
        rng.Add(1, 6, 5);
        rng.FailOnCall = rng.Calls + failingDraw;

        Assert.Throws<InvalidOperationException>(() => game.InitializeClassicEnemy());

        Assert.Null(game.State.Players[0].Tf[1].Destination);
        Assert.All(game.State.Stars, s => Assert.False(s.ExploredBy[0]));
        Assert.All(game.State.Board.Cast<Sector>(), c => Assert.Null(c.TaskForces[0]));
        AssertRejectedUnchanged(game, rng);
    }

    [Fact]
    public void InitializeEnemy_FailedPlayerInitializationDoesNotSatisfyPrerequisite() {
        var (game, rng) = GeneratedGame(new[] { (0, 0) });
        rng.FailOnCall = rng.Calls + 1;
        Assert.Throws<InvalidOperationException>(() => game.InitializeClassicPlayers());
        AssertRejectedUnchanged(game, rng);
    }

    private static void AssertRejectedUnchanged(Game game, ScriptedRng rng) {
        var before = Snapshot(game, includeStartupChanges: true);
        int calls = rng.Calls;
        Assert.Throws<InvalidOperationException>(() => game.InitializeClassicEnemy());
        Assert.Throws<InvalidOperationException>(() => new Game(rng, game.State).InitializeClassicEnemy());
        Assert.Equal(calls, rng.Calls);
        Assert.Equal(before, Snapshot(game, includeStartupChanges: true));
    }

    private static string Snapshot(Game game, bool includeStartupChanges) => JsonSerializer.Serialize(new {
        game.Turn,
        Field = includeStartupChanges ? game.State.EnemyResearchField : null,
        Players = game.State.Players.Select((p, team) => new {
            p.Velocity, p.Range, p.Weapons, p.PopulationGrowthRate,
            p.WeaponsResearchProgress, p.RangeResearchProgress,
            VelocityProgress = includeStartupChanges || team == 1 ? p.VelocityResearchProgress : 0,
            Fleets = p.Tf.Select((tf, slot) => !includeStartupChanges && team == 0 && slot == 1 ? null : tf)
        }),
        Stars = game.State.Stars.Select(s => new {
            s.Name, s.X, s.Y, PlayerExplored = s.ExploredBy[1],
            EnemyExplored = includeStartupChanges && s.ExploredBy[0],
            Planets = s.Planets.Select(p => new {
                p.Number, p.Capacity, p.Owner, p.Inhabitants, p.IndustrialUnits,
                p.MissileBases, p.AdvancedMissileBases, p.Conquered, p.UnderAttack,
                p.PlayerSeenCapacity, p.EnemySeenOwner, p.EnemySeenDefense
            })
        }),
        Board = game.State.Board.Cast<Sector>().Select(c => new {
            c.Star, c.Tf, c.Status, c.ViewingTeam,
            StarIndex = c.StarRef == null ? -1 : game.State.Stars.IndexOf(c.StarRef),
            EnemyFleet = includeStartupChanges ? c.TaskForces[0] : null,
            PlayerFleet = c.TaskForces[1]
        }),
        game.State.FogEnabled, game.State.RevealStars,
        Visible = game.State.Visible?.Cast<bool>().ToArray()
    });
}
