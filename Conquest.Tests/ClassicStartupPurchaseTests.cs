using Conquest.CLI;
using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.Tests;

public sealed class ClassicStartupPurchaseTests {
    private sealed class Rng(int seed) : IRng {
        private readonly CRandRng _inner = new(seed);
        public int Calls { get; private set; }
        public int Next(int minInclusive, int maxExclusive) { Calls++; return _inner.Next(minInclusive, maxExclusive); }
        public void Reseed(int seed) => throw new NotSupportedException();
    }
    private sealed class Terminal(string input) : IClassicTerminal {
        private readonly Queue<char> _keys = new(input);
        public List<string[]> Screens { get; } = [];
        public List<(int, int)> Cursors { get; } = [];
        public int Clears { get; private set; }
        public int ReadKey() => _keys.TryDequeue(out char key) ? key : -1;
        public void Present(string[] screen) => Screens.Add(screen);
        public void MoveCursor(int column, int row) => Cursors.Add((column, row));
        public void Clear() => Clears++;
    }
    private static (Game Game, Rng Rng) Prepare(int money = 30, bool sharedStart = false) {
        var rng = new Rng(1);
        var game = new Game(rng, new GameState(new GameConfig { StartingMoney = money }));
        game.GenerateStars(); game.InitializeClassicPlayers(); game.InitializeClassicEnemy(); game.BeginClassicPlayerStartup();
        int start = (game.State.Players[0].Tf[1].Destination!.Value + (sharedStart ? 0 : 1)) % game.State.Stars.Count;
        game.SelectClassicPlayerStartingStar(start);
        return (game, rng);
    }

    [Fact]
    public void PurchaseScreenMatchesOriginalPaneRowsAndKeepsMapVisibleWithoutScrollingIo() {
        var (game, rng) = Prepare(); int calls = rng.Calls;
        var terminal = new Terminal("");
        Assert.False(new ClassicStartupPurchases(game, terminal).Run());
        var screen = terminal.Screens[0]; var f = game.State.Players[1].Tf[1];
        var star = game.State.Stars[game.State.PlayerStartingStar!.Value];
        Assert.StartsWith("******* Initialization *******", screen[17]);
        Assert.Equal(" 30? ", screen[18][..5]);
        Assert.StartsWith($"TFa:{star.Name}({f.X + 1,2},{f.Y + 1,2})35t", screen[19]);
        Assert.StartsWith("you have 35 transports", screen[20]);
        Assert.StartsWith(" && 30 units to spend", screen[21]);
        Assert.StartsWith("on ships or research.", screen[22]);
        Assert.StartsWith("------- Events -------", screen[17][31..]);
        Assert.StartsWith("Turn:                1", screen[18][31..]);
        Assert.StartsWith("Prod yr:             1", screen[19][31..]);
        Assert.StartsWith("------- Research ------", screen[17][54..]);
        Assert.StartsWith("V: 1 res:   0 need:  40", screen[18][54..]);
        Assert.StartsWith("R: 5 res:   0 need:  20", screen[19][54..]);
        Assert.StartsWith("W: 3 res:   0 need:  50", screen[20][54..]);
        Assert.Contains((5, 18), terminal.Cursors);
        Assert.Equal($"?{star.Name} ", screen[14 - star.Y].Substring(3 + 3 * star.X, 3));
        var expected = new ClassicScreen().Render(game);
        for (int row = 0; row < 17; row++) for (int col = 0; col < 49; col++) {
            if (row == 14 - star.Y && col == 5 + 3 * star.X) continue;
            Assert.Equal(expected[row][col], screen[row][col]);
        }
        Assert.All(screen, row => Assert.Equal(79, row.Length));
        Assert.Equal(calls, rng.Calls); Assert.Equal(0, game.Turn);
        Assert.Equal(PlayerStartupStage.Spending, game.State.PlayerStartupStage);
    }

    [Theory]
    [InlineData("c\n2s\n2r\n")]
    [InlineData("C2S2R\n")]
    [InlineData("C2S2R\r\n")]
    [InlineData("x\bc2s2r\n")]
    public void PurchasesRefreshFleetBalanceAndResearchAndPreserveGalaxyOnGameplayEntry(string input) {
        var (game, rng) = Prepare(); int calls = rng.Calls;
        var stars = game.State.Stars.ToArray();
        var coords = stars.Select(s => (s.X, s.Y)).ToArray();
        var terminal = new Terminal(input);
        Assert.True(new ClassicStartupPurchases(game, terminal).Run());
        var fleet = game.State.Players[1].Tf[1];
        Assert.Equal((35, 2, 1, 0), (fleet.Transports, fleet.Scouts, fleet.Cruisers, fleet.BattleStars));
        Assert.Equal(2, game.State.Players[1].RangeResearchProgress); Assert.Equal(0, game.State.PlayerStartupBalance);
        Assert.Contains(terminal.Screens, s => s[18].StartsWith(" 14? "));
        Assert.Contains(terminal.Screens, s => s[18].StartsWith("  2? "));
        Assert.Contains(terminal.Screens, s => s[19][54..].StartsWith("R: 5 res:   2 need:  20"));
        var map = terminal.Screens[0].Take(17).Select(s => s[..49]).ToArray();
        Assert.All(terminal.Screens, s => Assert.Equal(map, s.Take(17).Select(r => r[..49]).ToArray()));
        Assert.Equal(0, terminal.Clears);
        game.FinalizeClassicPlayerStartup();
        Assert.Equal(0, new ClassicGameLoop(game, terminal).Run());
        var final = terminal.Screens[^1]; var star = stars[game.State.PlayerStartingStar!.Value];
        Assert.Equal($" {star.Name}a", final[14 - star.Y].Substring(3 + star.X * 3, 3));
        Assert.StartsWith("---------- Movement ----------", final[17]);
        Assert.DoesNotContain(terminal.Screens, s => s[24].Contains("illegal command"));
        Assert.Equal(coords, game.State.Stars.Select(s => (s.X, s.Y)).ToArray());
        for (int i = 0; i < stars.Length; i++) Assert.Same(stars[i], game.State.Stars[i]);
        Assert.Equal(calls, rng.Calls); Assert.Equal(0, game.Turn);
    }

    [Theory]
    [InlineData("B\n", "can't afford")]
    [InlineData("X\n", "Illegal field X")]
    [InlineData("2147483647B\n", "Invalid purchase quantity")]
    [InlineData("9999999999999999999999S\n", "Invalid purchase quantity")]
    public void InvalidOrUnaffordablePurchasesKeepBudgetAndMapAndShowStatus(string input, string error) {
        var (game, _) = Prepare(); var terminal = new Terminal(input);
        Assert.False(new ClassicStartupPurchases(game, terminal).Run());
        Assert.Equal(30, game.State.PlayerStartupBalance);
        Assert.Equal(0, game.State.Players[1].Tf[1].BattleStars);
        Assert.Contains(terminal.Screens, s => s[24].Contains(error));
        Assert.All(terminal.Screens, s => Assert.StartsWith("******* Initialization *******", s[17]));
    }

    [Fact]
    public void HelpAndSeparateMapResearchSubpromptsDoNotSpendOrAdvanceAndCanResumePurchasing() {
        var (game, rng) = Prepare(); int calls = rng.Calls;
        var terminal = new Terminal("h\n>\nm>\nrvrw\n>\nxC2S2R\n");
        Assert.True(new ClassicStartupPurchases(game, terminal).Run());
        Assert.Contains(terminal.Screens, s => s[0][49..].StartsWith(" B - Bld Battlestar(s)    75"));
        Assert.Contains(terminal.Screens, s => s[18].StartsWith(">?"));
        Assert.Contains(terminal.Screens, s => s[18].StartsWith(" Research field(s):"));
        Assert.Contains(terminal.Screens, s => s[24].Contains("Only M,R during initialize"));
        Assert.Equal(1, terminal.Clears); Assert.Equal(0, game.Turn); Assert.Equal(calls, rng.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("C")]
    [InlineData(">\n")]
    [InlineData(">\nR")]
    public void EofAtEachPromptDoesNotFinalizeOrSpendAnUnsubmittedPurchase(string input) {
        var (game, _) = Prepare(); var terminal = new Terminal(input);
        Assert.False(new ClassicStartupPurchases(game, terminal).Run());
        Assert.Equal(30, game.State.PlayerStartupBalance); Assert.Equal(PlayerStartupStage.Spending, game.State.PlayerStartupStage);
    }

    [Fact]
    public void TwentyFiveCharactersReturnImmediatelyWithoutEatingNextPurchaseOrEnter() {
        var (game, _) = Prepare(); var terminal = new Terminal(new string('?', 25) + "C2S2R\n");
        Assert.True(new ClassicStartupPurchases(game, terminal).Run());
        Assert.Contains(terminal.Cursors, c => c == (30, 18));
        Assert.Equal(1, game.State.Players[1].Tf[1].Cruisers);
    }

    [Fact]
    public void ZeroMoneyShowsPurchaseLayoutAndRequiresNoKeyboardInput() {
        var (game, _) = Prepare(money: 0); var terminal = new Terminal("");
        Assert.True(new ClassicStartupPurchases(game, terminal).Run());
        Assert.Single(terminal.Screens); Assert.StartsWith("  0? ", terminal.Screens[0][18]);
    }

    [Theory]
    [InlineData("B\n", 70, 1, 1, 3, 5)]
    [InlineData("40V\n", 40, 0, 2, 3, 5)]
    [InlineData("50W\n", 50, 0, 1, 4, 5)]
    [InlineData("20R\n", 20, 0, 1, 3, 6)]
    public void ShipAndResearchPurchasesUseOriginalCoreCostsAndBreakthroughs(string input, int money,
        int battles, int velocity, int weapons, int range) {
        var (game, rng) = Prepare(money); int calls = rng.Calls;
        Assert.True(new ClassicStartupPurchases(game, new Terminal(input)).Run());
        var player = game.State.Players[1];
        Assert.Equal((battles, velocity, weapons, range), (player.Tf[1].BattleStars, player.Velocity, player.Weapons, player.Range));
        Assert.Equal(0, game.State.PlayerStartupBalance); Assert.Equal(calls, rng.Calls);
    }

    [Fact]
    public void OriginalBlankTokenStopsLineAndQuitIsNotAPurchasePhaseCommand() {
        var (game, _) = Prepare(); var terminal = new Terminal("Q\n2 S\n30R\n");
        Assert.True(new ClassicStartupPurchases(game, terminal).Run());
        Assert.Contains(terminal.Screens, s => s[24].Contains("Illegal field Q"));
        Assert.Equal(0, game.State.Players[1].Tf[1].Scouts);
        Assert.Equal(6, game.State.Players[1].Range); Assert.Equal(10, game.State.Players[1].RangeResearchProgress);
    }

    [Fact]
    public void SharedStartFinishesPurchasingThenEntersOriginalCombatWithoutAnotherGalaxyOrTurn() {
        var (game, rng) = Prepare(sharedStart: true); int calls = rng.Calls;
        var stars = game.State.Stars.ToArray(); var terminal = new Terminal("C2S2R\n");
        Assert.True(new ClassicStartupPurchases(game, terminal).Run());
        Assert.False(game.State.PendingSharedStartEncounter);
        game.FinalizeClassicPlayerStartup(); Assert.True(game.State.PendingSharedStartEncounter);
        Assert.Equal(0, new ClassicGameLoop(game, terminal).Run());
        Assert.NotNull(game.State.Battle);
        Assert.Contains(terminal.Screens, s => s[17].StartsWith(">>>>>> Taskforce Battle <<<<<<"));
        Assert.Equal(calls, rng.Calls); Assert.Equal(0, game.Turn);
        for (int i = 0; i < stars.Length; i++) Assert.Same(stars[i], game.State.Stars[i]);
    }
}
