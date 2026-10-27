using System.Text.Json;
using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.Tests;

public sealed class PlayerStartupTests {
    private sealed class ScriptedRng : IRng {
        private readonly Queue<(int Min, int Max, int Value)> _draws = new();
        public int Calls { get; private set; }
        public int? FailOnCall { get; set; }
        public void Add(int min, int max, int value) => _draws.Enqueue((min, max, value));
        public int Next(int minInclusive, int maxExclusive) {
            Calls++;
            if (Calls == FailOnCall) throw new InvalidOperationException("Scripted RNG failure");
            Assert.NotEmpty(_draws);
            var draw = _draws.Dequeue();
            Assert.Equal((draw.Min, draw.Max), (minInclusive, maxExclusive));
            Assert.InRange(draw.Value, minInclusive, maxExclusive - 1);
            return draw.Value;
        }
        public void Reseed(int seed) => throw new NotSupportedException();
        public void AssertExhausted() => Assert.Empty(_draws);
    }

    private static (Game Game, ScriptedRng Rng) PreparedGame(
        int money = 30, int capacity = 0, int transports = 35, bool initializeEnemy = true) {
        var rng = new ScriptedRng();
        var state = new GameState(new GameConfig {
            NumStars = 3, StartingMoney = money, Capacity = capacity, StartingTransports = transports
        });
        var positions = new[] { (0, 0), (7, 0), (14, 14) };
        for (int i = 0; i < positions.Length; i++) {
            rng.Add(0, 15, positions[i].Item1);
            rng.Add(0, 15, positions[i].Item2);
            rng.Add(1, 5, i == 0 ? 3 : 2);
            if (i == 0) {
                rng.Add(1, 3, 1);
                rng.Add(1, 5, 4);
                rng.Add(1, 5, 3); // Star 0 has a capacity-50 planet; others are planetless.
            }
        }
        var game = new Game(rng, state);
        game.GenerateStars();
        rng.Add(1, 4, 2);
        rng.Add(1, 4, 3);
        game.InitializeClassicPlayers();
        if (initializeEnemy) {
            rng.Add(1, 11, 1); // Weapons selected; no velocity selection draw.
            rng.Add(1, 6, 1);
            rng.Add(1, 6, 1);
            rng.Add(1, 6, 1); // Enemy chooses star 0 on the first tied maximum.
            game.InitializeClassicEnemy();
            Assert.Equal(18, rng.Calls);
        }
        rng.AssertExhausted();
        return (game, rng);
    }

    private static void StartSpending(Game game, int star = 1) {
        game.BeginClassicPlayerStartup();
        game.SelectClassicPlayerStartingStar(star);
    }

    [Theory]
    [InlineData(0)] // Enemy's star, valid (0,0) coordinates.
    [InlineData(1)] // Planetless.
    [InlineData(2)] // Last star, unreachable from the enemy's start.
    public void ManualSelection_PreservesStateUntilSpendingFinishes(int index) {
        var (game, rng) = PreparedGame(transports: 17);
        var state = game.State;
        state.FogEnabled = true;
        state.Discover(14, 14);
        var before = UnchangedWorld(state);
        var enemy = state.Board[0, 0].TaskForces[0];
        game.BeginClassicPlayerStartup();
        Assert.Equal(PlayerStartupStage.SelectingStar, state.PlayerStartupStage);
        Assert.Null(state.PlayerStartingStar);
        Assert.Equal(30, state.PlayerStartupBalance);
        new Game(rng, state).SelectClassicPlayerStartingStar(index);

        var fleet = state.Players[1].Tf[1];
        var star = state.Stars[index];
        Assert.Equal(index, fleet.Destination);
        Assert.Equal(index, state.PlayerStartingStar);
        Assert.Equal((star.X, star.Y), (fleet.X, fleet.Y));
        Assert.Equal(0, fleet.ETA);
        Assert.Equal(0, fleet.EtaOriginal);
        Assert.Equal(17, fleet.Transports);
        Assert.Equal(0, fleet.Cruisers + fleet.Scouts + fleet.BattleStars);
        Assert.Same(fleet, state.Board[star.X, star.Y].TaskForces[1]);
        Assert.Equal(-1, state.Board[star.X, star.Y].Tf);
        Assert.All(state.Stars, s => Assert.False(s.ExploredBy[1]));
        Assert.False(state.PendingSharedStartEncounter);
        Assert.Equal(before, UnchangedWorld(state));
        Assert.True(new Game(rng, state).PurchaseClassicStartupShips(StartupShip.Scout, 5));
        Assert.All(state.Stars, s => Assert.False(s.ExploredBy[1]));
        new Game(rng, state).FinalizeClassicPlayerStartup();

        Assert.Equal(PlayerStartupStage.Completed, state.PlayerStartupStage);
        Assert.Equal(0, state.PlayerStartupBalance);
        Assert.Equal(index == 0, state.PendingSharedStartEncounter);
        Assert.Same(enemy, state.Board[0, 0].TaskForces[0]);
        Assert.Equal(1, state.Board[star.X, star.Y].Tf);
        Assert.Equal((int)' ', state.Board[star.X, star.Y].Status);
        for (int i = 0; i < state.Stars.Count; i++) Assert.Equal(i == index, state.Stars[i].ExploredBy[1]);
        Assert.Equal(before, UnchangedWorld(state));
        Assert.Equal(18, rng.Calls);
        Assert.Equal(0, game.Turn);
        rng.AssertExhausted();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(99)]
    public void InvalidManualChoice_IsRecoverableAndDoesNotMutate(int index) {
        var (game, rng) = PreparedGame();
        game.BeginClassicPlayerStartup();
        AssertRejectedUnchanged(game, rng, () => game.SelectClassicPlayerStartingStar(index), typeof(ArgumentOutOfRangeException));
        game.SelectClassicPlayerStartingStar(1);
        Assert.Equal(PlayerStartupStage.Spending, game.State.PlayerStartupStage);
    }

    [Fact]
    public void AutomaticSelection_UsesRepeatedSamplesAfterEnemyStartup() {
        var (game, rng) = PreparedGame(capacity: 50);
        rng.Add(1, 4, 2); // No planets.
        rng.Add(1, 4, 3); // No planets.
        rng.Add(1, 4, 1); // Exact capacity boundary; sharing the enemy's star is allowed.
        game.BeginClassicPlayerStartup();
        Assert.Equal(0, game.State.PlayerStartingStar);
        Assert.Equal(PlayerStartupStage.Spending, game.State.PlayerStartupStage);
        Assert.Equal(21, rng.Calls);
        Assert.Equal(50, game.State.Config.Capacity);
        rng.AssertExhausted();
        AssertRejectedUnchanged(game, rng, () => game.SelectClassicPlayerStartingStar(1));
    }

    [Theory]
    [InlineData(60, 1, 0)] // 50-capacity planet becomes eligible after 60 -> 50.
    [InlineData(10, 2, 1)] // Threshold reaches zero: even a planetless star is eligible.
    public void CapacityRelaxation_UsesNPlusTwoFailures_ThenSamplesAgain(int capacity, int sample, int expected) {
        var (game, rng) = PreparedGame(capacity: capacity);
        // N=3: the fifth sample fails before threshold relaxation, sixth can succeed.
        for (int i = 0; i < 6; i++) rng.Add(1, 4, sample);
        game.BeginClassicPlayerStartup();
        Assert.Equal(expected, game.State.PlayerStartingStar);
        Assert.Equal(24, rng.Calls);
        Assert.Equal(capacity, game.State.Config.Capacity);
        rng.AssertExhausted();
    }

    [Fact]
    public void CapacitySelection_RequiresOneQualifyingPlanet_NotSummedCapacity() {
        var (game, rng) = PreparedGame(capacity: 30);
        var star = game.State.Stars[0];
        star.Planets[0].Capacity = 20;
        star.Planets.Add(new Planet(star) { Number = 3, Capacity = 20, PlayerSeenCapacity = 20 });
        for (int i = 0; i < 6; i++) rng.Add(1, 4, 1);
        game.BeginClassicPlayerStartup();
        Assert.Equal(0, game.State.PlayerStartingStar);
        Assert.Equal(24, rng.Calls);
        rng.AssertExhausted();
    }

    [Theory]
    [InlineData(StartupShip.Cruiser, 2, 32)]
    [InlineData(StartupShip.Scout, 5, 30)]
    [InlineData(StartupShip.Battlestar, 1, 70)]
    public void ShipPurchases_UseOriginalCosts(StartupShip ship, int quantity, int cost) {
        var (game, rng) = PreparedGame(money: 70);
        StartSpending(game);
        Assert.True(game.PurchaseClassicStartupShips(ship, quantity));
        var fleet = game.State.Players[1].Tf[1];
        Assert.Equal(ship == StartupShip.Cruiser ? quantity : 0, fleet.Cruisers);
        Assert.Equal(ship == StartupShip.Scout ? quantity : 0, fleet.Scouts);
        Assert.Equal(ship == StartupShip.Battlestar ? quantity : 0, fleet.BattleStars);
        Assert.Equal(70 - cost, game.State.PlayerStartupBalance);
        Assert.Equal(35, fleet.Transports);
        Assert.Equal(18, rng.Calls);
    }

    [Fact]
    public void Spending_IsSharedAcrossFacades_AndUnaffordableRequestsHaveNoPartialEffects() {
        var (game, rng) = PreparedGame();
        StartSpending(game);
        string before = FullSnapshot(game);
        Assert.False(game.PurchaseClassicStartupShips(StartupShip.Cruiser, 2));
        Assert.False(game.InvestClassicStartupResearch(ResearchField.Range, 31));
        Assert.Equal(before, FullSnapshot(game));
        Assert.True(game.PurchaseClassicStartupShips(StartupShip.Cruiser));
        Assert.Equal(14, game.State.PlayerStartupBalance);
        var other = new Game(rng, game.State);
        Assert.True(other.PurchaseClassicStartupShips(StartupShip.Scout, 2));
        Assert.Equal(2, game.State.PlayerStartupBalance);
        AssertRejectedUnchanged(game, rng, game.FinalizeClassicPlayerStartup);
        Assert.True(other.InvestClassicStartupResearch(ResearchField.Range, 2));
        other.FinalizeClassicPlayerStartup();
        Assert.Equal(2, game.State.Players[1].RangeResearchProgress);
        Assert.Equal(18, rng.Calls);
    }

    [Theory]
    [InlineData(ResearchField.Weapons, 49, 3, 49)]
    [InlineData(ResearchField.Weapons, 50, 4, 0)]
    [InlineData(ResearchField.Weapons, 51, 4, 1)]
    [InlineData(ResearchField.Weapons, 217, 6, 7)]
    [InlineData(ResearchField.Velocity, 39, 1, 39)]
    [InlineData(ResearchField.Velocity, 40, 2, 0)]
    [InlineData(ResearchField.Velocity, 41, 2, 1)]
    [InlineData(ResearchField.Velocity, 187, 4, 7)]
    [InlineData(ResearchField.Range, 19, 5, 19)]
    [InlineData(ResearchField.Range, 20, 6, 0)]
    [InlineData(ResearchField.Range, 21, 6, 1)]
    [InlineData(ResearchField.Range, 30, 6, 10)]
    [InlineData(ResearchField.Range, 137, 8, 7)]
    public void Research_UsesThresholdsAndMultiBreakthroughProgress(ResearchField field, int amount, int level, int progress) {
        var (game, rng) = PreparedGame(money: amount);
        StartSpending(game);
        string worldBefore = UnchangedWorld(game.State);
        Assert.True(game.InvestClassicStartupResearch(field, amount));
        AssertResearch(game.State.Players[1], field, level, progress);
        Assert.Equal(0, game.State.PlayerStartupBalance);
        Assert.Equal(worldBefore, UnchangedWorld(game.State));
        Assert.Equal(18, rng.Calls);
    }

    [Theory]
    [InlineData(ResearchField.Weapons, 1080, 10)]
    [InlineData(ResearchField.Range, 2780, 15)]
    [InlineData(ResearchField.Velocity, 2700, 12)]
    public void CappedResearch_ChargesFullAmount_WithAndWithoutCrossingCap(ResearchField field, int required, int maximum) {
        var (game, rng) = PreparedGame(money: required + 200);
        StartSpending(game);
        Assert.True(game.InvestClassicStartupResearch(field, required + 100));
        AssertResearch(game.State.Players[1], field, maximum, 0);
        Assert.Equal(100, game.State.PlayerStartupBalance);
        Assert.True(game.InvestClassicStartupResearch(field, 100));
        AssertResearch(game.State.Players[1], field, maximum, 0);
        Assert.Equal(0, game.State.PlayerStartupBalance);
        game.FinalizeClassicPlayerStartup();
        Assert.Equal(18, rng.Calls);
    }

    [Fact]
    public void ZeroBudget_CanFinalizeWithoutPurchase_AndZeroAmountsAreHarmless() {
        var (game, rng) = PreparedGame(money: 0);
        StartSpending(game);
        string before = FullSnapshot(game);
        Assert.True(game.PurchaseClassicStartupShips(StartupShip.Scout, 0));
        Assert.True(game.InvestClassicStartupResearch(ResearchField.Range, 0));
        Assert.Equal(before, FullSnapshot(game));
        game.FinalizeClassicPlayerStartup();
        Assert.Equal(PlayerStartupStage.Completed, game.State.PlayerStartupStage);
        game.Tick(); // Distinct starts can advance with existing scaffold mechanics.
        Assert.Equal(1, game.Turn);
        Assert.Equal(18, rng.Calls);
    }

    public static IEnumerable<object[]> ResearchTables() {
        yield return new object[] { ResearchField.Weapons, 3, new[] { 50, 70, 90, 120, 150, 250, 350 } };
        yield return new object[] { ResearchField.Range, 5, new[] { 20, 40, 70, 100, 150, 200, 300, 400, 600, 900 } };
        yield return new object[] { ResearchField.Velocity, 1, new[] { 40, 60, 80, 120, 150, 200, 250, 300, 400, 500, 600 } };
    }

    [Theory]
    [MemberData(nameof(ResearchTables))]
    public void Research_EveryOriginalThreshold_ResetsProgress(ResearchField field, int initial, int[] requirements) {
        var (game, rng) = PreparedGame(money: requirements.Sum());
        StartSpending(game);
        int level = initial;
        foreach (int requirement in requirements) {
            Assert.True(game.InvestClassicStartupResearch(field, requirement - 1));
            AssertResearch(game.State.Players[1], field, level, requirement - 1);
            Assert.True(new Game(rng, game.State).InvestClassicStartupResearch(field, 1));
            AssertResearch(game.State.Players[1], field, ++level, 0);
        }
        Assert.Equal(0, game.State.PlayerStartupBalance);
        game.FinalizeClassicPlayerStartup();
        Assert.Equal(18, rng.Calls);
    }

    [Fact]
    public void MaximumRepresentableBudget_ResearchDoesNotOverflowOrRefundExcess() {
        var (game, rng) = PreparedGame(money: int.MaxValue);
        StartSpending(game);
        Assert.True(game.InvestClassicStartupResearch(ResearchField.Weapons, int.MaxValue));
        AssertResearch(game.State.Players[1], ResearchField.Weapons, 10, 0);
        Assert.Equal(0, game.State.PlayerStartupBalance);
        game.FinalizeClassicPlayerStartup();
        Assert.Equal(18, rng.Calls);
    }

    [Theory]
    [InlineData("money")]
    [InlineData("capacity")]
    [InlineData("transports")]
    public void MalformedConfiguration_IsRejectedWithoutMutationOrDraws(string setting) {
        var (game, rng) = PreparedGame(money: setting == "money" ? -1 : 30,
            capacity: setting == "capacity" ? -1 : 0, transports: setting == "transports" ? -1 : 35);
        if (setting == "capacity") {
            Assert.Equal(0, game.State.Config.Capacity); // Existing non-Classic behavior remains.
            game.State.Config.Validate(); // Revalidation must not erase the malformed request.
        }
        AssertRejectedUnchanged(game, rng, game.BeginClassicPlayerStartup, typeof(ArgumentOutOfRangeException));
    }

    [Theory]
    [InlineData("negative ships")]
    [InlineData("negative research")]
    [InlineData("overflow")]
    [InlineData("ship enum")]
    [InlineData("research enum")]
    public void MalformedPurchase_IsRejectedWithoutMutation(string setting) {
        var (game, rng) = PreparedGame();
        StartSpending(game);
        Action operation = setting switch {
            "negative ships" => () => game.PurchaseClassicStartupShips(StartupShip.Scout, -1),
            "negative research" => () => game.InvestClassicStartupResearch(ResearchField.Range, -1),
            "overflow" => () => game.PurchaseClassicStartupShips(StartupShip.Battlestar, int.MaxValue),
            "ship enum" => () => game.PurchaseClassicStartupShips((StartupShip)99),
            _ => () => game.InvestClassicStartupResearch((ResearchField)99, 1)
        };
        AssertRejectedUnchanged(game, rng, operation,
            setting == "overflow" ? typeof(OverflowException) : typeof(ArgumentOutOfRangeException));
    }

    [Fact]
    public void InvalidLifecycle_IsRejectedAcrossSharedFacades() {
        var (game, rng) = PreparedGame(initializeEnemy: false);
        AssertRejectedUnchanged(game, rng, game.BeginClassicPlayerStartup);
        rng.Add(1, 11, 1);
        for (int i = 0; i < 3; i++) rng.Add(1, 6, 1);
        game.InitializeClassicEnemy();
        AssertRejectedUnchanged(game, rng, () => game.SelectClassicPlayerStartingStar(1));
        AssertRejectedUnchanged(game, rng, () => game.PurchaseClassicStartupShips(StartupShip.Scout));
        AssertRejectedUnchanged(game, rng, game.FinalizeClassicPlayerStartup);
        game.BeginClassicPlayerStartup();
        AssertRejectedUnchanged(game, rng, () => new Game(rng, game.State).BeginClassicPlayerStartup());
        AssertRejectedUnchanged(game, rng, () => game.InvestClassicStartupResearch(ResearchField.Range, 1));
        game.SelectClassicPlayerStartingStar(1);
        AssertRejectedUnchanged(game, rng, () => new Game(rng, game.State).SelectClassicPlayerStartingStar(2));
        game.PurchaseClassicStartupShips(StartupShip.Scout, 5);
        game.FinalizeClassicPlayerStartup();
        AssertRejectedUnchanged(game, rng, () => new Game(rng, game.State).BeginClassicPlayerStartup());
        AssertRejectedUnchanged(game, rng, () => game.PurchaseClassicStartupShips(StartupShip.Scout, 0));
        AssertRejectedUnchanged(game, rng, game.FinalizeClassicPlayerStartup);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void AutomaticSelectionFailure_IsTerminalAcrossFacades(int failureDraw) {
        var (game, rng) = PreparedGame(capacity: 50);
        rng.Add(1, 4, 2);
        rng.FailOnCall = rng.Calls + failureDraw;
        Assert.Throws<InvalidOperationException>(game.BeginClassicPlayerStartup);
        Assert.Equal(PlayerStartupStage.Failed, game.State.PlayerStartupStage);
        Assert.Null(game.State.PlayerStartingStar);
        Assert.Null(game.State.Players[1].Tf[1].Destination);
        Assert.All(game.State.Board.Cast<Sector>(), c => Assert.Null(c.TaskForces[1]));
        AssertRejectedUnchanged(game, rng, () => new Game(rng, game.State).BeginClassicPlayerStartup());
        AssertRejectedUnchanged(game, rng, () => game.SelectClassicPlayerStartingStar(0));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void FailedEnemyInitialization_DoesNotPermitPlayerStartup(int failureDraw) {
        var (game, rng) = PreparedGame(initializeEnemy: false);
        rng.Add(1, 11, 1);
        for (int i = 0; i < 3; i++) rng.Add(1, 6, 1);
        rng.FailOnCall = rng.Calls + failureDraw;
        Assert.Throws<InvalidOperationException>(game.InitializeClassicEnemy);
        AssertRejectedUnchanged(game, rng, () => new Game(rng, game.State).BeginClassicPlayerStartup());
        Assert.Equal(PlayerStartupStage.NotStarted, game.State.PlayerStartupStage);
    }

    [Theory]
    [InlineData("before begin")]
    [InlineData("before selection")]
    [InlineData("before purchase")]
    [InlineData("before finalization")]
    public void DebugFleetActivity_CannotBeOverwrittenByStartup(string when) {
        var (game, rng) = PreparedGame(money: 0);
        if (when != "before begin") game.BeginClassicPlayerStartup();
        if (when is "before purchase" or "before finalization") game.SelectClassicPlayerStartingStar(1);
        if (when == "before selection") game.State.FleetsSys.SetCourse(1, 26, 1);
        else game.State.FleetsSys.Place(1, 1, 2);
        Action operation = when switch {
            "before begin" => game.BeginClassicPlayerStartup,
            "before selection" => () => game.SelectClassicPlayerStartingStar(1),
            "before purchase" => () => game.PurchaseClassicStartupShips(StartupShip.Scout, 0),
            _ => game.FinalizeClassicPlayerStartup
        };
        AssertRejectedUnchanged(game, rng, operation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdvancedState_CannotBeginStartupThroughAnotherFacade(bool directFleetTick) {
        var (game, rng) = PreparedGame();
        if (directFleetTick) game.State.FleetsSys.Tick();
        else game.Tick();
        AssertRejectedUnchanged(game, rng, () => new Game(rng, game.State).BeginClassicPlayerStartup());
    }

    [Theory]
    [InlineData("game")]
    [InlineData("shared game")]
    [InlineData("fleets")]
    [InlineData("new fleets")]
    public void ActiveStartupAndPendingEncounter_BlockEveryTickEntryPoint(string path) {
        var (game, rng) = PreparedGame(money: 0);
        game.BeginClassicPlayerStartup();
        Action advance = path switch {
            "game" => () => game.Tick(),
            "shared game" => () => new Game(rng, game.State).Tick(),
            "fleets" => () => game.State.FleetsSys.Tick(),
            _ => () => new FleetSystem(game.State).Tick()
        };
        AssertRejectedUnchanged(game, rng, advance);
        game.SelectClassicPlayerStartingStar(0);
        AssertRejectedUnchanged(game, rng, advance);
        game.FinalizeClassicPlayerStartup();
        Assert.True(game.State.PendingSharedStartEncounter);
        AssertRejectedUnchanged(game, rng, advance);
        // Existing debug relocation cannot clear the encounter and thereby skip combat.
        game.State.FleetsSys.Place(1, 1, 2);
        Assert.True(game.State.PendingSharedStartEncounter);
        AssertRejectedUnchanged(game, rng, advance);
    }

    private static void AssertResearch(Player player, ResearchField field, int level, int progress) {
        Assert.Equal(field == ResearchField.Weapons ? level : 3, player.Weapons);
        Assert.Equal(field == ResearchField.Range ? level : 5, player.Range);
        Assert.Equal(field == ResearchField.Velocity ? level : 1, player.Velocity);
        Assert.Equal(field == ResearchField.Weapons ? progress : 0, player.WeaponsResearchProgress);
        Assert.Equal(field == ResearchField.Range ? progress : 0, player.RangeResearchProgress);
        Assert.Equal(field == ResearchField.Velocity ? progress : 0, player.VelocityResearchProgress);
    }

    private static void AssertRejectedUnchanged(Game game, ScriptedRng rng, Action action, Type? exception = null) {
        string before = FullSnapshot(game);
        int calls = rng.Calls;
        Assert.Throws(exception ?? typeof(InvalidOperationException), action);
        Assert.Equal(before, FullSnapshot(game));
        Assert.Equal(calls, rng.Calls);
    }

    private static string FullSnapshot(Game game) => JsonSerializer.Serialize(new {
        game.Turn, game.State.PlayerStartupStage, game.State.PlayerStartupBalance,
        game.State.PlayerStartingStar, game.State.PendingSharedStartEncounter,
        game.State.Players,
        World = UnchangedWorld(game.State),
        Visits = game.State.Stars.Select(s => s.ExploredBy),
        Cells = game.State.Board.Cast<Sector>().Select(c => new { c.Tf, c.Status, c.TaskForces })
    });

    private static string UnchangedWorld(GameState state) => JsonSerializer.Serialize(new {
        Enemy = state.Players[0], state.EnemyResearchField,
        OtherFleets = state.Players[1].Tf.Skip(2),
        Stars = state.Stars.Select(s => new {
            s.Name, s.X, s.Y, EnemyVisit = s.ExploredBy[0],
            Planets = s.Planets.Select(p => new {
                p.Number, p.Capacity, p.Owner, p.Inhabitants, p.IndustrialUnits,
                p.MissileBases, p.AdvancedMissileBases, p.Conquered, p.UnderAttack,
                p.PlayerSeenCapacity, p.EnemySeenOwner, p.EnemySeenDefense
            })
        }),
        Board = state.Board.Cast<Sector>().Select(c => new {
            c.Star, c.ViewingTeam, EnemyFleet = c.TaskForces[0],
            StarIndex = c.StarRef == null ? -1 : state.Stars.IndexOf(c.StarRef)
        }),
        state.FogEnabled, state.RevealStars, Visible = state.Visible?.Cast<bool>().ToArray()
    });
}
