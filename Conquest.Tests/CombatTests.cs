using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.Tests;

public sealed class CombatTests {
    private sealed class Rng(params int[] draws) : IRng {
        private readonly Queue<int> _draws = new(draws);
        public int Calls { get; private set; }
        public int Next(int minInclusive, int maxExclusive) {
            Assert.Equal((1, 101), (minInclusive, maxExclusive)); Calls++;
            int value = _draws.Dequeue(); Assert.InRange(value, 1, 100); return value;
        }
        public void Reseed(int seed) => throw new NotSupportedException();
    }
    private static Game Scenario(IRng rng, Ownership owner = Ownership.None, int population = 10) {
        var state = new GameState(new GameConfig { NumStars = 2, SafeExplore = true }); state.Stars[1].X = 4;
        foreach (var s in state.Stars) { s.ExploredBy[1] = true; state.Board[s.X, s.Y].StarRef = s; }
        state.Stars[0].Planets.Add(new Planet(state.Stars[0]) { Number = 3, Owner = owner, Capacity = 20,
            PlayerSeenCapacity = 20, Inhabitants = population, IndustrialUnits = population });
        state.Players[0].Weapons = state.Players[1].Weapons = 3;
        state.Players[1].Velocity = 1; state.Players[1].Range = 5; state.Players[1].PopulationGrowthRate = 0.3;
        var f = state.FleetsSys.Place(1, 1, 0, scouts: 2); f.Transports = 8; f.Cruisers = 1;
        return new Game(rng, state);
    }
    private static void BeginAttack(Game game) { game.BeginClassicTurn(); game.PrepareClassicCombat(); Assert.NotNull(game.State.Attack); }

    [Theory]
    [InlineData(0, 20, 10, 10, 0)]
    [InlineData(-1, 20, 10, 10, 0)]
    [InlineData(1, 19, 9, 9, 1)]
    [InlineData(100, 15, 5, 5, 5)]
    public void BombardmentUsesWeaponsAndGunFactorsWithFourFactorsPerUnitAndNoRngOrTurn(int request, int capacity, int population, int iu, int killed) {
        var rng = new Rng(); var game = Scenario(rng); var p = game.State.Stars[0].Planets[0];
        Assert.Equal(5, game.BombardmentMaximum(1)); Assert.Equal(killed, game.BombardPlanet(1, 3, request));
        Assert.Equal((capacity, population, iu), (p.Capacity, p.Inhabitants, p.IndustrialUnits));
        Assert.Equal(killed == 0 ? 0 : 1, game.State.Players[1].Tf[1].Blasting);
        Assert.Equal(0, game.Turn); Assert.Equal(0, rng.Calls);
        Assert.Equal((8, 2, 1), (game.State.Players[1].Tf[1].Transports, game.State.Players[1].Tf[1].Scouts, game.State.Players[1].Tf[1].Cruisers));
    }

    [Fact]
    public void BombardmentClearsDestroyedConqueredColonyAndRefreshesCapacityWithoutChangingFleetContents() {
        var game = Scenario(new Rng(), Ownership.Player, 4); var p = game.State.Stars[0].Planets[0];
        p.Conquered = true; p.MissileBases = 2; p.AdvancedMissileBases = 1; p.EnemySeenOwner = Ownership.Player;
        Assert.Equal(5, game.BombardPlanet(1, 3, 5));
        Assert.Equal((15, 15, 0, 0, 0, 0), (p.Capacity, p.PlayerSeenCapacity, p.Inhabitants, p.IndustrialUnits, p.MissileBases, p.AdvancedMissileBases));
        Assert.Equal(Ownership.None, p.Owner); Assert.Equal(Ownership.None, p.EnemySeenOwner); Assert.False(p.Conquered);
        game.State.FleetsSys.LandPlayerTransports(1, 3, 2); // C L deliberately permits a blasting fleet.
        Assert.Equal(Ownership.Player, p.Owner); Assert.Equal(2, p.Inhabitants);
    }

    [Theory]
    [InlineData(Ownership.Enemy, false, " !Conquer it first!")]
    [InlineData(Ownership.Enemy, true, " !Conquer it first!")]
    [InlineData(Ownership.Player, false, " !That is a human colony!!")]
    public void BombardmentRejectsEnemyAndUnconqueredHumanTargetsEvenForZeroRequest(Ownership owner, bool conquered, string error) {
        var game = Scenario(new Rng(), owner); var p = game.State.Stars[0].Planets[0]; p.Conquered = conquered;
        Assert.Equal(error, Assert.Throws<InvalidOperationException>(() => game.BombardPlanet(1, 3, 0)).Message);
        Assert.Equal(20, p.Capacity); Assert.Equal(0, game.State.Players[1].Tf[1].Blasting);
    }

    [Theory]
    [InlineData(0, " !Illegal tf")]
    [InlineData(27, " !Illegal tf")]
    [InlineData(26, " !Nonexistent tf")]
    public void InvalidBombardingFleetIsRejected(int fleet, string error) {
        var game = Scenario(new Rng()); Assert.Equal(error, Assert.Throws<InvalidOperationException>(() => game.BombardPlanet(fleet, 3, 1)).Message);
    }

    [Fact]
    public void BombardingEligibilityAndFlagsFollowOriginalOrderAndResetWithMovement() {
        var game = Scenario(new Rng()); var f = game.State.Players[1].Tf[1];
        game.State.FleetsSys.SetCourse(1, 1, 1);
        Assert.Equal(" !Tf is not in normal space", Assert.Throws<InvalidOperationException>(() => game.BombardPlanet(1, 3, 1)).Message);
        game.State.FleetsSys.PreparePlayerCourse(1); f.Cruisers = 0;
        Assert.Equal(" !Tf has no warships", Assert.Throws<InvalidOperationException>(() => game.BombardPlanet(1, 3, 1)).Message);
        f.Cruisers = 1; f.Withdrew = 1; game.BombardPlanet(1, 3, 1); Assert.Equal(1, f.Withdrew);
        Assert.Equal(" !Tf is already blasting", Assert.Throws<InvalidOperationException>(() => game.BombardPlanet(1, 3, 1)).Message);
        Assert.Throws<InvalidOperationException>(() => game.State.FleetsSys.SetCourse(1, 1, 1));
        game.Tick(); Assert.Equal(0, f.Blasting); game.BombardPlanet(1, 3, 1);
    }

    [Fact]
    public void BombardingNoPlanetsWrongTargetOverflowAndZeroCapacityAreHandledWithoutSubstituteDamage() {
        var game = Scenario(new Rng()); var star = game.State.Stars[0];
        Assert.Equal(" !No such planet at this star", Assert.Throws<InvalidOperationException>(() => game.BombardPlanet(1, 6, 1)).Message);
        Assert.Throws<InvalidOperationException>(() => game.BombardPlanet(1, 3, int.MaxValue)); Assert.Equal(20, star.Planets[0].Capacity);
        star.Planets[0].Capacity = 0; Assert.Equal(0, game.BombardPlanet(1, 3, 1)); Assert.Equal(1, game.State.Players[1].Tf[1].Blasting);
        game.State.Players[1].Tf[1].Blasting = 0; star.Planets.Clear();
        Assert.Equal(" !No planets at star A", Assert.Throws<InvalidOperationException>(() => game.BombardPlanet(1, 3, 1)).Message);
    }

    [Fact]
    public void FirstSalvoUsesStrictComparisonExactDrawOrderAndNeverRollsForTransportsOrScouts() {
        // Equal 80/100 survives at exp(log(.8)*1). Attacker C survives, B dies;
        // M dies and A survives. All strengths are frozen for this salvo.
        var rng = new Rng(80, 81, 81, 80); var game = Scenario(rng, Ownership.Enemy);
        var f = game.State.Players[1].Tf[1]; f.BattleStars = 1;
        var p = game.State.Stars[0].Planets[0]; p.MissileBases = p.AdvancedMissileBases = 1;
        BeginAttack(game); var result = game.FirePlayerSalvo(1, 3);
        Assert.Equal((141, 141), (result.AttackerForces, result.DefenderForces)); Assert.Equal(0.8, result.AttackerSurvival!.Value, 12);
        Assert.Equal((0, 1, 1, 0), (result.CruisersLost, result.BattleStarsLost, result.MissileBasesLost, result.AdvancedBasesLost));
        Assert.Equal((8, 2, 1, 0), (f.Transports, f.Scouts, f.Cruisers, f.BattleStars)); Assert.Equal(4, rng.Calls); Assert.Equal(1, result.LossPasses);
        Assert.Equal(Ownership.Enemy, p.Owner); Assert.True(p.UnderAttack); Assert.False(game.State.Attack!.AwaitingAcknowledgement);
    }

    [Fact]
    public void FirstNoLossSalvoStopsButLaterAttackerOnlyLossesRepeatWithFrozenProbabilities() {
        var rng = new Rng(1, 1, 1, 100, 1, 1, 100, 1, 100); var game = Scenario(rng, Ownership.Enemy);
        game.State.Players[1].Tf[1].Cruisers = 2; var p = game.State.Stars[0].Planets[0]; p.MissileBases = 1;
        BeginAttack(game); var first = game.FirePlayerSalvo(1, 3); Assert.Equal(1, first.LossPasses); Assert.Equal(3, rng.Calls);
        var next = game.FirePlayerSalvo(1, 3);
        Assert.Equal(3, next.LossPasses); Assert.Equal((2, 1), (next.CruisersLost, next.MissileBasesLost)); Assert.Equal(9, rng.Calls);
        Assert.Equal(Ownership.Enemy, p.Owner); // No surviving warship to claim the now undefended planet.
        Assert.True(game.State.Attack!.AwaitingAcknowledgement); Assert.Equal(0, game.State.Players[1].Tf[1].Destination);
        Assert.False(p.UnderAttack);
    }

    [Fact]
    public void UndefendedPlanetFallsWithoutRandomnessPopulationDamageOrCapacityRefreshAndWaitsForAcknowledgement() {
        var rng = new Rng(); var game = Scenario(rng, Ownership.Enemy); var p = game.State.Stars[0].Planets[0]; p.PlayerSeenCapacity = 15;
        BeginAttack(game); var result = game.FirePlayerSalvo(1, 3);
        Assert.True(result.Captured); Assert.Null(result.AttackerSurvival); Assert.Equal(0, rng.Calls);
        Assert.Equal(Ownership.Player, p.Owner); Assert.Equal(Ownership.Player, p.EnemySeenOwner); Assert.True(p.Conquered);
        Assert.Equal((10, 10, 20, 15), (p.Inhabitants, p.IndustrialUnits, p.Capacity, p.PlayerSeenCapacity));
        Assert.Throws<InvalidOperationException>(() => game.PrepareClassicEconomy()); Assert.Equal(0, game.Turn);
        game.AcknowledgePlayerAttack(); game.PrepareClassicEconomy(); game.CompleteClassicTurn(); Assert.Equal(1, game.Turn);
    }

    [Fact]
    public void LossOfAllShipsCleansFleetAndBreakOffClearsAllAttackFlagsWithoutMovingOrRetreating() {
        var rng = new Rng(100, 1); var game = Scenario(rng, Ownership.Enemy); var p = game.State.Stars[0].Planets[0]; p.MissileBases = 1;
        var f = game.State.Players[1].Tf[1]; f.Transports = f.Scouts = 0;
        BeginAttack(game); game.FirePlayerSalvo(1, 3); Assert.Null(f.Destination); Assert.Null(game.State.Board[0, 0].TaskForces[1]);
        Assert.Equal(Ownership.Enemy, p.Owner); Assert.Equal(2, rng.Calls);
        game = Scenario(new Rng(), Ownership.Enemy); BeginAttack(game); p = game.State.Stars[0].Planets[0]; p.UnderAttack = true;
        game.EndPlayerAttack(); Assert.False(p.UnderAttack); Assert.Equal(Ownership.Enemy, p.Owner);
        Assert.Equal((0, 0), (game.State.Players[1].Tf[1].ETA, game.State.Players[1].Tf[1].Withdrew));
    }

    [Fact]
    public void CaptureCanUseAnotherStationaryFleetAfterSelectedFleetIsDestroyed() {
        var rng = new Rng(100, 100); var game = Scenario(rng, Ownership.Enemy); var star = game.State.Stars[0]; star.Planets[0].MissileBases = 1;
        game.State.Players[1].Tf[1].Transports = game.State.Players[1].Tf[1].Scouts = 0;
        var helper = game.State.FleetsSys.Place(1, 2, 0, scouts: 0); helper.Cruisers = 1;
        BeginAttack(game); Assert.True(game.FirePlayerSalvo(1, 3).Captured); Assert.Null(game.State.Players[1].Tf[1].Destination);
        Assert.Equal(Ownership.Player, star.Planets[0].Owner); Assert.Equal(1, helper.Cruisers);
    }

    [Fact]
    public void LosingLastGuardRevoltsAnEarlierCaptureAtEndOfAttack() {
        var rng = new Rng(100, 1); var game = Scenario(rng, Ownership.Enemy); var star = game.State.Stars[0];
        var second = new Planet(star) { Number = 6, Owner = Ownership.Enemy, Capacity = 30, PlayerSeenCapacity = 12,
            Inhabitants = 9, IndustrialUnits = 14, MissileBases = 1 }; star.Planets.Add(second);
        BeginAttack(game); Assert.True(game.FirePlayerSalvo(1, 3).Captured);
        Assert.False(game.FirePlayerSalvo(1, 6).Captured);
        Assert.True(game.State.Attack!.AwaitingAcknowledgement);
        Assert.Equal(Ownership.Enemy, star.Planets[0].Owner); Assert.False(star.Planets[0].Conquered);
        Assert.Equal(Ownership.Player, star.Planets[0].EnemySeenOwner); // C revolt doesn't repair this intelligence.
        Assert.Equal((9, 14, 30, 12), (second.Inhabitants, second.IndustrialUnits, second.Capacity, second.PlayerSeenCapacity));
        Assert.All(star.Planets, p => Assert.False(p.UnderAttack)); Assert.Equal(2, rng.Calls);
    }

    [Fact]
    public void PlanetAttackUsesWeaponsAndCapsForceRatiosAtFourteen() {
        var rng = new Rng(1, 100); var game = Scenario(rng, Ownership.Enemy);
        game.State.Players[1].Weapons = 1000; game.State.Players[0].Weapons = 2;
        game.State.Stars[0].Planets[0].MissileBases = 1; BeginAttack(game);
        var result = game.FirePlayerSalvo(1, 3);
        Assert.Equal((7000, 14), (result.AttackerForces, result.DefenderForces));
        Assert.Equal(Math.Exp(Math.Log(.8) * 14), result.DefenderSurvival);
        Assert.Equal(Math.Exp(Math.Log(.8) * (14.0 / 7000)), result.AttackerSurvival);
        Assert.True(result.Captured); Assert.Equal(2, rng.Calls);
    }

    [Fact]
    public void BombardmentClampsOverpopulationAndPreservesOriginalAlreadyNeutralFlags() {
        var game = Scenario(new Rng(), population: 30); var p = game.State.Stars[0].Planets[0];
        p.IndustrialUnits = 100; game.BombardPlanet(1, 3, 1);
        Assert.Equal((19, 19, 38), (p.Capacity, p.Inhabitants, p.IndustrialUnits));
        game = Scenario(new Rng(), population: 0); p = game.State.Stars[0].Planets[0];
        p.Conquered = true; p.EnemySeenOwner = Ownership.Enemy; p.UnderAttack = true;
        game.State.FleetsSys.Place(0, 1, 0); // B itself doesn't require fleet combat or test enemy presence.
        game.BombardPlanet(1, 3, 1);
        Assert.True(p.Conquered); Assert.True(p.UnderAttack); Assert.Equal(Ownership.Enemy, p.EnemySeenOwner);
    }

    [Fact]
    public void OrderedAttackSessionsSurviveNewGameFacadeAndPreventPrematureClockUpdates() {
        var rng = new Rng(); var game = Scenario(rng, Ownership.Enemy); var star = game.State.Stars[1];
        star.Planets.Add(new Planet(star) { Number = 1, Owner = Ownership.Enemy, Capacity = 20 });
        game.State.FleetsSys.Place(1, 2, 1, scouts: 0).Cruisers = 1; BeginAttack(game);
        Assert.Throws<InvalidOperationException>(() => game.Tick());
        Assert.Throws<InvalidOperationException>(() => game.CompleteClassicTurn());
        game.FirePlayerSalvo(1, 3);
        game = new Game(rng, game.State); game.PrepareClassicCombat();
        Assert.Equal(0, game.State.Attack!.StarIndex); game.AcknowledgePlayerAttack();
        Assert.Equal(1, game.State.Attack!.StarIndex); game.EndPlayerAttack(); game.AcknowledgePlayerAttack();
        game.PrepareClassicEconomy(); game.CompleteClassicTurn(); Assert.Equal(1, game.Turn); Assert.Equal(0, rng.Calls);
    }

    [Fact]
    public void SelectionValidationAndPreparingAutomaticEncounterConsumeNoBattleRng() {
        var rng = new Rng(); var game = Scenario(rng, Ownership.Enemy); BeginAttack(game);
        Assert.Equal("! That is not a useable planet", Assert.Throws<InvalidOperationException>(() => game.FirePlayerSalvo(1, 9)).Message);
        Assert.Equal(" !Illegal tf", Assert.Throws<InvalidOperationException>(() => game.FirePlayerSalvo(0, 3)).Message);
        game.State.FleetsSys.Place(1, 2, 0, scouts: 1);
        Assert.Equal(" !Tf has no warships", Assert.Throws<InvalidOperationException>(() => game.FirePlayerSalvo(2, 3)).Message); Assert.Equal(0, rng.Calls);
        game = Scenario(rng, Ownership.Enemy); game.State.FleetsSys.Place(0, 1, 0, scouts: 1); game.BeginClassicTurn();
        game.PrepareClassicCombat(); Assert.NotNull(game.State.Battle); Assert.Null(game.State.Attack);
        Assert.Equal(0, rng.Calls); Assert.Equal(0, game.Turn); Assert.True(game.State.ClassicTurnInProgress);
    }

    [Fact]
    public void FourthTurnAttackOccursBeforeGrowthAndInvestmentAndEnemyAttackRequiresAcknowledgement() {
        var game = Scenario(new Rng(), Ownership.Enemy, 4);
        // Stay out of attack range until arrival on phase four.
        game.State.FleetsSys.Place(1, 1, 1, scouts: 2); game.State.Players[1].Tf[1].Cruisers = 1;
        game.State.FleetsSys.SetCourse(1, 1, 0);
        for (int i = 0; i < 3; i++) { game.BeginClassicTurn(); game.PrepareClassicEconomy(); game.CompleteClassicTurn(); }
        BeginAttack(game); game.FirePlayerSalvo(1, 3); game.AcknowledgePlayerAttack(); game.PrepareClassicEconomy();
        Assert.Equal(4, game.State.Investment!.Balance); // Captured/conquered: round(4*.3)/2 == 0 growth.
        Assert.Null(game.InvestInClassicColony('R', 4)); game.FinishClassicInvestmentLine(); game.CompleteClassicTurn(); Assert.Equal(4, game.Turn);
        game = Scenario(new Rng(), Ownership.Player); game.State.Players[1].Tf[1].Destination = null;
        var enemy = game.State.FleetsSys.Place(0, 1, 0, scouts: 0); enemy.Cruisers = 1; game.BeginClassicTurn();
        game.PrepareClassicCombat(); Assert.NotNull(game.State.EnemyAttack); Assert.Equal(0, game.Turn);
        game.AcknowledgeEnemyAttack(); Assert.Equal(Ownership.Enemy, game.State.Stars[0].Planets[0].Owner);
        game.AcknowledgeEnemyAttack(); game.PrepareClassicEconomy(); game.CompleteClassicTurn(); Assert.Equal(1, game.Turn);
    }
}
