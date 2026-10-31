using System.Text.Json;
using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.Tests;

public sealed class LandingTests {
    private sealed class NoDrawRng : IRng {
        public int Next(int minInclusive, int maxExclusive) => throw new InvalidOperationException("Unexpected RNG draw");
        public void Reseed(int seed) => throw new InvalidOperationException("Unexpected reseed");
    }
    private static Game Scenario() {
        var state = new GameState(new GameConfig { NumStars = 3, SafeExplore = true });
        var positions = new[] { (0, 0), (4, 0), (0, 4) };
        for (int i = 0; i < positions.Length; i++) {
            var star = state.Stars[i]; (star.X, star.Y) = positions[i];
            star.ExploredBy[1] = true; state.Board[star.X, star.Y].StarRef = star;
        }
        var home = state.Stars[0];
        home.Planets.Add(new Planet(home) { Number = 3, Capacity = 20, PlayerSeenCapacity = 20 });
        var fleet = state.FleetsSys.Place(1, 1, 0, scouts: 2); fleet.Transports = 8; fleet.Cruisers = 1;
        state.Players[1].Velocity = 1; state.Players[1].Range = 5; state.Players[1].Weapons = 3;
        return new Game(new NoDrawRng(), state);
    }
    private static string Snapshot(Game game) => JsonSerializer.Serialize(new {
        game.Turn, game.State.PlayerStartupStage, game.State.PlayerStartupBalance, game.State.PendingSharedStartEncounter,
        Players = game.State.Players.Select(p => new {
            p.Velocity, p.Range, p.Weapons, p.VelocityResearchProgress, p.RangeResearchProgress, p.WeaponsResearchProgress,
            Fleets = p.Tf.Skip(1).Select(f => new { f.Destination, f.ETA, f.EtaOriginal, f.X, f.Y,
                f.DepartureX, f.DepartureY, f.Transports, f.Scouts, f.Cruisers, f.BattleStars, f.Blasting, f.Withdrew })
        }),
        Stars = game.State.Stars.Select(s => new { s.X, s.Y, s.ExploredBy,
            Planets = s.Planets.Select(p => new { p.Number, p.Owner, p.Capacity, p.PlayerSeenCapacity, p.Inhabitants,
                p.IndustrialUnits, p.MissileBases, p.AdvancedMissileBases, p.Conquered, p.UnderAttack, p.EnemySeenOwner, p.EnemySeenDefense }) })
    });
    private static void RejectUnchanged(Game game, Action action, string error) {
        string before = Snapshot(game);
        Assert.Equal(error, Assert.Throws<InvalidOperationException>(action).Message);
        Assert.Equal(before, Snapshot(game));
    }

    [Theory]
    [InlineData(1, 1, 7)]
    [InlineData(3, 3, 5)]
    [InlineData(8, 8, 0)]
    [InlineData(0, 8, 0)]
    public void ColonizationImmediatelyConsumesTransportsAndAddsEqualPopulationAndIndustrialUnits(int request, int landed, int left) {
        var game = Scenario(); var planet = game.State.Stars[0].Planets[0];
        game.State.FleetsSys.LandPlayerTransports(1, 3, request);
        Assert.Equal(Ownership.Player, planet.Owner);
        Assert.Equal(landed, planet.Inhabitants); Assert.Equal(landed, planet.IndustrialUnits);
        Assert.Equal(left, game.State.Players[1].Tf[1].Transports);
        Assert.Equal(2, game.State.Players[1].Tf[1].Scouts); Assert.Equal(1, game.State.Players[1].Tf[1].Cruisers);
        Assert.Equal(0, game.State.Players[1].Tf[1].Destination); // Warships/scouts retain the fleet.
        Assert.Equal(20, planet.Capacity); Assert.Equal(20, planet.PlayerSeenCapacity);
        Assert.Equal(0, game.Turn);
    }

    [Fact]
    public void ExistingColonyReinforcementAddsRatherThanResetsPopulationIndustryOrOtherFields() {
        var game = Scenario(); var planet = game.State.Stars[0].Planets[0];
        planet.Owner = Ownership.Player; planet.Inhabitants = 4; planet.IndustrialUnits = 11;
        planet.MissileBases = 2; planet.AdvancedMissileBases = 1; planet.UnderAttack = true;
        planet.EnemySeenOwner = Ownership.None; planet.EnemySeenDefense = 7;
        game.State.FleetsSys.LandPlayerTransports(1, 3, 2);
        game.State.FleetsSys.LandPlayerTransports(1, 3, 1);
        Assert.Equal((7, 14, 2, 1), (planet.Inhabitants, planet.IndustrialUnits, planet.MissileBases, planet.AdvancedMissileBases));
        Assert.True(planet.UnderAttack); Assert.False(planet.Conquered);
        Assert.Equal(Ownership.Player, planet.Owner);
        Assert.Equal(Ownership.None, planet.EnemySeenOwner); Assert.Equal(7, planet.EnemySeenDefense);
        Assert.Equal(5, game.State.Players[1].Tf[1].Transports);
        Assert.Equal(0, game.Turn);
    }

    [Theory]
    [InlineData(-1, 20, 0, "  !illegal transports")]
    [InlineData(9, 20, 0, "  !only  8 transports in tf")]
    [InlineData(9, 1, 0, "  !only  8 transports in tf")]
    [InlineData(8, 5, 0, "  !only room for  5 transports")]
    [InlineData(0, 5, 0, "  !only room for  5 transports")]
    [InlineData(1, 0, 0, "  !only room for  0 transports")]
    [InlineData(1, 10, 10, "  !only room for  0 transports")]
    [InlineData(1, 10, 12, "  !only room for -2 transports")]
    public void QuantityValidationRejectsWithoutClampingOrPartialEffects(int request, int capacity, int population, string error) {
        var game = Scenario(); var planet = game.State.Stars[0].Planets[0];
        planet.Capacity = capacity; planet.Inhabitants = population;
        RejectUnchanged(game, () => game.State.FleetsSys.LandPlayerTransports(1, 3, request), error);
    }

    [Fact]
    public void ExactCapacityBoundaryIsAllowedAndUsesActualNotRememberedCapacity() {
        var game = Scenario(); var planet = game.State.Stars[0].Planets[0];
        planet.Owner = Ownership.Player; planet.Inhabitants = 15; planet.IndustrialUnits = 30;
        planet.PlayerSeenCapacity = 0;
        game.State.FleetsSys.LandPlayerTransports(1, 3, 5);
        Assert.Equal(20, planet.Inhabitants); Assert.Equal(35, planet.IndustrialUnits);
        Assert.Equal(0, planet.PlayerSeenCapacity); // L does not refresh intelligence or cap IU.
        RejectUnchanged(game, () => game.State.FleetsSys.LandPlayerTransports(1, 3, 1), "  !only room for  0 transports");
    }

    [Theory]
    [InlineData(Ownership.Enemy, false)]
    [InlineData(Ownership.Enemy, true)]
    [InlineData(Ownership.Player, true)]
    public void EnemyAndConqueredPlayerPlanetsAreRejectedBeforeQuantityValidation(Ownership owner, bool conquered) {
        var game = Scenario(); var planet = game.State.Stars[0].Planets[0];
        planet.Owner = owner; planet.Conquered = conquered;
        RejectUnchanged(game, () => game.State.FleetsSys.LandPlayerTransports(1, 3, -1), "  !Enemy infested planet  !!");
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 2, false)]
    [InlineData(1, 0, false)]
    [InlineData(null, 0, false)]
    public void EnemyFleetBlockingUsesStationaryDestinationRatherThanCoordinatesOrShipTypes(int? destination, int eta, bool blocked) {
        var game = Scenario(); var enemy = game.State.Players[0].Tf[1];
        enemy.Destination = destination; enemy.ETA = eta; enemy.X = enemy.Y = 14; enemy.Transports = 4;
        if (blocked) RejectUnchanged(game, () => game.State.FleetsSys.LandPlayerTransports(1, 3, 1), "  !enemy ships present");
        else {
            game.State.FleetsSys.LandPlayerTransports(1, 3, 1);
            Assert.Equal(Ownership.Player, game.State.Stars[0].Planets[0].Owner);
            Assert.Equal(destination, enemy.Destination); Assert.Equal(eta, enemy.ETA); Assert.Equal(4, enemy.Transports);
        }
    }

    [Fact]
    public void EmptyActiveEnemyStillBlocksAndNoPlanetsErrorHasPriorityOverEnemyPresence() {
        var game = Scenario(); var enemy = game.State.Players[0].Tf[1]; enemy.Destination = 0;
        RejectUnchanged(game, () => game.State.FleetsSys.LandPlayerTransports(1, 3, 1), "  !enemy ships present");
        game.State.Stars[0].Planets.Clear();
        RejectUnchanged(game, () => game.State.FleetsSys.LandPlayerTransports(1, 3, 1), "  !no planets at this star");
    }

    [Theory]
    [InlineData(0, "  !illegal tf")]
    [InlineData(27, "  !illegal tf")]
    [InlineData(26, "  !nonexistent tf")]
    public void InvalidOrUnusedFleetsCannotLandEvenWithStaleTransportCounts(int fleet, string error) {
        var game = Scenario(); game.State.Players[1].Tf[26].Transports = 99;
        RejectUnchanged(game, () => game.State.FleetsSys.LandPlayerTransports(fleet, 3, 1), error);
    }

    [Fact]
    public void PendingCourseAndInFlightFleetCannotLandOrCancelTheirMovement() {
        var game = Scenario(); game.State.FleetsSys.SetCourse(1, 1, 1);
        RejectUnchanged(game, () => game.State.FleetsSys.LandPlayerTransports(1, 3, 1), "  !tf is not in normal space");
        game.Tick();
        RejectUnchanged(game, () => game.State.FleetsSys.LandPlayerTransports(1, 3, 1), "  !tf is not in normal space");
    }

    [Fact]
    public void InvalidPlanetNumbersDoNotFallbackToAnotherPlanet() {
        var game = Scenario(); var star = game.State.Stars[0];
        RejectUnchanged(game, () => game.State.FleetsSys.LandPlayerTransports(1, 1, 1), " !Not a habitable planet");
        star.Planets.Add(new Planet(star) { Number = 6, Capacity = 40 });
        RejectUnchanged(game, () => game.State.FleetsSys.LandPlayerTransports(1, 2, 1), " !Not a habitable planet");
        game.State.FleetsSys.LandPlayerTransports(1, 6, 2);
        Assert.Equal(Ownership.None, star.Planets[0].Owner); Assert.Equal(0, star.Planets[0].Inhabitants);
        Assert.Equal(Ownership.Player, star.Planets[1].Owner); Assert.Equal(2, star.Planets[1].Inhabitants);
    }

    [Theory]
    [InlineData(0, "  !illegal transports")]
    [InlineData(1, "  !only  0 transports in tf")]
    public void FleetsWithoutTransportsUseOriginalQuantityDependentErrors(int request, string error) {
        var game = Scenario(); game.State.Players[1].Tf[1].Transports = 0;
        RejectUnchanged(game, () => game.State.FleetsSys.LandPlayerTransports(1, 3, request), error);
    }

    [Fact]
    public void LandingAllShipsRemovesFleetButRetainsColonyStarAndOtherFleetReferences() {
        var game = Scenario(); var system = game.State.FleetsSys; var fleet = game.State.Players[1].Tf[1];
        fleet.Scouts = fleet.Cruisers = fleet.BattleStars = 0;
        var other = system.Place(1, 2, 0, scouts: 1);
        var enemy = system.Place(0, 1, 1, scouts: 2);
        system.LandPlayerTransports(1, 3, 0);
        Assert.Null(fleet.Destination); Assert.Equal(0, fleet.Transports);
        Assert.Equal(Ownership.Player, game.State.Stars[0].Planets[0].Owner);
        Assert.Same(game.State.Stars[0], game.State.Board[0, 0].StarRef);
        Assert.Same(other, game.State.Board[0, 0].TaskForces[1]); Assert.Equal(2, game.State.Board[0, 0].Tf);
        Assert.Same(enemy, game.State.Board[4, 0].TaskForces[0]); Assert.Equal(2, enemy.Scouts);
        Assert.Equal(0, game.Turn);
    }

    [Fact]
    public void LandingPreservesSourceFlagsVisibilityAndResearchWithoutInventingBlastingRestriction() {
        var game = Scenario(); var fleet = game.State.Players[1].Tf[1]; var star = game.State.Stars[0];
        fleet.Blasting = 1; fleet.Withdrew = 1; fleet.EtaOriginal = 9;
        star.ExploredBy[1] = false; star.Planets[0].Conquered = true; // None + conquered is not blocked in C.
        game.State.Players[1].RangeResearchProgress = 7;
        game.State.FleetsSys.LandPlayerTransports(1, 3, 2);
        Assert.Equal((1, 1, 9), (fleet.Blasting, fleet.Withdrew, fleet.EtaOriginal));
        Assert.False(star.ExploredBy[1]); Assert.True(star.Planets[0].Conquered);
        Assert.Equal(7, game.State.Players[1].RangeResearchProgress);
        Assert.Equal((1, 5, 3), (game.State.Players[1].Velocity, game.State.Players[1].Range, game.State.Players[1].Weapons));
        Assert.Equal(0, game.Turn);
    }

    [Fact]
    public void SubsequentMovementPhasesDoNotInventPopulationGrowthOrProduction() {
        var game = Scenario(); game.State.FleetsSys.LandPlayerTransports(1, 3, 3);
        game.Tick(5);
        var planet = game.State.Stars[0].Planets[0];
        Assert.Equal((3, 3, 20), (planet.Inhabitants, planet.IndustrialUnits, planet.Capacity));
        Assert.Equal(5, game.Turn);
    }
}
