using Conquest.CLI;
using Conquest.Core;
using Conquest.Core.Model;
using Conquest.Core.Random;

namespace Conquest.Tests;

public sealed class ClassicStarSummaryTests {
    private sealed class Terminal(string input) : IClassicTerminal {
        private readonly Queue<char> _keys = new(input);
        public string[] LastScreen { get; private set; } = [];
        public int ReadKey() => _keys.TryDequeue(out char key) ? key : -1;
        public void Present(string[] screen) => LastScreen = screen;
    }

    private static Game NewGame() {
        var state = new GameState(new GameConfig { NumStars = 2 });
        state.Stars[0].ExploredBy[1] = true;
        return new Game(new CRandRng(), state);
    }
    private static string[] Report(Game game) {
        var terminal = new Terminal("sa\n");
        new ClassicGameLoop(game, terminal).Run();
        return terminal.LastScreen.Take(17).Select(row => row[49..]).ToArray();
    }
    private static void EqualRows(string[] actual, params string[] expected) {
        for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i].PadRight(30), actual[i]);
        Assert.All(actual.Skip(expected.Length), row => Assert.Equal(new string(' ', 30), row));
    }
    private static TaskForce Fleet(Game game, int team, int slot, int transports = 0, int scouts = 0,
        int cruisers = 0, int battles = 0, int eta = 0) {
        var f = game.State.Players[team].Tf[slot];
        f.Destination = 0; f.ETA = eta;
        f.Transports = transports; f.Scouts = scouts; f.Cruisers = cruisers; f.BattleStars = battles;
        return f;
    }

    [Fact]
    public void MultipleFleetsAndPlanetsUseOriginalSingleRowsAndColumns() {
        var g = NewGame(); var s = g.State.Stars[0];
        Fleet(g, 1, 1, transports: 35, scouts: 2, cruisers: 1);
        Fleet(g, 1, 26, battles: 3);
        Fleet(g, 1, 2, scouts: 9, eta: 2); // Travelling fleets are absent.
        Fleet(g, 1, 3); // Empty fleets are not printed.
        s.Planets.Add(new Planet(s) { Number = 1, PlayerSeenCapacity = 20, Capacity = 99 });
        s.Planets.Add(new Planet(s) { Number = 4, PlayerSeenCapacity = 35, Owner = Ownership.Player,
            Inhabitants = 7, IndustrialUnits = 12, Conquered = true, MissileBases = 2, AdvancedMissileBases = 1 });
        EqualRows(Report(g),
            "--------- Star A ---------",
            "TFa  35t 2s 1c   ",
            "TFz            3b",
            " 1:20 No colony",
            " 4:35( 7,/ 12)Con 2mb 1amb");
    }

    [Fact]
    public void ShipFieldsHaveMinimumWidthAndBlankZeroCounts() {
        var g = NewGame();
        Fleet(g, 1, 8, transports: 123, scouts: 4, cruisers: 56, battles: 7);
        EqualRows(Report(g), "--------- Star A ---------", "TFh  123t 4s56c 7b", "  no useable planets");
    }

    [Fact]
    public void RememberedCapacitiesDoNotRevealUnobservedColoniesOrEnemies() {
        var g = NewGame(); var s = g.State.Stars[0];
        Fleet(g, 0, 1, cruisers: 9);
        s.Planets.Add(new Planet(s) { Number = 2, PlayerSeenCapacity = 15 });
        s.Planets.Add(new Planet(s) { Number = 3, PlayerSeenCapacity = 40, Owner = Ownership.Enemy,
            Conquered = true, UnderAttack = true, MissileBases = 5, AdvancedMissileBases = 6 });
        EqualRows(Report(g), "--------- Star A ---------", " 2:15", " 3:40");
    }

    [Fact]
    public void PlayerColonyProvidesSightAndEnemyFleetIsFirstStationarySlot() {
        var g = NewGame(); var s = g.State.Stars[0];
        Fleet(g, 0, 1, battles: 8, eta: 1);
        Fleet(g, 0, 2, scouts: 3, cruisers: 4);
        Fleet(g, 0, 3, battles: 9);
        s.Planets.Add(new Planet(s) { Number = 1, PlayerSeenCapacity = 20, Owner = Ownership.Player,
            Inhabitants = 10, IndustrialUnits = 100 });
        s.Planets.Add(new Planet(s) { Number = 2, PlayerSeenCapacity = 25 });
        EqualRows(Report(g), "--------- Star A ---------", " EN:     3s 4c   ",
            " 1:20(10,/100)       ", " 2:25 No colony");
    }

    [Theory]
    [InlineData(false, false, "*EN*   ")]
    [InlineData(true, false, "*EN*Conquered")]
    [InlineData(false, true, "*EN*    2mb 3amb")]
    [InlineData(true, true, "*EN*Conquered 2mb 3amb")]
    public void EnemyColonyDefensesRequireAttack(bool conquered, bool attack, string expected) {
        var g = NewGame(); var s = g.State.Stars[0];
        Fleet(g, 1, 1, scouts: 1);
        s.Planets.Add(new Planet(s) { Number = 1, PlayerSeenCapacity = 25, Owner = Ownership.Enemy,
            Conquered = conquered, UnderAttack = attack, MissileBases = 2, AdvancedMissileBases = 3 });
        EqualRows(Report(g), "--------- Star A ---------", "TFa      1s      ", " 1:25" + expected);
    }

    [Fact]
    public void DecimatedCapacityOverridesAllColonyDetailsWithoutPresence() {
        var g = NewGame(); var s = g.State.Stars[0];
        s.Planets.Add(new Planet(s) { Number = 3, PlayerSeenCapacity = 0, Capacity = 50,
            Owner = Ownership.Enemy, Conquered = true, UnderAttack = true, MissileBases = 5 });
        EqualRows(Report(g), "--------- Star A ---------", " 3: 0 Decimated");
    }

    [Fact]
    public void PlayerColonyReservesBlankConquestAndMissileFieldsBeforeAdvancedBases() {
        var g = NewGame(); var s = g.State.Stars[0];
        s.Planets.Add(new Planet(s) { Number = 4, PlayerSeenCapacity = 35, Owner = Ownership.Player,
            Inhabitants = 12, IndustrialUnits = 123, AdvancedMissileBases = 2 });
        EqualRows(Report(g), "--------- Star A ---------", " 4:35(12,/123)        2amb");
    }

    [Fact]
    public void EmptyVisitedStarUsesOriginalNoPlanetsWording() {
        EqualRows(Report(NewGame()), "--------- Star A ---------", "  no useable planets");
    }

    [Fact]
    public void UnvisitedStarSuppressesWholeReportEvenWithPlayerPresence() {
        var g = NewGame(); g.State.Stars[0].ExploredBy[1] = false;
        Fleet(g, 1, 1, scouts: 1);
        EqualRows(Report(g));
    }
}
