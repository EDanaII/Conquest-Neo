using Conquest.CLI;
using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.Tests;

public sealed class ClassicCombatTests {
    private sealed class Rng(params int[] draws) : IRng {
        private readonly Queue<int> _draws = new(draws);
        public int Calls { get; private set; }
        public int Next(int minInclusive, int maxExclusive) {
            Assert.Equal((1, 101), (minInclusive, maxExclusive)); Calls++; return _draws.Dequeue();
        }
        public void Reseed(int seed) => throw new NotSupportedException();
    }
    private sealed class Terminal(string input) : IClassicTerminal {
        private readonly Queue<char> _keys = new(input);
        public List<string[]> Screens { get; } = [];
        public List<(int, int)> Cursors { get; } = [];
        public int ReadKey() => _keys.TryDequeue(out char ch) ? ch : -1;
        public void Present(string[] screen) => Screens.Add(screen);
        public void MoveCursor(int column, int row) => Cursors.Add((column, row));
    }
    private static Game Scenario(IRng rng, Ownership owner = Ownership.None, bool multiple = false) {
        var state = new GameState(new GameConfig { NumStars = 2, SafeExplore = true }); state.Stars[1].X = 4;
        foreach (var s in state.Stars) { s.ExploredBy[1] = true; state.Board[s.X, s.Y].StarRef = s; }
        state.Stars[0].Planets.Add(new Planet(state.Stars[0]) { Number = 3, Owner = owner, Capacity = 20,
            PlayerSeenCapacity = 20, Inhabitants = owner == Ownership.None ? 0 : 10, IndustrialUnits = owner == Ownership.None ? 0 : 10 });
        if (multiple) state.Stars[0].Planets.Add(new Planet(state.Stars[0]) { Number = 6, Owner = owner, Capacity = 30, PlayerSeenCapacity = 30 });
        var f = state.FleetsSys.Place(1, 1, 0, scouts: 2); f.Transports = 8; f.Cruisers = 1;
        state.Players[0].Weapons = state.Players[1].Weapons = 3; state.Players[1].Velocity = 1; state.Players[1].Range = 5;
        return new Game(rng, state);
    }
    private static Terminal Run(Game game, string input) {
        var terminal = new Terminal(input); Assert.Equal(0, new ClassicGameLoop(game, terminal).Run()); return terminal;
    }

    [Theory]
    [InlineData("ba\n", 20, 0)]
    [InlineData("ba0\n", 20, 0)]
    [InlineData("ba1\n", 19, 1)]
    [InlineData("ba99\n", 15, 1)]
    [InlineData("bafoo\n", 19, 1)]
    [InlineData("ba-3\n", 19, 1)]
    [InlineData("ba2c99\n", 18, 1)]
    public void BlastUsesOriginalQuantityParsingAndBlankCancelsRatherThanBlastingAll(string input, int capacity, int flag) {
        var rng = new Rng(); var game = Scenario(rng); var terminal = Run(game, input);
        Assert.Equal(capacity, game.State.Stars[0].Planets[0].Capacity); Assert.Equal(flag, game.State.Players[1].Tf[1].Blasting);
        Assert.Equal(0, game.Turn); Assert.Equal(0, rng.Calls);
        Assert.Contains(terminal.Screens, s => s[17].StartsWith(">>>>>>>> Blast Planet <<<<<<<<"));
        Assert.Contains(terminal.Screens, s => s[18].StartsWith(" Firing TF:"));
        Assert.Contains(terminal.Screens, s => s[19].StartsWith("Target colony  3") && s[20].StartsWith("Units (max   5) :"));
        Assert.Contains((17, 20), terminal.Cursors);
        if (flag != 0) {
            Assert.Contains(terminal.Screens, s => s[2][49..].StartsWith($" 3:{capacity} No colony"));
            Assert.Contains(terminal.Screens, s => s[21].StartsWith("Blasting "));
        }
    }

    [Fact]
    public void MultiplePlanetsAndPostBlastLandingFleetAndColonyReportsRefreshImmediately() {
        var game = Scenario(new Rng(), multiple: true); var terminal = Run(game, "ba62\nla63\nt\nc");
        Assert.Equal(20, game.State.Stars[0].Planets[0].Capacity);
        var p = game.State.Stars[0].Planets[1]; Assert.Equal((28, 3, 3), (p.Capacity, p.Inhabitants, p.IndustrialUnits));
        Assert.Contains(terminal.Screens, s => s[19].StartsWith("Target colony :"));
        Assert.Contains(terminal.Screens, s => s[0][49..].StartsWith("TFa:A( 1, 1) 5t 2s 1c"));
        Assert.Contains(terminal.Screens, s => s[1][49..].StartsWith("A6:28( 3,/  3)"));
        Assert.Equal("@Aa", terminal.Screens[^1][14].Substring(3, 3));
    }

    [Theory]
    [InlineData("b1", Ownership.None, " !Illegal tf")]
    [InlineData("bz", Ownership.None, " !Nonexistent tf")]
    [InlineData("ba", Ownership.Enemy, " !Conquer it first!")]
    [InlineData("ba", Ownership.Player, " !That is a human colony!!")]
    [InlineData("ba1\nba", Ownership.None, " !Tf is already blasting")]
    public void BlastErrorsReturnWithoutConsumingSubsequentMovementCommands(string input, Ownership owner, string error) {
        var game = Scenario(new Rng(), owner); var terminal = Run(game, input);
        Assert.Contains(terminal.Screens, s => s[24].TrimEnd() == error);
    }

    [Theory]
    [InlineData("b")]
    [InlineData("ba")]
    [InlineData("ba1")]
    public void BlastEofAtAnyPromptDoesNotApplyIncompleteOrder(string input) {
        var game = Scenario(new Rng(), multiple: true); Run(game, input);
        Assert.All(game.State.Stars[0].Planets, p => Assert.True(p.Capacity is 20 or 30));
        Assert.Equal(0, game.State.Players[1].Tf[1].Blasting);
    }

    [Fact]
    public void GEntersPlanetAttackWithoutExtraSelectorForSoleTargetsAndUpdatesCaptureBeforeClock() {
        var rng = new Rng(); var game = Scenario(rng, Ownership.Enemy); var terminal = Run(game, "ggx");
        Assert.Equal(1, game.Turn); Assert.Null(game.State.Attack); Assert.False(game.State.ClassicTurnInProgress);
        Assert.Equal(Ownership.Player, game.State.Stars[0].Planets[0].Owner); Assert.Equal(0, rng.Calls);
        Assert.Contains(terminal.Screens, s => s[17].StartsWith(">>>>>> Attack at Star A <<<<<<") && s[18].StartsWith("P?"));
        Assert.Contains(terminal.Screens, s => s[14].Substring(3, 3) == "!Aa");
        Assert.Contains(terminal.Screens, s => s[23].StartsWith("                Planet 3 falls!"));
        Assert.Contains(terminal.Screens, s => s[24].StartsWith("Planet attack concluded"));
        Assert.Contains(terminal.Screens, s => s[2][49..].StartsWith(" 3:20(10,/ 10)Con"));
        Assert.Equal("@Aa", terminal.Screens[^1][14].Substring(3, 3));
    }

    [Fact]
    public void DefendedAttackShowsOriginalForcesLossesAndRevealsBasesOnlyAfterFirstSalvo() {
        var rng = new Rng(1, 100); var game = Scenario(rng, Ownership.Enemy);
        game.State.Stars[0].Planets[0].MissileBases = 1; var terminal = Run(game, "ggx");
        Assert.Equal(2, rng.Calls); Assert.Equal(1, game.Turn);
        Assert.Contains(terminal.Screens, s => s[19].StartsWith("TFa:   21(weap  3)sur:   80"));
        Assert.Contains(terminal.Screens, s => s[20].StartsWith(" A3:  21 (weap  3)sur:   80"));
        Assert.Contains(terminal.Screens, s => s[21].StartsWith("Attacker losses:(none)") && s[22].StartsWith(" Planet losses :  1mb"));
        Assert.Contains(terminal.Screens, s => s[2][49..].TrimEnd() == " 3:20*EN*");
    }

    [Fact]
    public void SurvivingEnemyBasesAreRevealedDuringAttackAndHiddenAgainOnBreakOff() {
        var rng = new Rng(1, 1); var game = Scenario(rng, Ownership.Enemy);
        game.State.Stars[0].Planets[0].MissileBases = 1; var terminal = Run(game, "gg");
        Assert.Equal(0, game.Turn); Assert.Equal(2, rng.Calls); Assert.True(game.State.Stars[0].Planets[0].UnderAttack);
        Assert.Contains(terminal.Screens, s => s[2][49..].TrimEnd() == " 3:20*EN*    1mb");
        terminal = Run(game, "bx"); Assert.Equal(1, game.Turn); Assert.Equal(2, rng.Calls);
        Assert.False(game.State.Stars[0].Planets[0].UnderAttack);
        Assert.Contains(terminal.Screens, s => s[2][49..].TrimEnd() == " 3:20*EN*");
    }

    [Fact]
    public void DestructionRemovesFleetLetterAndRestoresStarAndEnemyReportVisibility() {
        var rng = new Rng(100, 1); var game = Scenario(rng, Ownership.Enemy);
        var fleet = game.State.Players[1].Tf[1]; fleet.Transports = fleet.Scouts = 0;
        game.State.Stars[0].Planets[0].MissileBases = 1; var terminal = Run(game, "ggx");
        Assert.Null(fleet.Destination); Assert.Equal(1, game.Turn); Assert.Equal(2, rng.Calls);
        Assert.Equal(" A ", terminal.Screens[^1][14].Substring(3, 3));
        Assert.Equal(" 3:20", terminal.Screens[^1][1][49..].TrimEnd());
        Assert.Contains(terminal.Screens, s => s[21].StartsWith("Attacker losses:  1c"));
    }

    [Fact]
    public void BreakOffIsBAndWIsIllegalHelpConsumesPauseAndEofCanResumePendingAttack() {
        var rng = new Rng(); var game = Scenario(rng, Ownership.Enemy); var terminal = Run(game, "gwhx");
        Assert.NotNull(game.State.Attack); Assert.Equal(0, game.Turn);
        Assert.Contains(terminal.Screens, s => s[24].TrimEnd() == " !Illegal command");
        Assert.Contains(terminal.Screens, s => s[0][49..].Contains("Break off Attack"));
        terminal = Run(game, "bx"); Assert.Equal(1, game.Turn); Assert.Null(game.State.Attack); Assert.Equal(0, rng.Calls);
        Assert.Equal(Ownership.Enemy, game.State.Stars[0].Planets[0].Owner);
        Assert.Contains(terminal.Screens, s => s[19].StartsWith("Break off attack"));
    }

    [Fact]
    public void MultiplePlanetsAndFleetsUseSingleKeysWithOriginalValidationAndNoPrematureRolls() {
        var rng = new Rng(); var game = Scenario(rng, Ownership.Enemy, multiple: true);
        game.State.FleetsSys.Place(1, 2, 0, scouts: 1);
        var terminal = Run(game, "gg9g3bg3abx");
        Assert.Contains(terminal.Screens, s => s[18].StartsWith("P?Attack planet :"));
        Assert.Contains(terminal.Screens, s => s[18].StartsWith(" attacking tf :"));
        Assert.Contains(terminal.Screens, s => s[24].TrimEnd() == "! That is not a useable planet");
        Assert.Contains(terminal.Screens, s => s[24].TrimEnd() == " !Tf has no warships");
        Assert.Equal(Ownership.Player, game.State.Stars[0].Planets[0].Owner);
        Assert.Equal(Ownership.Enemy, game.State.Stars[0].Planets[1].Owner);
        Assert.Equal(1, game.Turn); Assert.Equal(0, rng.Calls);
    }
}
