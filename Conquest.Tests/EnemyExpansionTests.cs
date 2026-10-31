using Conquest.CLI;
using Conquest.Core;
using Conquest.Core.Model;
using System.Text.Json;

namespace Conquest.Tests;

public sealed class EnemyExpansionTests {
    private sealed class Rng(params int[] values) : IRng {
        private readonly Queue<int> _values = new(values);
        public List<(int, int)> Bounds { get; } = [];
        public int Next(int minInclusive, int maxExclusive) {
            Bounds.Add((minInclusive, maxExclusive)); int value = _values.Dequeue();
            Assert.InRange(value, minInclusive, maxExclusive - 1); return value;
        }
        public void Reseed(int seed) => throw new NotSupportedException();
    }
    private sealed class Terminal(string keys) : IClassicTerminal {
        private readonly Queue<char> _keys = new(keys);
        public List<string[]> Screens { get; } = [];
        public int ReadKey() => _keys.TryDequeue(out char key) ? key : -1;
        public void Present(string[] screen) => Screens.Add(screen);
    }
    // Genuine initialization enables normal AI; controlled geometry isolates source decisions.
    private static Game Scenario(IRng rng, int seed = 1) {
        var game = new Game(new CRandRng(seed), new GameState(new GameConfig { NumStars = 4, StartingMoney = 0, SafeExplore = true }));
        game.GenerateStars(); game.InitializeClassicPlayers(); game.InitializeClassicEnemy(); game.BeginClassicPlayerStartup();
        int playerStar = game.State.Players[0].Tf[1].Destination == 3 ? 2 : 3;
        game.SelectClassicPlayerStartingStar(playerStar); game.FinalizeClassicPlayerStartup();
        foreach (var cell in game.State.Board) { cell.StarRef = null; cell.TaskForces[0] = cell.TaskForces[1] = null!; cell.Tf = -1; }
        for (int i = 0; i < 4; i++) {
            var star = game.State.Stars[i]; star.X = i == 3 ? 14 : i; star.Y = i == 3 ? 14 : 0;
            star.ExploredBy[0] = i == 0; star.ExploredBy[1] = i == 3; star.Planets.Clear();
            game.State.Board[star.X, star.Y].StarRef = star; game.State.Board[star.X, star.Y].Star = i;
            star.Planets.Add(new Planet(star) { Number = 1, Capacity = i == 3 ? 20 : 60, PlayerSeenCapacity = i == 3 ? 20 : 0 });
        }
        foreach (var p in game.State.Players) {
            p.Weapons = 3; p.Velocity = 1; p.Range = 7; p.PopulationGrowthRate = .5;
            p.WeaponsResearchProgress = p.RangeResearchProgress = p.VelocityResearchProgress = 0;
            foreach (var f in p.Tf.Skip(1)) { f.Destination = null; f.ETA = f.EtaOriginal = f.Transports = f.Scouts = f.Cruisers = f.BattleStars = 0; }
        }
        game.State.Players[1].PopulationGrowthRate = .3;
        game.State.FleetsSys.Place(1, 1, 3, scouts: 0).Transports = 35;
        return new Game(rng, game.State);
    }
    private static void Finish(Game game) {
        game.PrepareClassicCombat(); Assert.False(game.HasPendingClassicCombat);
        game.PrepareClassicEconomy();
        while (game.State.Investment is { } session) {
            Assert.Null(game.InvestInClassicColony('R', session.Balance)); game.FinishClassicInvestmentLine();
        }
        game.CompleteClassicTurn();
    }
    private static void Advance(Game game) { game.BeginClassicTurn(); Finish(game); }
    private static Planet EnemyColony(Game game, int star, int pop, int iu, int capacity = 60) {
        var p = game.State.Stars[star].Planets[0]; p.Owner = p.EnemySeenOwner = Ownership.Enemy;
        p.Capacity = capacity; p.Inhabitants = pop; p.IndustrialUnits = iu; return p;
    }

    [Fact]
    public void OriginalStartupHasNoColonyButCarriesTransportsTwoScoutsAndCruiserAndInitialResearch() {
        var game = new Game(new CRandRng(1), new GameState(new GameConfig()));
        game.GenerateStars(); game.InitializeClassicPlayers(); game.InitializeClassicEnemy(); var enemy = game.State.Players[0];
        Assert.All(game.State.Stars.SelectMany(s => s.Planets), p => { Assert.Equal(Ownership.None, p.Owner); Assert.Equal((0, 0), (p.Inhabitants, p.IndustrialUnits)); });
        Assert.Equal((35, 2, 1, 0), (enemy.Tf[1].Transports, enemy.Tf[1].Scouts, enemy.Tf[1].Cruisers, enemy.Tf[1].BattleStars));
        Assert.Equal(7, enemy.Range); Assert.Equal(2, enemy.VelocityResearchProgress); Assert.NotNull(game.State.EnemyResearchField);
        Assert.Single(game.State.Stars.Where(s => s.ExploredBy[0])); Assert.DoesNotContain(game.State.Stars, s => s.ExploredBy[1]);
    }

    [Fact]
    public void InputmachSendsScoutsThenLandsAndSplitsTransportsThenOrdersWarshipsBeforeMovement() {
        var rng = new Rng(1, 1, 1, 1, 1); var game = Scenario(rng);
        var fleet = game.State.FleetsSys.Place(0, 1, 0, scouts: 2); fleet.Transports = 35; fleet.Cruisers = 1;
        game.BeginClassicTurn(); var home = game.State.Stars[0].Planets[0];
        Assert.Equal((Ownership.Enemy, 20, 20), (home.Owner, home.Inhabitants, home.IndustrialUnits));
        Assert.Equal(0, home.PlayerSeenCapacity); Assert.False(game.State.Stars[0].ExploredBy[1]);
        var arrived = game.State.Players[0].Tf[4];
        Assert.Null(fleet.Destination); Assert.Null(game.State.Players[0].Tf[2].Destination);
        Assert.Equal((1, 0, 7, 1, 1), (arrived.Destination, arrived.ETA, arrived.Transports, arrived.Scouts, arrived.Cruisers));
        Assert.Equal((2, 1, 1), (game.State.Players[0].Tf[3].Destination, game.State.Players[0].Tf[3].ETA, game.State.Players[0].Tf[3].Scouts));
        Assert.Equal((2, 1, 8), (game.State.Players[0].Tf[5].Destination, game.State.Players[0].Tf[5].ETA, game.State.Players[0].Tf[5].Transports));
        Assert.Equal(new[] { (1, 3), (1, 2), (1, 21), (1, 21), (1, 21) }, rng.Bounds);
        Assert.Empty(game.State.VisibleEnemyArrivals); Assert.Empty(game.State.VisibleEnemyDepartures); Finish(game);
    }

    [Fact]
    public void RecursiveLandingUsesOneThirdRemainingRoomAndStopsWithTwoFreePlaces() {
        var game = Scenario(new Rng()); var p = game.State.Stars[0].Planets[0]; p.Capacity = 20;
        // Other planets are enemy-known player worlds, so the current neutral planet stays best.
        foreach (var star in game.State.Stars.Skip(1).Take(2)) { star.ExploredBy[0] = true; star.Planets[0].EnemySeenOwner = Ownership.Player; }
        var f = game.State.FleetsSys.Place(0, 1, 0, scouts: 0); f.Transports = 35;
        Advance(game); Assert.Equal((18, 18, 17), (p.Inhabitants, p.IndustrialUnits, f.Transports)); Assert.Equal(Ownership.Enemy, p.Owner);
        Assert.Equal(0, p.PlayerSeenCapacity); Assert.Equal(35, game.State.Players[1].Tf[1].Transports);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void EnemyCannotLandThroughPlayerFleetOrOnActualPlayerColony(bool playerFleet, bool playerPlanet) {
        var game = Scenario(new Rng()); var p = game.State.Stars[0].Planets[0];
        foreach (var star in game.State.Stars.Skip(1).Take(2)) { star.ExploredBy[0] = true; star.Planets[0].EnemySeenOwner = Ownership.Player; }
        if (playerFleet) game.State.FleetsSys.Place(1, 2, 0, scouts: 1);
        if (playerPlanet) p.Owner = Ownership.Player; // Enemy intelligence remains stale neutral.
        var f = game.State.FleetsSys.Place(0, 1, 0, scouts: 0); f.Transports = 10;
        Advance(game); Assert.Equal(10, f.Transports); Assert.Equal(0, p.Inhabitants);
    }

    [Fact]
    public void SecondBestPlanetAtSameStarStillCausesTwoConvoysAndEscorts() {
        var game = Scenario(new Rng()); game.State.Stars[0].Planets.Clear();
        game.State.Stars[1].Planets.Add(new Planet(game.State.Stars[1]) { Number = 2, Capacity = 50 });
        var f = game.State.FleetsSys.Place(0, 1, 0, scouts: 0); f.Transports = 11; f.Cruisers = 2;
        game.BeginClassicTurn();
        // Both convoys arrive in one tick and merge, preserving the source's planet-based ranking.
        var atB = game.State.Players[0].Tf.Skip(1).Single(x => x.Destination == 1 && x.ETA == 0);
        Assert.Equal((11, 2), (atB.Transports, atB.Cruisers)); Assert.Null(f.Destination); Finish(game);
    }

    [Fact]
    public void EmptyEnemyProductionFleetIsRemovedAndZeroBudgetStillChoosesResearchField() {
        var rng = new Rng(1); var game = Scenario(rng); var p = EnemyColony(game, 0, 0, 0, 10);
        for (int i = 0; i < 4; i++) Advance(game);
        Assert.Equal(ResearchField.Weapons, game.State.EnemyResearchField); Assert.Single(rng.Bounds);
        Assert.Equal((1, 11), rng.Bounds[0]); Assert.DoesNotContain(game.State.Players[0].Tf.Skip(1), f => f.Destination != null);
        Assert.Equal(0, p.PlayerSeenCapacity); Assert.Equal(Ownership.Enemy, p.Owner);
    }

    [Fact]
    public void FourthYearGrowsAllColoniesThenBuildsOriginalMinimumBasesAndResearchesRemainder() {
        var rng = new Rng(1); var game = Scenario(rng); var p = EnemyColony(game, 0, 10, 10, 60);
        var field = game.State.EnemyResearchField!.Value;
        for (int i = 0; i < 4; i++) Advance(game);
        Assert.Equal((15, 15, 1, 0), (p.Inhabitants, p.IndustrialUnits, p.MissileBases, p.AdvancedMissileBases));
        int progress = field == ResearchField.Weapons ? game.State.Players[0].WeaponsResearchProgress
            : field == ResearchField.Range ? game.State.Players[0].RangeResearchProgress : game.State.Players[0].VelocityResearchProgress;
        Assert.Equal(7, progress); Assert.Single(rng.Bounds);
    }

    [Theory]
    [InlineData(3, 1, 0, 9)]
    [InlineData(5, 1, 3, 0)]
    [InlineData(11, 1, 3, 0)]
    [InlineData(1, 1, 0, 9)]
    public void EnemyInvestmentUsesOriginalRandomBranchCosts(int decision, int cruisers, int iuAdded, int extraResearch) {
        var rng = decision == 3 ? new Rng(1, 1, 1, 1, 3, 1, 1, 1)
            : decision == 1 ? new Rng(1, 1, 1, 1, 1, 1, 3, 1) : new Rng(1, 1, 1, 1, decision, 3, 1);
        var game = Scenario(rng); var p = EnemyColony(game, 0, 20, 20, 100); p.Conquered = true;
        var guard = game.State.FleetsSys.Place(0, 1, 0, scouts: 0); guard.Cruisers = 1;
        // Prevent warship orders changing the guard while keeping inv_enemy active via initialization.
        game.State.Stars[1].Planets.Clear(); game.State.Stars[2].Planets.Clear();
        // Current conquered planet always wins; each of the four turns consumes one eval draw.
        for (int i = 0; i < 4; i++) Advance(game);
        Assert.Equal(1 + cruisers, guard.Cruisers); Assert.Equal(25 + iuAdded, p.IndustrialUnits);
        Assert.Equal(0, p.MissileBases); Assert.Equal(0, p.AdvancedMissileBases);
        var e = game.State.Players[0]; Assert.Equal(extraResearch, e.WeaponsResearchProgress + e.RangeResearchProgress + e.VelocityResearchProgress);
    }

    [Fact]
    public void EnemyScoutWithNoReachableDestinationFailsVisiblyWithoutRngReplay() {
        var rng = new Rng(); var game = Scenario(rng); game.State.Players[0].Range = 0;
        game.State.FleetsSys.Place(0, 1, 0, scouts: 1);
        Assert.Contains("would not terminate", Assert.Throws<InvalidOperationException>(() => game.BeginClassicTurn()).Message);
        Assert.Contains("will not be replayed", Assert.Throws<InvalidOperationException>(() => new Game(rng, game.State).BeginClassicTurn()).Message);
        Assert.Empty(rng.Bounds); Assert.Equal(0, game.Turn);
    }

    [Fact]
    public void MobilizedPopulationAndIndustryBecomeTransportsWhichCanFoundAnotherColony() {
        var rng = new Rng(11, 1, 3, 1); var game = Scenario(rng);
        for (int i = 0; i < 3; i++) Advance(game);
        var p = EnemyColony(game, 0, 20, 30, 20); Advance(game);
        Assert.Equal((13, 23, 2), (p.Inhabitants, p.IndustrialUnits, p.MissileBases));
        var ships = game.State.Players[0].Tf[1]; Assert.Equal(7, ships.Transports);
        Advance(game); Advance(game);
        Assert.Equal(Ownership.Enemy, game.State.Stars[1].Planets[0].Owner);
        Assert.Equal(7, game.State.Stars[1].Planets[0].Inhabitants);
        Assert.Equal(4, rng.Bounds.Count); Assert.Equal((1, 3), rng.Bounds[1]);
        Assert.Equal(35, game.State.Players[1].Tf[1].Transports);
    }

    [Fact]
    public void OriginalAmbCommentActuallyBuildsBattlestarBeforeAdvancedBaseAndCruisers() {
        var rng = new Rng(2, 5, 3, 3, 3, 1); var game = Scenario(rng);
        for (int i = 0; i < 3; i++) Advance(game);
        var p = EnemyColony(game, 0, 100, 160, 100); p.AdvancedMissileBases = 2; Advance(game);
        Assert.Equal((100, 160, 0, 3), (p.Inhabitants, p.IndustrialUnits, p.MissileBases, p.AdvancedMissileBases));
        Assert.Equal((1, 3), (game.State.Players[0].Tf[1].BattleStars, game.State.Players[0].Tf[1].Cruisers));
        Assert.Equal(new[] { (1, 6), (1, 8), (1, 13), (1, 13), (1, 13), (1, 11) }, rng.Bounds);
    }

    [Theory]
    [InlineData(1, 4, 0, 3)]
    [InlineData(2, 5, 1, 0)]
    public void AdvancedBaseFourIsRandomGateRatherThanCap(int roll, int advanced, int missile, int cruisers) {
        var rng = roll == 1 ? new Rng(1, 3, 3, 3, 1) : new Rng(2, 3, 1); var game = Scenario(rng);
        for (int i = 0; i < 3; i++) Advance(game);
        var p = EnemyColony(game, 0, 100, 50, 100); p.AdvancedMissileBases = 4; Advance(game);
        Assert.Equal((advanced, missile), (p.AdvancedMissileBases, p.MissileBases));
        Assert.Equal(cruisers, game.State.Players[0].Tf[1].Cruisers);
    }

    [Fact]
    public void EnemyAndPlayerInvestmentInterleaveInStarOrderAfterOneWholeGrowthPass() {
        var rng = new Rng(1, 5); var game = Scenario(rng);
        var a = EnemyColony(game, 0, 4, 4, 20); var c = EnemyColony(game, 2, 2, 2, 20);
        var b = game.State.Stars[1].Planets[0]; b.Owner = Ownership.Player; b.Capacity = 20; b.Inhabitants = b.IndustrialUnits = 3;
        game.State.Players[1].WeaponsResearchProgress = 49;
        for (int i = 0; i < 3; i++) Advance(game);
        game.BeginClassicTurn(); game.PrepareClassicEconomy();
        Assert.Equal((6, 4, 3), (a.Inhabitants, b.Inhabitants, c.Inhabitants)); Assert.Single(rng.Bounds);
        Assert.Equal(1, game.State.Investment!.StarIndex); Assert.Equal(4, game.State.Investment.Balance);
        Assert.Null(game.InvestInClassicColony('W', 4)); game.FinishClassicInvestmentLine();
        Assert.Equal(2, rng.Bounds.Count); Assert.Equal(4, game.State.Players[1].Weapons);
        Assert.Equal(ResearchField.Weapons, game.State.EnemyResearchField); // After W breakthrough, 6 > rnd(10)==5.
        game.CompleteClassicTurn(); Assert.Equal(4, game.Turn);
    }

    [Fact]
    public void ResearchBreakthroughsPreserveInnerAndOuterFieldSelectionAndZeroAmountReroll() {
        var covered = new HashSet<ResearchField>();
        for (int seed = 1; seed <= 100 && covered.Count < 3; seed++) {
            var rng = new Rng(1, 1); var game = Scenario(rng, seed); var field = game.State.EnemyResearchField!.Value;
            if (!covered.Add(field)) continue;
            var enemy = game.State.Players[0];
            int level = field == ResearchField.Weapons ? enemy.Weapons : field == ResearchField.Range ? enemy.Range : enemy.Velocity;
            int need = ResearchRules.NextRequirement(enemy, field)!.Value;
            if (field == ResearchField.Weapons) enemy.WeaponsResearchProgress = need - 1;
            else if (field == ResearchField.Range) enemy.RangeResearchProgress = need - 1;
            else enemy.VelocityResearchProgress = need - 1;
            for (int i = 0; i < 3; i++) Advance(game);
            EnemyColony(game, 0, 0, 1, 10); Advance(game);
            Assert.Equal(level + 1, field == ResearchField.Weapons ? enemy.Weapons : field == ResearchField.Range ? enemy.Range : enemy.Velocity);
            Assert.Equal(field == ResearchField.Velocity ? 1 : 2, rng.Bounds.Count);
            Assert.All(rng.Bounds, bounds => Assert.Equal((1, 11), bounds));
        }
        Assert.Equal(3, covered.Count);
    }

    [Fact]
    public void CappedEnemyResearchStillSelectsFieldAndDiscardsExcessWithoutNewDrawsAtAllCaps() {
        var game = Scenario(new Rng()); var enemy = game.State.Players[0]; enemy.Weapons = 10; enemy.Range = 15; enemy.Velocity = 12;
        EnemyColony(game, 0, 0, 7, 10); for (int i = 0; i < 4; i++) Advance(game);
        Assert.Equal(ResearchField.Range, game.State.EnemyResearchField);
        Assert.Equal((0, 0, 0), (enemy.WeaponsResearchProgress, enemy.RangeResearchProgress, enemy.VelocityResearchProgress));
    }

    [Fact]
    public void ClassicGExpandsAndPlayerScoutCanDiscoverColonyThroughOrdinaryOrders() {
        var game = Scenario(new CRandRng(7)); var enemy = game.State.FleetsSys.Place(0, 1, 0, scouts: 2);
        enemy.Transports = 35; enemy.Cruisers = 1;
        var player = game.State.Players[1].Tf[1]; player.Transports = 0; player.Scouts = 1; game.State.Players[1].Velocity = 12;
        game.State.FleetsSys.Place(1, 2, 3, scouts: 0).Transports = 1; // A scout alone is eliminated by C's T+IU rule.
        var terminal = new Terminal("daaggxsa\n"); Assert.Equal(0, new ClassicGameLoop(game, terminal).Run());
        Assert.Equal(2, game.Turn); Assert.True(game.State.Stars[0].ExploredBy[1]);
        Assert.Equal(Ownership.Enemy, game.State.Stars[0].Planets[0].Owner);
        int firstVisible = terminal.Screens.FindIndex(s => s.Any(row => row.Contains("*EN*")));
        Assert.True(firstVisible > 0); Assert.StartsWith("Task force a exploring A.", terminal.Screens[firstVisible][18]);
        Assert.Contains(" 1:60*EN*", terminal.Screens[^1][2]); Assert.DoesNotContain("mb", terminal.Screens[^1][2]);
    }

    [Fact]
    public void EnemyEventsOnlyReportDeparturesAndArrivalsAtPlayerOccupiedLocations() {
        var rng = new Rng(1); var game = Scenario(rng);
        game.State.FleetsSys.Place(1, 2, 0, scouts: 1); game.State.FleetsSys.Place(0, 1, 0, scouts: 1);
        var p = game.State.Stars[1].Planets[0]; p.Owner = Ownership.Player; p.Inhabitants = p.IndustrialUnits = 3;
        game.State.Stars[1].ExploredBy[1] = true;
        Advance(game); Assert.Equal(new[] { 0 }, game.State.VisibleEnemyDepartures); Assert.Equal(new[] { 1 }, game.State.VisibleEnemyArrivals);
        var screen = new ClassicScreen().Render(game);
        Assert.StartsWith("E Arrive:            B", screen[21][31..]); Assert.StartsWith("E Depart:            A", screen[22][31..]);
        Assert.False(game.State.Stars[2].ExploredBy[1]);
    }

    [Fact]
    public void KnownScoutDestinationRejectionSamplingAndAllocatedSlotsMatchC() {
        var rng = new Rng(1, 4, 2); var game = Scenario(rng);
        foreach (var star in game.State.Stars) star.ExploredBy[0] = true;
        var scout = game.State.FleetsSys.Place(0, 1, 0, scouts: 1);
        game.BeginClassicTurn(); // Reject current A and out-of-range D, then choose B.
        Assert.Equal(new[] { (1, 5), (1, 5), (1, 5) }, rng.Bounds); Assert.Null(scout.Destination);
        Assert.Equal((1, 0, 1), (game.State.Players[0].Tf[2].Destination, game.State.Players[0].Tf[2].ETA, game.State.Players[0].Tf[2].Scouts)); Finish(game);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 56)]
    public void EnemyBombardsLowIndustryConquestWithoutLeakingUnobservedCapacity(bool observed, int seenCapacity) {
        var rng = new Rng(1, 1, 1); var game = Scenario(rng); var p = EnemyColony(game, 0, 4, 4); p.Conquered = true;
        if (observed) game.State.FleetsSys.Place(1, 2, 0, scouts: 1);
        game.State.FleetsSys.Place(0, 1, 0, scouts: 0).Cruisers = 1;
        game.BeginClassicTurn(); Assert.Equal((56, 0, 0, Ownership.None), (p.Capacity, p.Inhabitants, p.IndustrialUnits, p.Owner));
        Assert.Equal(seenCapacity, p.PlayerSeenCapacity); Assert.False(p.Conquered); Assert.Equal(3, rng.Bounds.Count);
    }

    [Fact]
    public void OriginalWanderStoppageNowSplitsToReachableStarAndRepeatedGCompletesTurns() {
        var rng = new Rng(1, 1, 1, 4, 2, 1, 1, 1); var game = Scenario(rng); EnemyColony(game, 0, 30, 30).Conquered = true;
        var fleet = game.State.FleetsSys.Place(0, 1, 0, scouts: 0); fleet.Cruisers = 4;
        var terminal = new Terminal("gg"); Assert.Equal(0, new ClassicGameLoop(game, terminal).Run());
        Assert.Equal(2, game.Turn); Assert.Equal(8, rng.Bounds.Count); Assert.Equal(2, fleet.Cruisers);
        Assert.DoesNotContain(terminal.Screens, s => s[24].Contains("stopped") || s[24].Contains("wander_bc"));
        Assert.Equal(0, fleet.Destination); Assert.Equal(0, fleet.ETA);
        var child = game.State.Players[0].Tf[2]; Assert.Equal((2, 0, 2), (child.Destination, child.ETA, child.Cruisers));
        Assert.Equal((1, 3), rng.Bounds[4]); Assert.True(game.State.Stars[2].ExploredBy[0]);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 2)]
    public void WanderSelectsFirstOrLastReachableStarSkippingCurrentAndOutOfRangeEntries(int selection, int target) {
        var rng = new Rng(1, 1, 1, 4, selection); var game = Scenario(rng);
        game.State.Stars[1].ExploredBy[0] = true; EnemyColony(game, 1, 30, 30).Conquered = true;
        var parent = game.State.FleetsSys.Place(0, 1, 1, scouts: 0); parent.Cruisers = 5; parent.BattleStars = 2;
        Advance(game); var child = game.State.Players[0].Tf[2];
        Assert.Equal((1, 3, 1), (parent.Destination, parent.Cruisers, parent.BattleStars));
        Assert.Equal((target, 0, 2, 1), (child.Destination, child.ETA, child.Cruisers, child.BattleStars));
        Assert.Equal(new[] { (1, 21), (1, 21), (1, 21), (1, 5), (1, 3) }, rng.Bounds);
        Assert.Equal(0, child.Scouts); Assert.True(game.State.Stars[target].ExploredBy[0]); Assert.Equal(1, game.Turn);
    }

    [Fact]
    public void WanderCanSelectFinalStarInArrayWithoutUsingAnIndexPastTheEnd() {
        var rng = new Rng(1, 1, 1, 1, 4, 3); var game = Scenario(rng);
        var last = game.State.Stars[3]; game.State.Board[last.X, last.Y].StarRef = null;
        last.X = 3; last.Y = 0; game.State.Board[3, 0].StarRef = last;
        game.State.FleetsSys.Place(1, 1, 3, scouts: 0);
        game.State.Stars[1].ExploredBy[0] = true; EnemyColony(game, 1, 30, 30).Conquered = true;
        game.State.FleetsSys.Place(0, 1, 1, scouts: 0).Cruisers = 4;
        Advance(game); var child = game.State.Players[0].Tf[2];
        Assert.Equal((3, 1, 2), (child.Destination, child.ETA, child.Cruisers));
        Assert.Equal((1, 4), rng.Bounds[^1]); Assert.Equal(1, game.Turn);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(3, 3, 0)]
    [InlineData(4, 2, 2)]
    [InlineData(5, 3, 2)]
    public void WanderKeepsOddWarshipsInGarrisonAndOnlyTransfersTwoTransportsAboveThree(int transports, int left, int sent) {
        var rng = new Rng(1, 1, 1, 4, 1); var game = Scenario(rng);
        EnemyColony(game, 0, 58, 30).Conquered = true;
        foreach (var star in game.State.Stars.Skip(1).Take(2)) {
            star.ExploredBy[0] = true; star.Planets[0].EnemySeenOwner = Ownership.Player;
        }
        var parent = game.State.FleetsSys.Place(0, 1, 0, scouts: 0); parent.Cruisers = 5; parent.BattleStars = 3; parent.Transports = transports;
        Advance(game); var child = game.State.Players[0].Tf[2];
        Assert.Equal((3, 2, left), (parent.Cruisers, parent.BattleStars, parent.Transports));
        Assert.Equal((2, 1, sent), (child.Cruisers, child.BattleStars, child.Transports));
        Assert.Equal(transports, parent.Transports + child.Transports); Assert.Equal(1, game.Turn);
        Assert.True(game.State.Stars[0].Planets[0].Conquered); Assert.Equal(Ownership.Enemy, game.State.Stars[0].Planets[0].Owner);
    }

    [Theory]
    [InlineData(4, 0, 1, false)]
    [InlineData(4, 0, 4, true)]
    [InlineData(0, 4, 1, false)]
    [InlineData(0, 4, 4, true)]
    [InlineData(0, 9, 1, true)]
    public void WanderPreservesConditionalTriggerAndDrawBeforeGuaranteedBattlestarSplit(int cruisers, int battles, int trigger, bool split) {
        var rng = new Rng(1, 1, 1, trigger, 1); var game = Scenario(rng); EnemyColony(game, 0, 30, 30).Conquered = true;
        var parent = game.State.FleetsSys.Place(0, 1, 0, scouts: 0); parent.Cruisers = cruisers; parent.BattleStars = battles;
        Advance(game); var child = game.State.Players[0].Tf[2];
        Assert.Equal(split ? 5 : 4, rng.Bounds.Count); Assert.Equal((1, 5), rng.Bounds[3]);
        Assert.Equal(split, child.Destination != null);
        Assert.Equal(split ? cruisers - cruisers / 2 : cruisers, parent.Cruisers);
        Assert.Equal(split ? battles - battles / 2 : battles, parent.BattleStars); Assert.Equal(1, game.Turn);
    }

    [Fact]
    public void WanderDoesNotDrawOrAllocateWhenNoOtherStarIsReachable() {
        var rng = new Rng(1, 4); var game = Scenario(rng); game.State.Players[0].Range = 0;
        EnemyColony(game, 0, 30, 30).Conquered = true;
        var parent = game.State.FleetsSys.Place(0, 1, 0, scouts: 0); parent.Cruisers = 4;
        Advance(game); Assert.Equal(new[] { (1, 21), (1, 5) }, rng.Bounds);
        Assert.Single(game.State.Players[0].Tf.Skip(1).Where(f => f.Destination != null));
        Assert.Equal(4, parent.Cruisers); Assert.Equal(1, game.Turn);
    }

    [Fact]
    public void SmallGarrisonDoesNotConsumeTriggerOrTargetDraw() {
        var rng = new Rng(1, 1, 1); var game = Scenario(rng); EnemyColony(game, 0, 30, 30).Conquered = true;
        var parent = game.State.FleetsSys.Place(0, 1, 0, scouts: 0); parent.Cruisers = parent.BattleStars = 3;
        Advance(game); Assert.Equal(3, rng.Bounds.Count); Assert.All(rng.Bounds, b => Assert.Equal((1, 21), b));
        Assert.Single(game.State.Players[0].Tf.Skip(1).Where(f => f.Destination != null)); Assert.Equal(1, game.Turn);
    }

    [Fact]
    public void WanderUsesEnemyDistanceEtaFormulaAndMovesOnTheSameTurnWithoutResettingOrigeta() {
        var rng = new Rng(1, 1, 1, 4, 2); var game = Scenario(rng); game.State.Players[0].Velocity = 2;
        var target = game.State.Stars[2]; game.State.Board[target.X, target.Y].StarRef = null;
        target.X = 3; target.Y = 4; game.State.Board[3, 4].StarRef = target;
        EnemyColony(game, 0, 30, 30).Conquered = true;
        game.State.FleetsSys.Place(0, 1, 0, scouts: 0).Cruisers = 4;
        Advance(game); var child = game.State.Players[0].Tf[2];
        Assert.Equal((2, 2, 0), (child.Destination, child.ETA, child.EtaOriginal)); // floor((5-.01)/2)+1 == 3, then move once.
        Assert.Equal((0, 0, 0, 0), (child.X, child.Y, child.DepartureX, child.DepartureY)); Assert.Equal(1, game.Turn);
        Assert.Empty(game.State.VisibleEnemyDepartures); Assert.False(target.ExploredBy[1]);
    }

    [Fact]
    public void EnemySlotExhaustionStopsOrdersWithoutDummySlotWritesOrRepeatedRng() {
        var rng = new Rng(); var game = Scenario(rng);
        for (int i = 1; i <= 26; i++) game.State.FleetsSys.Place(0, i, 0, scouts: 1);
        Assert.Contains("Taskforce limit", Assert.Throws<InvalidOperationException>(() => game.BeginClassicTurn()).Message);
        Assert.Contains("will not be replayed", Assert.Throws<InvalidOperationException>(() => game.BeginClassicTurn()).Message);
        Assert.Empty(rng.Bounds); Assert.Equal(26, game.State.Players[0].Tf.Skip(1).Sum(f => f.Scouts));
        Assert.Null(game.State.Players[0].Tf[0]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(41)]
    public void FortyTurnExpansionIsDeterministicAndDoesNotExposeHiddenColonies(int seed) {
        static (string State, int EnemyColonies) Simulate(int seed) {
            var game = Scenario(new CRandRng(seed)); var f = game.State.FleetsSys.Place(0, 1, 0, scouts: 2); f.Transports = 35; f.Cruisers = 1;
            game.State.FleetsSys.LandPlayerTransports(1, 1, 3);
            for (int turn = 0; turn < 40; turn++) {
                Advance(game);
                var screen = new ClassicScreen().Render(game);
                Assert.All(game.State.Stars.Take(3), s => Assert.False(s.ExploredBy[1]));
                Assert.DoesNotContain(screen, row => row.Contains("*EN*") || row.Contains("E Arrive:") || row.Contains("E Depart:"));
            }
            Assert.Equal(40, game.Turn); Assert.Contains(game.State.Stars.Take(3).SelectMany(s => s.Planets), p => p.MissileBases > 0);
            Assert.True(game.State.Players[0].WeaponsResearchProgress + game.State.Players[0].RangeResearchProgress + game.State.Players[0].VelocityResearchProgress > 0);
            return (JsonSerializer.Serialize(new { game.Turn, game.State.ProductionYear, game.State.EnemyResearchField,
                Players = game.State.Players.Select(p => new { p.Weapons, p.Range, p.Velocity, p.WeaponsResearchProgress, p.RangeResearchProgress, p.VelocityResearchProgress,
                    Fleets = p.Tf.Skip(1).Select(x => new { x.Destination, x.ETA, x.Transports, x.Scouts, x.Cruisers, x.BattleStars }) }),
                Planets = game.State.Stars.SelectMany(s => s.Planets).Select(p => new { p.Owner, p.Inhabitants, p.IndustrialUnits, p.MissileBases, p.AdvancedMissileBases }) }),
                game.State.Stars.SelectMany(s => s.Planets).Count(p => p.Owner == Ownership.Enemy));
        }
        var a = Simulate(seed); var b = Simulate(seed); Assert.Equal(a, b); Assert.Equal(3, a.EnemyColonies);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    public void UnmodifiedGeneratedGalaxyRunsFortyClassicTurnsWithEnemyColoniesAndRepeatableOutcomes(int seed) {
        static string Run(int seed) {
            var game = new Game(new CRandRng(seed), new GameState(new GameConfig()));
            game.GenerateStars(); game.InitializeClassicPlayers(); game.InitializeClassicEnemy(); game.BeginClassicPlayerStartup();
            game.SelectClassicPlayerStartingStar((game.State.Players[0].Tf[1].Destination!.Value + 1) % game.State.Stars.Count);
            game.InvestClassicStartupResearch(ResearchField.Range, 30); game.FinalizeClassicPlayerStartup();
            void Combat() {
                int interactions = 0;
                while (game.HasPendingClassicCombat) {
                    Assert.True(++interactions < 1000);
                    if (game.State.Battle is { Stage: FleetBattleStage.Orders }) game.ContinueFleetBattle();
                    else if (game.State.Battle != null) game.AcknowledgeFleetBattle();
                    else if (game.State.EnemyAttack != null) game.AcknowledgeEnemyAttack();
                    else if (game.State.Attack is { AwaitingAcknowledgement: true }) game.AcknowledgePlayerAttack();
                    else game.EndPlayerAttack();
                }
            }
            for (int turn = 0; turn < 40; turn++) {
                game.BeginClassicTurn(); game.PrepareClassicCombat(); Combat(); game.PrepareClassicEconomy();
                Assert.Null(game.State.Investment); // The player issues G and never lands or owns colonies.
                game.PrepareClassicCombat(afterInvestment: true); Combat(); game.CompleteClassicTurn();
            }
            Assert.Equal(40, game.Turn);
            int colonies = game.State.Stars.SelectMany(s => s.Planets).Count(p => p.Owner == Ownership.Enemy);
            Assert.True(colonies >= 1, $"Seed {seed}: no enemy colony after 40 turns.");
            if (seed == 17) Assert.True(colonies > 1);
            return JsonSerializer.Serialize(new { game.Turn, game.State.EnemyResearchField, Players = game.State.Players,
                Colonies = game.State.Stars.SelectMany(s => s.Planets).Select(p => new { p.Owner, p.Capacity, p.Inhabitants, p.IndustrialUnits, p.MissileBases, p.AdvancedMissileBases }) });
        }
        Assert.Equal(Run(seed), Run(seed));
    }

    [Theory]
    [InlineData(Ownership.None, 0)]
    [InlineData(Ownership.Player, 1)]
    public void StrategicWarshipScoreUsesEnemyMemoryAndDeploymentCanCaptureBeforeClockCompletion(Ownership memory, int destination) {
        var rng = new Rng(1, 1, 1); var game = Scenario(rng);
        var home = EnemyColony(game, 0, 20, 30); home.MissileBases = 3;
        var target = game.State.Stars[1].Planets[0]; target.Owner = Ownership.Player;
        target.Inhabitants = target.IndustrialUnits = 10; target.EnemySeenOwner = memory;
        game.State.Stars[1].ExploredBy[0] = game.State.Stars[2].ExploredBy[0] = true;
        var fleet = game.State.FleetsSys.Place(0, 1, 0, scouts: 0); fleet.Cruisers = 1;
        game.BeginClassicTurn(); Assert.Equal(destination, fleet.Destination); Assert.Equal(0, game.Turn);
        game.PrepareClassicCombat();
        if (memory == Ownership.Player) {
            Assert.NotNull(game.State.EnemyAttack); game.AcknowledgeEnemyAttack(); game.AcknowledgeEnemyAttack();
            Assert.Equal(Ownership.Enemy, target.Owner); Assert.True(target.Conquered);
        }
        else { Assert.Null(game.State.EnemyAttack); Assert.Equal(Ownership.Player, target.Owner); }
        Finish(game); Assert.Equal(1, game.Turn); Assert.Equal(3, rng.Bounds.Count);
    }

    [Theory]
    [InlineData(0, Ownership.None)]
    [InlineData(3, Ownership.Enemy)]
    public void TransportRoomBonusUsesDisplayedTurnIntegerDivisionBeforeOrdersAndGrowth(int completed, Ownership homeOwner) {
        var game = Scenario(new Rng()); for (int i = 0; i < completed; i++) Advance(game);
        game.State.Stars[0].Planets[0].Capacity = 39;
        game.State.FleetsSys.Place(0, 1, 0, scouts: 0).Transports = 1;
        game.BeginClassicTurn(); var home = game.State.Stars[0].Planets[0];
        Assert.Equal(homeOwner, home.Owner); Assert.Equal(completed == 3 ? 1 : 0, home.Inhabitants);
        if (completed == 0) Assert.Equal(1, game.State.Players[0].Tf[2].Destination);
        else Assert.Null(game.State.Players[0].Tf[1].Destination);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(41)]
    public void SixtyTurnDevelopmentEndsAtConfiguredLimitWithRepeatableOriginalScores(int seed) {
        static string Play(int seed) {
            var game = Scenario(new CRandRng(seed)); game.State.Config.MaxTurns = 60;
            var enemy = game.State.FleetsSys.Place(0, 1, 0, scouts: 2); enemy.Transports = 35; enemy.Cruisers = 1;
            game.State.Stars[3].Planets[0].Capacity = 60; game.State.FleetsSys.LandPlayerTransports(1, 1, 20);
            while (game.State.Result == null) { Advance(game); game.CheckClassicGameOver(); }
            var result = game.State.Result!; Assert.Equal(60, game.Turn); Assert.Equal(61, result.DisplayedTurn);
            Assert.Equal(ClassicEndReason.TurnLimit, result.Reasons);
            Assert.Equal(3, game.State.Stars.SelectMany(s => s.Planets).Count(p => p.Owner == Ownership.Enemy));
            Assert.True(result.Enemy.IndustrialUnits > 0); Assert.True(result.Player.IndustrialUnits > 0);
            Assert.Throws<InvalidOperationException>(() => game.BeginClassicTurn());
            return JsonSerializer.Serialize(new { result, Players = game.State.Players,
                Colonies = game.State.Stars.SelectMany(s => s.Planets).Select(p => new { p.Owner, p.Inhabitants, p.IndustrialUnits, p.MissileBases, p.AdvancedMissileBases }) });
        }
        Assert.Equal(Play(seed), Play(seed));
    }

    [Fact]
    public void EnemyProductionCanCreateMandatoryCombatBeforeAnnualUpdateAndOutcomeCheck() {
        var rng = new Rng(3, 1, 1, 100); var game = Scenario(rng);
        EnemyColony(game, 0, 16, 16, 20); game.State.FleetsSys.Place(1, 2, 0, scouts: 1);
        for (int i = 0; i < 3; i++) Advance(game);
        game.BeginClassicTurn(); game.PrepareClassicCombat(); Assert.False(game.HasPendingClassicCombat);
        game.PrepareClassicEconomy(); Assert.Equal(3, game.Turn); Assert.Null(game.State.Investment);
        Assert.Equal(1, game.State.Players[0].Tf[1].Cruisers);
        game.PrepareClassicCombat(afterInvestment: true); Assert.NotNull(game.State.Battle);
        Assert.Throws<InvalidOperationException>(() => game.CheckClassicGameOver()); Assert.Null(game.State.Result);
        game.AcknowledgeFleetBattle(); Assert.Equal(FleetBattleStage.FinalPause, game.State.Battle!.Stage);
        game.AcknowledgeFleetBattle(); game.CompleteClassicTurn(); Assert.Equal(4, game.Turn);
        Assert.Null(game.CheckClassicGameOver()); Assert.Equal(4, rng.Bounds.Count);
        Assert.Null(game.State.Players[1].Tf[2].Destination);
    }
}
