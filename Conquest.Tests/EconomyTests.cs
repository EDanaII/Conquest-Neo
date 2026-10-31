using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.Tests;

public sealed class EconomyTests {
    private sealed class NoDrawRng : IRng {
        public int Next(int minInclusive, int maxExclusive) => throw new InvalidOperationException("Unexpected economic RNG draw");
        public void Reseed(int seed) => throw new InvalidOperationException("Unexpected reseed");
    }
    private static Game Scenario(int population = 20, int iu = 20, int capacity = 100, double rate = 0) {
        var state = new GameState(new GameConfig { NumStars = 2, SafeExplore = true });
        state.Stars[1].X = 4;
        foreach (var star in state.Stars) { star.ExploredBy[1] = true; state.Board[star.X, star.Y].StarRef = star; }
        var home = state.Stars[0];
        home.Planets.Add(new Planet(home) { Number = 3, Capacity = capacity, PlayerSeenCapacity = capacity,
            Owner = Ownership.Player, Inhabitants = population, IndustrialUnits = iu });
        for (int team = 0; team < 2; team++) {
            state.Players[team].Velocity = 1; state.Players[team].Range = 5; state.Players[team].Weapons = 3;
            state.Players[team].PopulationGrowthRate = team == 1 ? rate : 0.5;
        }
        var fleet = state.FleetsSys.Place(1, 1, 0, scouts: 2); fleet.Transports = 8; fleet.Cruisers = 1;
        return new Game(new NoDrawRng(), state);
    }
    private static void AdvanceWithoutInvestment(Game game) {
        game.BeginClassicTurn(); game.PrepareClassicEconomy();
        Assert.Null(game.State.Investment); game.CompleteClassicTurn();
    }
    private static ColonyInvestment OpenBank(Game game) {
        for (int i = 0; i < 3; i++) AdvanceWithoutInvestment(game);
        game.BeginClassicTurn(); game.PrepareClassicEconomy();
        return Assert.IsType<ColonyInvestment>(game.State.Investment);
    }
    private static void SpendAndFinish(Game game, int amount) {
        Assert.Null(game.InvestInClassicColony('R', amount)); game.FinishClassicInvestmentLine();
    }

    [Theory]
    [InlineData(0, 20, 0.3, false, 0)]
    [InlineData(1, 20, 0.3, false, 1)]
    [InlineData(2, 20, 0.3, false, 3)]
    [InlineData(3, 20, 0.3, false, 4)]
    [InlineData(5, 20, 0.3, false, 7)]
    [InlineData(19, 20, 0.3, false, 25)]
    [InlineData(20, 20, 0.3, false, 20)]
    [InlineData(25, 20, 0.3, false, 25)]
    [InlineData(40, 20, 0.3, false, 28)]
    [InlineData(4, 20, 0.3, true, 4)]
    [InlineData(5, 20, 0.3, true, 6)]
    [InlineData(3, 1, 0.5, true, 2)]
    [InlineData(1, 20, 0.5, false, 2)]
    [InlineData(1, 20, 0.49999999999999994, false, 2)]
    public void GrowthMatchesCIntegerDivisionSignedAdditionRoundingAndConqueredHalving(int population, int capacity, double rate, bool conquered, int expected) {
        var game = Scenario(population, 50, capacity, rate); var planet = game.State.Stars[0].Planets[0]; planet.Conquered = conquered;
        var bank = OpenBank(game);
        Assert.Equal(expected, planet.Inhabitants);
        Assert.Equal(50 + expected - population, planet.IndustrialUnits);
        Assert.Equal(planet.IndustrialUnits, bank.Balance);
        Assert.Equal(3, game.Turn); Assert.Equal(0, game.State.ProductionYear);
        Assert.True(game.State.ClassicTurnInProgress);
    }

    [Fact]
    public void AllColoniesGrowBeforeFirstBudgetAndSpendingFollowsStarAndStoredPlanetOrder() {
        var game = Scenario(3, 3, 20, 0.3); var first = game.State.Stars[0].Planets[0]; first.Number = 6;
        var second = new Planet(game.State.Stars[0]) { Number = 2, Owner = Ownership.Player, Capacity = 20, Inhabitants = 5, IndustrialUnits = 5 };
        var third = new Planet(game.State.Stars[1]) { Number = 1, Owner = Ownership.Player, Capacity = 30, Inhabitants = 9, IndustrialUnits = 9 };
        game.State.Stars[0].Planets.Add(second); game.State.Stars[1].Planets.Add(third);
        var bank = OpenBank(game);
        Assert.Same(first, bank.Planet); Assert.Equal(4, bank.Balance);
        Assert.Equal((7, 7, 12, 12), (second.Inhabitants, second.IndustrialUnits, third.Inhabitants, third.IndustrialUnits));
        SpendAndFinish(game, 4);
        Assert.Same(second, game.State.Investment!.Planet); Assert.Equal(7, game.State.Investment.Balance);
        SpendAndFinish(game, 7);
        Assert.Same(third, game.State.Investment!.Planet); Assert.Equal(12, game.State.Investment.Balance);
        SpendAndFinish(game, 12); game.CompleteClassicTurn();
        Assert.Equal(4, game.Turn); Assert.Equal(1, game.State.ProductionYear);
        Assert.Equal(6, game.State.Players[1].Range); Assert.Equal(3, game.State.Players[1].RangeResearchProgress);
    }

    [Fact]
    public void BudgetIsCopiedFromIUInfrastructureCostsThreeAndFutureCyclesUseNewIUWithoutCarryingCash() {
        var game = Scenario(20, 10); var planet = game.State.Stars[0].Planets[0];
        var bank = OpenBank(game);
        Assert.Null(game.InvestInClassicColony('I', 2)); Assert.Equal(4, bank.Balance); Assert.Equal(12, planet.IndustrialUnits);
        SpendAndFinish(game, 4); game.CompleteClassicTurn();
        Assert.Equal(12, planet.IndustrialUnits);
        bank = OpenBank(game);
        Assert.Equal(12, bank.Balance); Assert.Equal(7, game.Turn);
    }

    [Theory]
    [InlineData('S', 2, 88)]
    [InlineData('C', 2, 68)]
    [InlineData('B', 1, 30)]
    [InlineData('M', 2, 84)]
    [InlineData('A', 2, 30)]
    [InlineData('I', 2, 94)]
    [InlineData('R', 2, 98)]
    [InlineData('V', 2, 98)]
    [InlineData('W', 2, 98)]
    public void OrdinaryInvestmentsHaveOriginalCostsAndImmediateFields(char key, int amount, int expectedBalance) {
        var game = Scenario(100, 100, 1000); var bank = OpenBank(game);
        Assert.Null(game.InvestInClassicColony(key, amount)); Assert.Equal(expectedBalance, bank.Balance);
        var player = game.State.Players[1]; var fleet = player.Tf[bank.FleetIndex]; var planet = bank.Planet;
        Assert.Equal(key == 'I' ? 102 : 100, planet.IndustrialUnits);
        Assert.Equal(key == 'S' ? amount : 0, fleet.Scouts); Assert.Equal(key == 'C' ? amount : 0, fleet.Cruisers);
        Assert.Equal(key == 'B' ? amount : 0, fleet.BattleStars);
        Assert.Equal(key == 'M' ? amount : 0, planet.MissileBases); Assert.Equal(key == 'A' ? amount : 0, planet.AdvancedMissileBases);
        Assert.Equal(key == 'R' ? amount : 0, player.RangeResearchProgress); Assert.Equal(key == 'V' ? amount : 0, player.VelocityResearchProgress);
        Assert.Equal(key == 'W' ? amount : 0, player.WeaponsResearchProgress);
    }

    [Fact]
    public void ConstructionUsesFreshSlotAndFinishMergesStationaryArrivalsEvenForResearchOnly() {
        var game = Scenario(20, 6);
        var arriving = game.State.FleetsSys.Place(1, 3, 1, scouts: 1); game.State.FleetsSys.SetCourse(1, 3, 0);
        var bank = OpenBank(game); Assert.Equal(2, bank.FleetIndex); Assert.Equal(0, arriving.ETA);
        Assert.Null(game.InvestInClassicColony('S')); Assert.Equal(1, game.State.Players[1].Tf[2].Scouts);
        Assert.Equal(0, game.State.Players[1].Tf[1].Destination);
        game.FinishClassicInvestmentLine(); game.CompleteClassicTurn();
        Assert.Null(game.State.Players[1].Tf[1].Destination); Assert.Null(arriving.Destination);
        Assert.Equal((8, 4, 1), (game.State.Players[1].Tf[2].Transports, game.State.Players[1].Tf[2].Scouts, game.State.Players[1].Tf[2].Cruisers));
        game = Scenario(20, 3); bank = OpenBank(game); SpendAndFinish(game, 3);
        Assert.Null(game.State.Players[1].Tf[1].Destination); Assert.Equal(2, game.State.Players[1].Tf[2].Scouts);
    }

    [Theory]
    [InlineData('S', 13, 2, 1)]
    [InlineData('C', 33, 2, 1)]
    [InlineData('B', 141, 2, 1)]
    [InlineData('M', 17, 2, 1)]
    [InlineData('A', 71, 2, 1)]
    public void MaximumPurchasesUseFloorDivisionAndLeaveTheRemainder(char key, int budget, int quantity, int remainder) {
        var game = Scenario(200, budget, 1000); var bank = OpenBank(game);
        Assert.Null(game.InvestInClassicColony(key, 999, maximum: true)); Assert.Equal(remainder, bank.Balance);
        int count = key switch { 'S' => game.State.Players[1].Tf[bank.FleetIndex].Scouts,
            'C' => game.State.Players[1].Tf[bank.FleetIndex].Cruisers, 'B' => game.State.Players[1].Tf[bank.FleetIndex].BattleStars,
            'M' => bank.Planet.MissileBases, _ => bank.Planet.AdvancedMissileBases };
        Assert.Equal(quantity, count);
    }

    [Fact]
    public void MaxInfrastructureAndBundlesUseOriginalSupportLimitsAndDenominations() {
        var game = Scenario(10, 15); var bank = OpenBank(game);
        Assert.Null(game.InvestInClassicColony('I', maximum: true)); Assert.Equal(20, bank.Planet.IndustrialUnits); Assert.Equal(0, bank.Balance);
        game = Scenario(100, 105, 1000); bank = OpenBank(game);
        Assert.Null(game.InvestInClassicColony('+')); Assert.Equal(3, bank.Balance);
        Assert.Equal((1, 2), (game.State.Players[1].Tf[bank.FleetIndex].BattleStars, game.State.Players[1].Tf[bank.FleetIndex].Cruisers));
        game = Scenario(100, 90, 1000); bank = OpenBank(game);
        Assert.Null(game.InvestInClassicColony('-')); Assert.Equal((2, 2, 4), (bank.Planet.AdvancedMissileBases, bank.Planet.MissileBases, bank.Balance));
    }

    [Theory]
    [InlineData('S', 2, false, " !can't affort   2S")]
    [InlineData('B', 1, true, " !can't affort   1B")]
    [InlineData('T', 6, false, " ! Not enough people for ( trans")]
    [InlineData('I', 6, false, " !Can't support that many iu's")]
    [InlineData('X', 1, false, " !Illegal field X")]
    [InlineData('X', 1, true, "Invalid investment field X")]
    [InlineData('S', -1, false, " !Invalid investment quantity")]
    [InlineData('B', int.MaxValue, false, " !Invalid investment quantity")]
    public void RejectedTokensDoNotChargeTheBudgetOrMutateFields(char token, int amount, bool maximum, string expected) {
        var game = Scenario(5, 6); var bank = OpenBank(game);
        Assert.Equal(expected, game.InvestInClassicColony(token, amount, maximum)); Assert.Equal(6, bank.Balance);
        Assert.Equal((5, 6, 0, 0), (bank.Planet.Inhabitants, bank.Planet.IndustrialUnits, bank.Planet.MissileBases, bank.Planet.AdvancedMissileBases));
        Assert.Equal(0, game.State.Players[1].Tf[bank.FleetIndex].Scouts);
    }

    [Fact]
    public void MobilizingTransportsUsesPopulationAndClampsIUButDoesNotRecomputeSpendingBalance() {
        var game = Scenario(5, 20); var bank = OpenBank(game);
        Assert.Null(game.InvestInClassicColony('T', 3));
        Assert.Equal((2, 4, 17), (bank.Planet.Inhabitants, bank.Planet.IndustrialUnits, bank.Balance));
        Assert.Equal(3, game.State.Players[1].Tf[bank.FleetIndex].Transports);
        SpendAndFinish(game, 17); Assert.Equal(4, game.State.Stars[0].Planets[0].IndustrialUnits);
    }

    [Fact]
    public void MaximumTransportsCanAbandonAPlanetWhileTheRemainingBudgetStillBuysShipsOrResearch() {
        var game = Scenario(6, 12); var bank = OpenBank(game); bank.Planet.MissileBases = 2; bank.Planet.AdvancedMissileBases = 1;
        Assert.Null(game.InvestInClassicColony('T', maximum: true));
        Assert.Equal(Ownership.None, bank.Planet.Owner); Assert.Equal((0, 0, 0, 0, 6),
            (bank.Planet.Inhabitants, bank.Planet.IndustrialUnits, bank.Planet.MissileBases, bank.Planet.AdvancedMissileBases, bank.Balance));
        Assert.Equal("  !abandoned planet", game.InvestInClassicColony('M'));
        Assert.Null(game.InvestInClassicColony('S')); game.FinishClassicInvestmentLine(); game.CompleteClassicTurn();
        Assert.Equal((14, 3), (game.State.Players[1].Tf[2].Transports, game.State.Players[1].Tf[2].Scouts));
    }

    [Theory]
    [InlineData('T', "!No transports on conqered col")]
    [InlineData('M', " !No Mb  on conquered colony")]
    [InlineData('A', " !No amb  on conquered colony")]
    [InlineData('-', " !No defense on conquered colony")]
    public void ConqueredColonyRestrictionsLeaveBalanceUntouched(char key, string error) {
        var game = Scenario(10, 10); game.State.Stars[0].Planets[0].Conquered = true;
        var bank = OpenBank(game);
        Assert.Equal(error, game.InvestInClassicColony(key)); Assert.Equal(10, bank.Balance);
        Assert.Null(game.InvestInClassicColony('I')); Assert.Equal(11, bank.Planet.IndustrialUnits); Assert.Equal(7, bank.Balance);
    }

    [Fact]
    public void ResearchSupportsLimitedNextBreakthroughMultipleLevelsAndWasteAtTheCapWithoutRng() {
        var game = Scenario(100, 100, 1000); game.State.Players[1].RangeResearchProgress = 18;
        var bank = OpenBank(game);
        Assert.Null(game.InvestInClassicColony('R', maximum: true));
        Assert.Equal((6, 0, 98), (game.State.Players[1].Range, game.State.Players[1].RangeResearchProgress, bank.Balance));
        Assert.Null(game.InvestInClassicColony('R', 96));
        Assert.Equal((7, 56, 2), (game.State.Players[1].Range, game.State.Players[1].RangeResearchProgress, bank.Balance));
        game = Scenario(100, 100, 1000); game.State.Players[1].Range = 15; bank = OpenBank(game);
        Assert.Null(game.InvestInClassicColony('R', maximum: true)); Assert.Equal(100, bank.Balance);
        Assert.Null(game.InvestInClassicColony('R', 100)); Assert.Equal(0, bank.Balance); Assert.Equal(15, game.State.Players[1].Range);
    }

    [Fact]
    public void BudgetAndClockSurviveAnotherFacadeAndGrowthCannotBeRepeatedOrBypassed() {
        var game = Scenario(3, 3, 20, 0.3); var bank = OpenBank(game);
        Assert.Null(game.InvestInClassicColony('R', 1)); Assert.Equal(3, bank.Balance);
        var other = new Game(new NoDrawRng(), game.State); other.PrepareClassicEconomy();
        Assert.Same(bank, other.State.Investment); Assert.Equal(3, other.Turn);
        Assert.Equal(4, bank.Planet.Inhabitants);
        Assert.Throws<InvalidOperationException>(() => other.Tick());
        Assert.Throws<InvalidOperationException>(() => other.BeginClassicTurn());
        Assert.Throws<InvalidOperationException>(() => other.CompleteClassicTurn());
        SpendAndFinish(other, 3); other.CompleteClassicTurn();
        Assert.Equal(4, game.Turn); Assert.Equal(1, game.State.ProductionYear);
    }

    [Fact]
    public void ZeroBudgetStillRequiresOneSubmittedLineAndProcessesAllTokensBeforeCompletion() {
        var game = Scenario(0, 0); var bank = OpenBank(game);
        Assert.NotNull(bank); Assert.Throws<InvalidOperationException>(() => game.CompleteClassicTurn());
        Assert.Equal(" !can't affort   1S", game.InvestInClassicColony('S'));
        game.FinishClassicInvestmentLine(); game.CompleteClassicTurn(); Assert.Equal(4, game.Turn);
        game = Scenario(10, 6); bank = OpenBank(game);
        Assert.Null(game.InvestInClassicColony('S')); Assert.Equal(0, bank.Balance);
        Assert.Equal(" !can't affort   1C", game.InvestInClassicColony('C'));
        game.FinishClassicInvestmentLine(); Assert.Equal(3, game.State.Players[1].Tf[2].Scouts);
    }

    [Fact]
    public void IntelligenceAgingAppliesToRememberedPlayerPlanetsWithoutOwnershipOrGrowthAndCapsAtTwelve() {
        var game = Scenario(0, 0); var remote = new Planet(game.State.Stars[1]) {
            Owner = Ownership.None, Capacity = 20, Inhabitants = 3, EnemySeenOwner = Ownership.Player, EnemySeenDefense = 11
        }; game.State.Stars[1].Planets.Add(remote);
        OpenBank(game); Assert.Equal(12, remote.EnemySeenDefense); Assert.Equal(3, remote.Inhabitants);
        game.FinishClassicInvestmentLine(); game.CompleteClassicTurn();
        OpenBank(game); Assert.Equal(12, remote.EnemySeenDefense);
    }

    [Fact]
    public void UnsupportedEnemyEconomyZeroCapacityAndFullSlotsRejectBeforeFourthMovement() {
        foreach (string condition in new[] { "enemy", "capacity", "slots" }) {
            var game = Scenario();
            if (condition == "enemy") game.State.Stars[1].Planets.Add(new Planet(game.State.Stars[1]) { Owner = Ownership.Enemy, Capacity = 20 });
            if (condition == "capacity") game.State.Stars[0].Planets[0].Capacity = 0;
            if (condition == "slots") for (int i = 2; i <= 26; i++) game.State.FleetsSys.Place(1, i, 0);
            for (int i = 0; i < 3; i++) AdvanceWithoutInvestment(game);
            Assert.Throws<InvalidOperationException>(() => game.BeginClassicTurn());
            Assert.Equal(3, game.Turn); Assert.Equal(4, game.State.ProductionYear); Assert.False(game.State.ClassicTurnInProgress);
            Assert.Equal(20, game.State.Stars[0].Planets[0].Inhabitants);
        }
    }

    [Fact]
    public void FinalConfiguredTurnSkipsInvestmentAndFurtherClassicAdvanceFinalizesEndgame() {
        var game = Scenario(3, 3, 20, 0.3); game.State.Config.MaxTurns = 4;
        for (int i = 0; i < 4; i++) AdvanceWithoutInvestment(game);
        Assert.Equal(4, game.Turn); Assert.Equal(3, game.State.Stars[0].Planets[0].Inhabitants);
        Assert.Equal(5, game.State.ProductionYear);
        Assert.Contains("game is over", Assert.Throws<InvalidOperationException>(() => game.BeginClassicTurn()).Message);
        Assert.NotNull(game.State.Result); Assert.True(game.State.Result.Reasons.HasFlag(ClassicEndReason.TurnLimit));
    }

    [Fact]
    public void RevoltUsesPostMovementWarshipPresenceAndUpdatesOwnershipWithoutCombatOrRng() {
        var game = Scenario(); var planet = game.State.Stars[0].Planets[0]; planet.Conquered = true;
        game.State.Players[1].Tf[1].Cruisers = 0;
        AdvanceWithoutInvestment(game);
        Assert.Equal(Ownership.Enemy, planet.Owner); Assert.False(planet.Conquered); Assert.Equal(100, planet.PlayerSeenCapacity);
        game = Scenario(); planet = game.State.Stars[0].Planets[0]; planet.Conquered = true; game.State.Players[1].Tf[1].Cruisers = 0;
        game.State.Stars[1].X = 1;
        game.State.Board[4, 0].StarRef = null; game.State.Board[1, 0].StarRef = game.State.Stars[1];
        var guard = game.State.FleetsSys.Place(1, 3, 1, scouts: 0); guard.Cruisers = 1;
        game.State.FleetsSys.SetCourse(1, 3, 0); AdvanceWithoutInvestment(game);
        Assert.Equal(Ownership.Player, planet.Owner); Assert.True(planet.Conquered); Assert.Equal(0, guard.ETA);
    }

    [Fact]
    public void ProductionAutoMergeUsesDestinationNotTheManualJoinXOnlyDefect() {
        var game = Scenario(20, 6); var remoteStar = game.State.Stars[1];
        game.State.Board[4, 0].StarRef = null; remoteStar.X = 0; remoteStar.Y = 4; game.State.Board[0, 4].StarRef = remoteStar;
        var remote = game.State.FleetsSys.Place(1, 3, 1, scouts: 5);
        OpenBank(game); SpendAndFinish(game, 6); game.CompleteClassicTurn();
        Assert.Equal(1, remote.Destination); Assert.Equal(5, remote.Scouts);
        Assert.Equal(2, game.State.Players[1].Tf[2].Scouts);
    }

    [Fact]
    public void MidCycleSlotBoundaryKeepsQueueAndBudgetEffectsWithoutRepeatingGrowthOnRecovery() {
        var game = Scenario(5, 5, 20, 0.3); var other = game.State.Stars[1];
        other.Planets.Add(new Planet(other) { Number = 1, Capacity = 20, Owner = Ownership.Player, Inhabitants = 5, IndustrialUnits = 5 });
        for (int i = 1; i <= 25; i++) game.State.FleetsSys.Place(1, i, 1, scouts: 1);
        var bank = OpenBank(game); Assert.Equal(26, bank.FleetIndex); Assert.Equal(7, bank.Balance);
        Assert.Null(game.InvestInClassicColony('S')); Assert.Null(game.InvestInClassicColony('R'));
        Assert.Contains("free task-force slot", Assert.Throws<InvalidOperationException>(() => game.FinishClassicInvestmentLine()).Message);
        Assert.Null(game.State.Investment); Assert.True(game.State.ClassicTurnInProgress); Assert.Equal(3, game.Turn);
        Assert.Throws<InvalidOperationException>(() => game.PrepareClassicEconomy());
        Assert.Equal(7, game.State.Stars[0].Planets[0].Inhabitants); Assert.Equal(7, other.Planets[0].Inhabitants);
        game.State.FleetsSys.JoinPlayerFleets(1, new[] { 2 }); game.PrepareClassicEconomy();
        Assert.Equal(2, game.State.Investment!.FleetIndex); Assert.Equal(7, game.State.Investment.Balance);
        SpendAndFinish(game, 7); game.CompleteClassicTurn();
        Assert.Equal(4, game.Turn); Assert.Equal(8, game.State.Players[1].RangeResearchProgress);
        Assert.Equal(7, other.Planets[0].Inhabitants);
    }

    [Fact]
    public void VelocityResearchOccursAfterMovementAndOnlyChangesNewJourneyEtas() {
        var game = Scenario(40, 40); var destination = game.State.Stars[1];
        game.State.Board[4, 0].StarRef = null; destination.X = 10; game.State.Board[10, 0].StarRef = destination;
        var oldJourney = game.State.FleetsSys.Place(1, 3, 0, scouts: 1); game.State.FleetsSys.SetCourse(1, 3, 1);
        OpenBank(game); Assert.Equal((6, 4), (oldJourney.ETA, oldJourney.X));
        Assert.Null(game.InvestInClassicColony('V', 40)); game.FinishClassicInvestmentLine(); game.CompleteClassicTurn();
        Assert.Equal(2, game.State.Players[1].Velocity); Assert.Equal((6, 10), (oldJourney.ETA, oldJourney.EtaOriginal));
        var newJourney = game.State.FleetsSys.Place(1, 4, 0, scouts: 1); game.State.FleetsSys.SetCourse(1, 4, 1);
        Assert.Equal((5, 5), (newJourney.ETA, newJourney.EtaOriginal));
        AdvanceWithoutInvestment(game); Assert.Equal((5, 4), (oldJourney.ETA, newJourney.ETA));
    }
}
