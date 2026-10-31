using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.Tests;

public sealed class FleetLifecycleTests {
    private sealed class NoDrawRng : IRng {
        public int Next(int minInclusive, int maxExclusive) => throw new InvalidOperationException("Unexpected RNG draw");
        public void Reseed(int seed) => throw new InvalidOperationException("Unexpected reseed");
    }
    private static Game Scenario() {
        var state = new GameState(new GameConfig { NumStars = 3, SafeExplore = true });
        var positions = new[] { (0, 0), (0, 4), (4, 0) };
        for (int i = 0; i < positions.Length; i++) {
            var star = state.Stars[i]; (star.X, star.Y) = positions[i];
            state.Board[star.X, star.Y].StarRef = star; star.ExploredBy[1] = true;
        }
        state.Players[1].Range = 5; state.Players[1].Velocity = 1; state.Players[1].Weapons = 3;
        var source = state.FleetsSys.Place(1, 1, 0, scouts: 6);
        source.Transports = 8; source.Cruisers = 3; source.BattleStars = 2;
        return new Game(new NoDrawRng(), state);
    }
    private static (int, int, int, int) Ships(TaskForce f) => (f.Transports, f.Scouts, f.Cruisers, f.BattleStars);
    private static string Snapshot(Game game) => game.Turn + ":" + string.Join(";", game.State.Players.SelectMany(p => p.Tf.Skip(1))
        .Select(f => $"{f.Destination},{f.ETA},{f.EtaOriginal},{f.X},{f.Y},{f.DepartureX},{f.DepartureY},{f.Blasting},{f.Withdrew},{Ships(f)}"));

    [Fact]
    public void SplitTransfersAllShipTypesSequentiallyAndClampsToRemainingAvailability() {
        var game = Scenario(); var system = game.State.FleetsSys;
        var result = system.SplitPlayerFleet(1, new[] { ('T', 3), ('S', 2), ('C', 1), ('B', 1), ('S', 99) });
        Assert.Equal((2, (string?)null), result);
        Assert.Equal((5, 0, 2, 1), Ships(game.State.Players[1].Tf[1]));
        Assert.Equal((3, 6, 1, 1), Ships(game.State.Players[1].Tf[2]));
        Assert.Equal(0, game.State.Players[1].Tf[2].Destination);
        Assert.Equal(0, game.Turn);
    }

    [Fact]
    public void BlankSplitMovesEntireFleetAndReusesFirstUnusedSlotWithoutRetainingOldShips() {
        var game = Scenario(); var old = game.State.Players[1].Tf[2];
        old.Transports = 999; old.Scouts = 999; old.Cruisers = 999; old.BattleStars = 999;
        old.ETA = 9; old.EtaOriginal = 10; old.X = old.Y = old.DepartureX = old.DepartureY = 8;
        old.Blasting = 1; old.Withdrew = 1;
        var result = game.State.FleetsSys.SplitPlayerFleet(1, Array.Empty<(char, int)>());
        Assert.Equal(2, result.Slot);
        Assert.Null(game.State.Players[1].Tf[1].Destination);
        Assert.Equal((0, 0, 0, 0), Ships(game.State.Players[1].Tf[1]));
        Assert.Equal((8, 6, 3, 2), Ships(old));
        Assert.Equal((0, 0, 0, 0, 0, 0, 0), (old.ETA, old.EtaOriginal, old.X, old.Y, old.DepartureX, old.DepartureY, old.Blasting));
        Assert.Equal(1, old.Withdrew); // get_tf does not initialize this field.
        Assert.Same(old, game.State.Board[0, 0].TaskForces[1]);
        Assert.Equal(2, game.State.Board[0, 0].Tf);
        Assert.Equal(0, game.Turn);
    }

    [Fact]
    public void AllocationMayChooseAnEarlierSlotThanSourceAndEmptySourcesAreRemoved() {
        var game = Scenario(); var player = game.State.Players[1];
        player.Tf[1].Destination = null;
        game.State.FleetsSys.Place(1, 5, 0, scouts: 2);
        Assert.Equal(1, game.State.FleetsSys.SplitPlayerFleet(5, Array.Empty<(char, int)>()).Slot);
        Assert.Null(player.Tf[5].Destination); Assert.Equal(2, player.Tf[1].Scouts);
        player.Tf[1].Scouts = 0; player.Tf[1].Transports = player.Tf[1].Cruisers = player.Tf[1].BattleStars = 0;
        game.State.FleetsSys.SplitPlayerFleet(1, Array.Empty<(char, int)>());
        Assert.Null(player.Tf[1].Destination); Assert.Null(player.Tf[2].Destination);
        Assert.Null(game.State.Board[0, 0].TaskForces[1]);
    }

    [Theory]
    [InlineData('S', 0, null)]
    [InlineData('X', 1, "  ! Illegal field X")]
    [InlineData('S', -1, "  !invalid ship quantity")]
    public void EmptyOrInvalidSplitSelectionReleasesUnusedNewSlot(char type, int quantity, string? expectedError) {
        var game = Scenario(); var before = Ships(game.State.Players[1].Tf[1]);
        var result = game.State.FleetsSys.SplitPlayerFleet(1, new[] { (type, quantity) });
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(before, Ships(game.State.Players[1].Tf[1]));
        Assert.Null(game.State.Players[1].Tf[result.Slot].Destination);
        Assert.Equal(2, game.State.FleetsSys.ValidatePlayerSplit(1));
    }

    [Fact]
    public void InvalidSplitTokensDoNotRollBackValidTransfersAndLastErrorIsRetained() {
        var game = Scenario();
        var result = game.State.FleetsSys.SplitPlayerFleet(1, new[] { ('X', 1), ('S', 2), ('?', 1), ('T', 3) });
        Assert.Equal("  ! Illegal field ?", result.Error);
        Assert.Equal((3, 2, 0, 0), Ships(game.State.Players[1].Tf[2]));
        Assert.Equal((5, 4, 3, 2), Ships(game.State.Players[1].Tf[1]));
    }

    [Fact]
    public void SlotLimitRejectsEvenWholeFleetSplitBeforeChangingSource() {
        var game = Scenario();
        for (int i = 2; i <= 26; i++) game.State.FleetsSys.Place(1, i, 0);
        string before = Snapshot(game);
        Assert.Equal(0, game.State.FleetsSys.ValidatePlayerSplit(1));
        Assert.Equal("! Taskforce limit reached!", Assert.Throws<InvalidOperationException>(() =>
            game.State.FleetsSys.SplitPlayerFleet(1, Array.Empty<(char, int)>())).Message);
        Assert.Equal(before, Snapshot(game));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(27)]
    [InlineData(26)]
    public void InvalidOrDestroyedSplitSourcesDoNotAllocate(int slot) {
        var game = Scenario(); string before = Snapshot(game);
        Assert.Equal("  !illegal tf", Assert.Throws<InvalidOperationException>(() =>
            game.State.FleetsSys.SplitPlayerFleet(slot, new[] { ('S', 1) })).Message);
        Assert.Equal(before, Snapshot(game));
    }

    [Fact]
    public void PendingMovementAndBlastingBlockSplitWithoutCancellingOrders() {
        var game = Scenario(); var source = game.State.Players[1].Tf[1];
        game.State.FleetsSys.SetCourse(1, 1, 2);
        string before = Snapshot(game);
        Assert.Throws<InvalidOperationException>(() => game.State.FleetsSys.SplitPlayerFleet(1, new[] { ('S', 1) }));
        Assert.Equal(before, Snapshot(game));
        game.State.FleetsSys.PreparePlayerCourse(1); source.Blasting = 1;
        before = Snapshot(game);
        Assert.Equal(" !Tf is blasting a planet", Assert.Throws<InvalidOperationException>(() =>
            game.State.FleetsSys.SplitPlayerFleet(1, new[] { ('S', 1) })).Message);
        Assert.Equal(before, Snapshot(game));
    }

    [Fact]
    public void JoinMergesAllTypesMarksChildrenUnusedAndCanReuseTheirSlots() {
        var game = Scenario(); var system = game.State.FleetsSys;
        var child = system.Place(1, 2, 0, scouts: 2); child.Transports = 3; child.Cruisers = 1; child.BattleStars = 1;
        var third = system.Place(1, 3, 0, scouts: 1);
        Assert.Null(system.JoinPlayerFleets(1, new[] { 2, 3 }));
        Assert.Equal((11, 9, 4, 3), Ships(game.State.Players[1].Tf[1]));
        Assert.Null(child.Destination); Assert.Null(third.Destination);
        Assert.Equal((3, 2, 1, 1), Ships(child)); // C joinsilent retains inactive slot counts.
        Assert.Equal(2, system.SplitPlayerFleet(1, new[] { ('S', 1) }).Slot);
        Assert.Equal((0, 1, 0, 0), Ships(child));
        Assert.Equal(0, game.Turn);
    }

    [Fact]
    public void JoinProcessesChildrenSequentiallyThroughErrorsAndDuplicatesWithoutDoubleCounting() {
        var game = Scenario(); var system = game.State.FleetsSys;
        system.Place(1, 2, 0, scouts: 1); system.Place(1, 3, 0, scouts: 2);
        Assert.Equal("!Nonexistant TFZ", system.JoinPlayerFleets(1, new[] { 1, 0, 2, 2, 3, 26 }));
        Assert.Equal((8, 9, 3, 2), Ships(game.State.Players[1].Tf[1]));
        Assert.Null(game.State.Players[1].Tf[2].Destination); Assert.Null(game.State.Players[1].Tf[3].Destination);
    }

    [Fact]
    public void ClassicJoinPreservesDocumentedSameXDifferentYDefectWithoutRevealingRemoteStar() {
        var game = Scenario(); var system = game.State.FleetsSys;
        game.State.Stars[1].ExploredBy[1] = false;
        var child = system.Place(1, 2, 1, scouts: 2);
        var enemy = system.Place(0, 1, 1, scouts: 4);
        Assert.Null(system.JoinPlayerFleets(1, new[] { 2 }));
        Assert.Equal(8, game.State.Players[1].Tf[1].Scouts);
        Assert.Null(child.Destination);
        Assert.False(game.State.Stars[1].ExploredBy[1]);
        Assert.Null(game.State.Board[0, 4].TaskForces[1]); Assert.Equal(-1, game.State.Board[0, 4].Tf);
        Assert.Same(enemy, game.State.Board[0, 4].TaskForces[0]);
        Assert.Same(game.State.Stars[1], game.State.Board[0, 4].StarRef);
    }

    [Theory]
    [InlineData(2, 0, 0, "  !tfB bad location")]
    [InlineData(2, 1, 0, "  !tfB bad location")]
    [InlineData(0, 1, 0, "  !tfB is not in normal space")]
    [InlineData(0, 0, 1, " !TfB is blasting a planet")]
    public void ChildJoinEligibilityUsesOriginalValidationOrder(int star, int eta, int blasting, string expected) {
        var game = Scenario(); var child = game.State.FleetsSys.Place(1, 2, star);
        child.ETA = eta; child.Blasting = blasting;
        string before = Snapshot(game);
        Assert.Equal(expected, game.State.FleetsSys.JoinPlayerFleets(1, new[] { 2 }));
        Assert.Equal(before, Snapshot(game));
    }

    [Theory]
    [InlineData(0, "  ! illegal tf @")]
    [InlineData(27, "  ! illegal tf [")]
    [InlineData(26, "  !nonexistent tf")]
    public void InvalidJoinParentRejectsWholeCommand(int index, string expected) {
        var game = Scenario(); game.State.FleetsSys.Place(1, 2, 0);
        string before = Snapshot(game);
        Assert.Equal(expected, Assert.Throws<InvalidOperationException>(() => game.State.FleetsSys.JoinPlayerFleets(index, new[] { 2 })).Message);
        Assert.Equal(before, Snapshot(game));
    }

    [Fact]
    public void ParentOrderAndBlastingPreventJoinWithoutConsumingChildren() {
        var game = Scenario(); var system = game.State.FleetsSys;
        system.Place(1, 2, 0); system.SetCourse(1, 1, 2);
        string before = Snapshot(game);
        Assert.Equal("  !tfA is not in normal space", Assert.Throws<InvalidOperationException>(() => system.JoinPlayerFleets(1, new[] { 2 })).Message);
        Assert.Equal(before, Snapshot(game));
        system.PreparePlayerCourse(1); game.State.Players[1].Tf[1].Blasting = 1; before = Snapshot(game);
        Assert.Equal("  !Tf is blasting a planet", Assert.Throws<InvalidOperationException>(() => system.JoinPlayerFleets(1, new[] { 2 })).Message);
        Assert.Equal(before, Snapshot(game));
    }

    [Fact]
    public void EmptyActiveFleetsAndDestroyedStaleSlotsFollowDestinationLifecycle() {
        var game = Scenario(); var system = game.State.FleetsSys;
        var parent = game.State.Players[1].Tf[1]; parent.Transports = parent.Scouts = parent.Cruisers = parent.BattleStars = 0;
        var empty = system.Place(1, 2, 0, scouts: 0);
        var destroyed = game.State.Players[1].Tf[3]; destroyed.Scouts = 100; // null destination means unused.
        Assert.Equal("!Nonexistant TFC", system.JoinPlayerFleets(1, new[] { 2, 3 }));
        Assert.Null(empty.Destination); Assert.Equal(0, parent.Destination); // J does not call zero_tf(parent).
        system.Place(1, 4, 0, scouts: 3);
        Assert.Null(system.JoinPlayerFleets(1, new[] { 4 }));
        Assert.Equal(3, parent.Scouts);
    }

    [Fact]
    public void SplitAndJoinDoNotResetUnrelatedFlagsEnemyFleetsOrIntelligence() {
        var game = Scenario(); var system = game.State.FleetsSys;
        var source = game.State.Players[1].Tf[1]; source.Withdrew = 1; source.EtaOriginal = 7;
        var other = system.Place(1, 3, 2); other.Blasting = 1;
        var enemy = system.Place(0, 1, 0, scouts: 4);
        var star = game.State.Stars[0];
        star.Planets.Add(new Planet(star) { Capacity = 40, PlayerSeenCapacity = 20, Owner = Ownership.None });
        game.State.Stars[1].ExploredBy[1] = false;
        Assert.Equal(2, system.SplitPlayerFleet(1, new[] { ('S', 2) }).Slot);
        Assert.Null(system.JoinPlayerFleets(1, new[] { 2 }));
        Assert.Equal(1, other.Blasting); Assert.Equal(1, source.Withdrew); Assert.Equal(7, source.EtaOriginal);
        Assert.Equal((8, 6, 3, 2), Ships(source)); Assert.Equal(4, enemy.Scouts);
        Assert.Same(enemy, game.State.Board[0, 0].TaskForces[0]);
        Assert.Equal(20, star.Planets[0].PlayerSeenCapacity); Assert.Equal(Ownership.None, star.Planets[0].Owner);
        Assert.False(game.State.Stars[1].ExploredBy[1]);
        Assert.Equal((1, 5, 3), (game.State.Players[1].Velocity, game.State.Players[1].Range, game.State.Players[1].Weapons));
        Assert.Equal(0, game.Turn);
    }

    [Fact]
    public void JoiningParentOnlyIsANoOpAndJoiningFreesAFullTablesFirstChildSlot() {
        var game = Scenario(); var system = game.State.FleetsSys;
        string before = Snapshot(game);
        Assert.Null(system.JoinPlayerFleets(1, Array.Empty<int>()));
        Assert.Equal(before, Snapshot(game));
        for (int i = 2; i <= 26; i++) system.Place(1, i, 0, scouts: 1);
        Assert.Equal(0, system.ValidatePlayerSplit(1));
        system.JoinPlayerFleets(1, new[] { 2 });
        Assert.Equal(2, system.SplitPlayerFleet(1, new[] { ('S', 1) }).Slot);
        Assert.Equal(1, game.State.Players[1].Tf[2].Scouts);
        Assert.Equal(26, game.State.Players[1].Tf.Skip(1).Count(f => f.Destination != null));
    }
}
