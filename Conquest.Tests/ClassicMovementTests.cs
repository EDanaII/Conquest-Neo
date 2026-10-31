using Conquest.Core;
using Conquest.Core.Model;
using Conquest.Tests.TestHelpers;

namespace Conquest.Tests;

public sealed class ClassicMovementTests {
    private sealed class ScriptedRng : IRng {
        private readonly Queue<(int Max, int Value)> _draws = new();
        public void Add(int max, params int[] values) {
            foreach (int value in values) _draws.Enqueue((max, value));
        }
        public int Next(int minInclusive, int maxExclusive) {
            Assert.Equal(1, minInclusive);
            Assert.NotEmpty(_draws);
            var draw = _draws.Dequeue();
            Assert.Equal(draw.Max + 1, maxExclusive);
            Assert.InRange(draw.Value, 1, draw.Max);
            return draw.Value;
        }
        public void Reseed(int seed) => throw new NotSupportedException();
        public void AssertExhausted() => Assert.Empty(_draws);
    }

    private static (Game Game, GameState State, TaskForce Fleet, ScriptedRng Rng) Scenario(
        int x = 0, int y = 0, int targetX = 3, int targetY = 4, int velocity = 1, int range = 5) {
        var state = new GameState(new GameConfig { NumStars = 3 });
        var points = new[] { (x, y), (targetX, targetY), (14, 14) };
        for (int i = 0; i < points.Length; i++) {
            var star = state.Stars[i];
            (star.X, star.Y) = points[i];
            state.Board[star.X, star.Y].Star = i;
            state.Board[star.X, star.Y].StarRef = star;
            star.ExploredBy[1] = true;
        }
        state.Players[1].Velocity = velocity;
        state.Players[1].Range = range;
        var rng = new ScriptedRng();
        var game = new Game(rng, state);
        var fleet = state.FleetsSys.Place(1, 1, 0);
        return (game, state, fleet, rng);
    }

    // Independent transcription of input.c dist, movement.c set_des/move_ships,
    // and utils.c round. No production movement helpers are used as the oracle.
    private static int CTravelTime(int dx, int dy, int velocity) {
        double r = Math.Sqrt(Math.Abs(dx) * Math.Abs(dx) + Math.Abs(dy) * Math.Abs(dy));
        int dst = (int)(r - 0.049 + 1);
        return r < 0.5 ? 0 : ((dst - 1) / velocity) + 1;
    }

    private static int CPosition(int from, int to, int eta, int originalEta) {
        double ratio = 1.0 - (double)eta / originalEta;
        double offset = ratio * (to - from);
        return from + (offset < 0 ? (int)(offset - 0.5) : (int)(offset + 0.5));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    [InlineData(9)] [InlineData(10)] [InlineData(11)] [InlineData(12)]
    public void AllBoardDisplacements_MatchCReferenceAtEveryTick(int velocity) {
        for (int dx = -14; dx <= 14; dx++)
            for (int dy = -14; dy <= 14; dy++) {
                int x = dx < 0 ? 14 : 0, y = dy < 0 ? 14 : 0;
                var (game, state, fleet, rng) = Scenario(x, y, x + dx, y + dy, velocity);
                int expected = CTravelTime(dx, dy, velocity);
                state.FleetsSys.SetCourse(1, 1, 1);
                Assert.Equal(0, game.Turn);
                Assert.Equal(expected, fleet.ETA);
                if (expected == 0) {
                    Assert.Equal(0, fleet.Destination);
                    continue;
                }
                Assert.Equal((x, y), (fleet.DepartureX, fleet.DepartureY));
                for (int remaining = expected - 1; remaining >= 0; remaining--) {
                    game.Tick();
                    Assert.Equal(remaining, fleet.ETA);
                    Assert.Equal(expected, fleet.EtaOriginal);
                    Assert.Equal((CPosition(x, x + dx, remaining, expected), CPosition(y, y + dy, remaining, expected)),
                        (fleet.X, fleet.Y));
                    Assert.Same(fleet, state.Board[fleet.X, fleet.Y].TaskForces[1]);
                }
                Assert.Equal(1, fleet.Destination);
                Assert.Equal(expected, game.Turn);
                game.Tick();
                Assert.Equal((x + dx, y + dy, 0), (fleet.X, fleet.Y, fleet.ETA));
                rng.AssertExhausted();
            }
    }

    [Theory]
    [InlineData(3, 4, 5, true)] // Inclusive integer boundary.
    [InlineData(3, 4, 4, false)]
    [InlineData(10, 1, 10, false)] // sqrt(101) - 10 > .049.
    [InlineData(11, 1, 11, true)] // sqrt(122) - 11 < .049: not ceiling(distance).
    [InlineData(5, 1, 5, false)]
    public void Range_UsesOriginalTolerance_ForEveryNonScoutType(int x, int y, int range, bool allowed) {
        foreach (string ship in new[] { "transport", "cruiser", "battlestar" }) {
            var (_, state, fleet, _) = Scenario(targetX: x, targetY: y, range: range);
            fleet.Scouts = 2; // Mixed fleets do not receive the scout exception.
            if (ship == "transport") fleet.Transports = 1;
            if (ship == "cruiser") fleet.Cruisers = 1;
            if (ship == "battlestar") fleet.BattleStars = 1;
            if (allowed) {
                state.FleetsSys.SetCourse(1, 1, 1);
                Assert.Equal(1, fleet.Destination);
            }
            else {
                Assert.Contains("Maximum range", Assert.Throws<InvalidOperationException>(() => state.FleetsSys.SetCourse(1, 1, 1)).Message);
                Assert.Equal((0, 0, 0), (fleet.X, fleet.Y, fleet.ETA));
                Assert.Equal(0, fleet.Destination);
            }
        }
    }

    [Fact]
    public void ReissueBeforeDeparture_CanChangeOrCancel_ButNotAfterMovement() {
        var (game, state, fleet, _) = Scenario();
        state.FleetsSys.SetCourse(1, 1, 1);
        state.FleetsSys.SetCourse(1, 1, 2); // Scouts ignore range.
        Assert.Equal(20, fleet.ETA);
        fleet.Cruisers = 1;
        Assert.Throws<InvalidOperationException>(() => state.FleetsSys.SetCourse(1, 1, 2));
        Assert.Equal(0, fleet.Destination); // C cancels previous orders before rejecting range.
        Assert.Equal(0, fleet.ETA);
        state.FleetsSys.SetCourse(1, 1, 1);
        state.FleetsSys.SetCourse(1, 1, 0); // Cancels to the current star.
        Assert.Equal(0, fleet.ETA);
        Assert.Equal(0, game.Turn);
        state.FleetsSys.SetCourse(1, 1, 1);
        game.Tick();
        var before = (fleet.X, fleet.Y, fleet.Destination, fleet.ETA, fleet.EtaOriginal);
        Assert.Throws<InvalidOperationException>(() => state.FleetsSys.SetCourse(1, 1, 0));
        Assert.Equal(before, (fleet.X, fleet.Y, fleet.Destination, fleet.ETA, fleet.EtaOriginal));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void WithdrawalAndBombardment_BlockOrders(bool withdrawal) {
        var (_, state, fleet, _) = Scenario();
        state.FleetsSys.SetCourse(1, 1, 1);
        if (withdrawal) fleet.Withdrew = 1;
        else fleet.Blasting = 1;
        Assert.Throws<InvalidOperationException>(() => state.FleetsSys.SetCourse(1, 1, 0));
        Assert.Equal(1, fleet.Destination);
        Assert.Equal(5, fleet.ETA);
    }

    [Fact]
    public void Arrival_UsesCExplorationDrawOrderSharedProbabilityAndStrictComparison() {
        var (game, state, fleet, rng) = Scenario(targetX: 1, targetY: 0);
        var target = state.Stars[1];
        target.ExploredBy[1] = false;
        var planet = new Planet(target) { Capacity = 40 };
        target.Planets.Add(planet);
        fleet.Transports = fleet.Scouts = fleet.Cruisers = fleet.BattleStars = 1;
        rng.Add(5, 1);
        rng.Add(10, 1, 1);
        rng.Add(3, 1); // Last type overwrites probability: (97 + 1*1)/100 = .98.
        rng.Add(100, 98, 99, 98, 100); // Equality survives, greater loses.
        state.FleetsSys.SetCourse(1, 1, 1);
        game.Tick(3);
        Assert.Equal((1, 0, 1, 0), (fleet.Transports, fleet.Scouts, fleet.Cruisers, fleet.BattleStars));
        Assert.True(target.ExploredBy[1]);
        Assert.Equal(40, planet.PlayerSeenCapacity);
        Assert.Equal(1, fleet.Destination);
        Assert.Equal(3, game.Turn);
        rng.AssertExhausted(); // Subsequent stationary ticks consume no draws.
    }

    [Fact]
    public void Exploration_AllShipsLost_RemovesFleetWithoutVisitingStar() {
        var (game, state, fleet, rng) = Scenario(targetX: 1, targetY: 0);
        state.Stars[1].ExploredBy[1] = false;
        fleet.Scouts = 1;
        rng.Add(10, 1);
        rng.Add(100, 72); // probability .71.
        state.FleetsSys.SetCourse(1, 1, 1);
        game.Tick();
        Assert.Null(fleet.Destination);
        Assert.Equal(0, fleet.Scouts);
        Assert.False(state.Stars[1].ExploredBy[1]);
        Assert.Null(state.Board[1, 0].TaskForces[1]);
        rng.AssertExhausted();
    }

    [Fact]
    public void Exploration_ProbabilityAboveOneStillConsumesAllShipDraws() {
        var (game, state, fleet, rng) = Scenario(targetX: 1, targetY: 0);
        state.Stars[1].ExploredBy[1] = false;
        fleet.Scouts = 4;
        rng.Add(10, 10); // (70+10*4)/100 = 1.10.
        rng.Add(100, 100, 100, 100, 100);
        state.FleetsSys.SetCourse(1, 1, 1);
        game.Tick();
        Assert.Equal(4, fleet.Scouts);
        rng.AssertExhausted();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void SafeOrKnownArrival_UsesNoRandomness_AndUpdatesPlanetIntelligence(bool safe) {
        var (game, state, fleet, rng) = Scenario(targetX: 1, targetY: 0);
        state.Config.SafeExplore = safe;
        state.Stars[1].ExploredBy[1] = !safe;
        var planet = new Planet(state.Stars[1]) { Capacity = 60, PlayerSeenCapacity = 10 };
        state.Stars[1].Planets.Add(planet);
        state.FleetsSys.SetCourse(1, 1, 1);
        game.Tick();
        Assert.Equal(60, planet.PlayerSeenCapacity);
        Assert.True(state.Stars[1].ExploredBy[1]);
        Assert.Equal(3, fleet.Scouts);
        rng.AssertExhausted();
    }

    [Fact]
    public void FleetTickWithoutRng_RejectsUnknownArrivalBeforeMutation() {
        var (_, state, fleet, _) = Scenario(targetX: 1, targetY: 0);
        state.Stars[1].ExploredBy[1] = false;
        state.FleetsSys.SetCourse(1, 1, 1);
        Assert.Throws<InvalidOperationException>(() => state.FleetsSys.Tick());
        Assert.Equal((0, 0, 1), (fleet.X, fleet.Y, fleet.ETA));
    }

    [Fact]
    public void ColocatedAndCrossingFleets_PreserveBoardOccupancy() {
        var (game, state, first, _) = Scenario(targetX: 4, targetY: 0, velocity: 2);
        var stationary = state.FleetsSys.Place(1, 2, 0);
        var opposite = state.FleetsSys.Place(1, 3, 1);
        state.FleetsSys.SetCourse(1, 1, 1);
        state.FleetsSys.SetCourse(1, 3, 0);
        game.Tick();
        Assert.Same(stationary, state.Board[0, 0].TaskForces[1]);
        Assert.Equal((2, 0), (first.X, first.Y));
        Assert.Equal((2, 0), (opposite.X, opposite.Y));
        Assert.NotNull(state.Board[2, 0].TaskForces[1]);
        game.Tick();
        Assert.Same(first, state.Board[4, 0].TaskForces[1]);
        Assert.Equal(0, opposite.Destination);
        Assert.Equal(0, stationary.Destination);
    }

    [Fact]
    public void EnemyMovement_CountsDownWithoutInterpolation_VisitsAndJoinsOnArrival() {
        var (game, state, _, rng) = Scenario();
        var parent = state.FleetsSys.Place(0, 2, 1, scouts: 4);
        var moving = state.FleetsSys.Place(0, 1, 0, scouts: 2);
        moving.Destination = 1;
        moving.ETA = moving.EtaOriginal = 2; // Enemy orders are an AI responsibility.
        var planet = new Planet(state.Stars[1]) { Owner = Ownership.Player };
        state.Stars[1].Planets.Add(planet);
        game.Tick();
        Assert.Equal((0, 0, 1), (moving.X, moving.Y, moving.ETA));
        game.Tick();
        Assert.Null(parent.Destination);
        Assert.Equal(1, moving.Destination);
        Assert.Equal(6, moving.Scouts);
        Assert.True(state.Stars[1].ExploredBy[0]);
        Assert.Equal(Ownership.Player, planet.EnemySeenOwner);
        rng.AssertExhausted();
    }

    [Theory]
    [InlineData("a", "B")] [InlineData("A", "b")] [InlineData("1", "1")]
    public void Cli_ShowsDepartureTravelArrivalAndStationaryAtStarZero(string fleetId, string starId) {
        var (game, state, fleet, rng) = Scenario(targetX: 3, targetY: 0);
        var io = new DummyIO(new[] { $"move {fleetId} {starId}", "tf list", "tick 3", "tf list", "course a A", "tick 3", "tf list", "quit" });
        new Conquest.CLI.GameShell(game, io, rng, new SpyRenderer()).Run();
        Assert.Contains(io.OutputLines, line => line.Contains("travelling -> star 1 (B) (ETA 3)"));
        Assert.Contains(io.OutputLines, line => line.Contains("TF 1 arrived at star 1"));
        Assert.Contains(io.OutputLines, line => line.Contains("@ star 1 (B) stationary"));
        Assert.Contains(io.OutputLines, line => line.Contains("@ star 0 (A) stationary"));
        Assert.Equal(0, fleet.Destination);
        Assert.Equal(6, game.Turn);
    }

    [Fact]
    public void MultipleUnknownArrivals_ExploreOnceInFleetOrder() {
        var (game, state, first, rng) = Scenario(targetX: 1, targetY: 0);
        state.Stars[1].ExploredBy[1] = false;
        var second = state.FleetsSys.Place(1, 2, 0, scouts: 1);
        first.Scouts = 1;
        rng.Add(10, 1);
        rng.Add(100, 71); // First fleet survives and visits; second uses no RNG.
        state.FleetsSys.SetCourse(1, 1, 1);
        state.FleetsSys.SetCourse(1, 2, 1);
        game.Tick();
        Assert.Equal(1, first.Destination);
        Assert.Equal(1, second.Destination);
        Assert.Equal(1, second.Scouts);
        rng.AssertExhausted();
    }

    [Fact]
    public void Cli_DoesNotDescribeIntermediateStarAsStationaryLocation() {
        var (game, state, _, rng) = Scenario(targetX: 4, targetY: 0);
        (state.Stars[2].X, state.Stars[2].Y) = (2, 0);
        var io = new DummyIO(new[] { "move a B", "tick 2", "tf list", "quit" });
        new Conquest.CLI.GameShell(game, io, rng, new SpyRenderer()).Run();
        string listing = Assert.Single(io.OutputLines.Where(line => line.StartsWith("TF 1:")));
        Assert.Contains("(2,0)", listing);
        Assert.Contains("travelling -> star 1 (B) (ETA 2)", listing);
        Assert.DoesNotContain("@ star", listing);
    }

    [Fact]
    public void Cli_DestroyedExplorationFleet_IsReportedAndNoLongerListed() {
        var (game, state, fleet, rng) = Scenario(targetX: 1, targetY: 0);
        fleet.Scouts = 1;
        state.Stars[1].ExploredBy[1] = false;
        rng.Add(10, 1);
        rng.Add(100, 72);
        var io = new DummyIO(new[] { "move a B", "tick", "tf list", "quit" });
        new Conquest.CLI.GameShell(game, io, rng, new SpyRenderer()).Run();
        Assert.Contains(io.OutputLines, line => line.Contains("TF 1 destroyed exploring star 1"));
        Assert.DoesNotContain(io.OutputLines, line => line.StartsWith("TF 1:"));
        rng.AssertExhausted();
    }

    [Fact]
    public void BatchedTicks_MatchIndividualTicks_AfterOrdersAndArrival() {
        var first = Scenario(velocity: 2);
        var second = Scenario(velocity: 2);
        first.State.FleetsSys.SetCourse(1, 1, 1);
        second.State.FleetsSys.SetCourse(1, 1, 1);
        first.Game.Tick(5);
        for (int i = 0; i < 5; i++) second.Game.Tick();
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(first.Fleet), System.Text.Json.JsonSerializer.Serialize(second.Fleet));
        Assert.Equal(5, first.Game.Turn);
        Assert.Equal(second.Game.Turn, first.Game.Turn);
    }
}
