using Conquest.CLI;
using Conquest.Core;
using Conquest.Core.Model;
using Conquest.Core.Random;

namespace Conquest.Tests;

public sealed class ClassicTerminalTests {
    private static Game Game() {
        var state = new GameState(new GameConfig { Width = 15, Height = 15, NumStars = 2, SafeExplore = true });
        state.Stars[0].X = 0; state.Stars[0].Y = 0;
        state.Stars[1].X = 2; state.Stars[1].Y = 0;
        state.Board[0, 0].StarRef = state.Stars[0];
        state.Board[2, 0].StarRef = state.Stars[1];
        state.FleetsSys.Place(1, 1, 0);
        return new Game(new CRandRng(), state);
    }
    private sealed class Terminal(string input) : IClassicTerminal {
        private readonly Queue<char> _keys = new(input);
        public List<string[]> Screens { get; } = [];
        public int ReadKey() => _keys.TryDequeue(out char key) ? key : -1;
        public void Present(string[] screen) => Screens.Add(screen);
    }
    [Fact]
    public void ScreenMatchesSourceCoordinatesAndThreeCharacterSectors() {
        var g = Game(); var renderer = new ClassicScreen();
        var screen = renderer.Render(g);
        Assert.Equal(25, screen.Length);
        Assert.All(screen, row => Assert.Equal(79, row.Length));
        Assert.Equal(" 1|?Aa . ?B ", screen[14][..12]);
        Assert.StartsWith("15|", screen[0]);
        Assert.Equal("---------- Movement ----------", screen[17][..30]);
        Assert.Equal("------- Events -------", screen[17].Substring(31, 22));
        Assert.Equal("------- Research ------", screen[17].Substring(54, 23));
        Assert.Equal('1', screen[16][4]);
        Assert.Equal("?Aa", renderer.Cell(g.State, 0, 0));
    }
    [Fact]
    public void SharedFleetsAndColonyPreserveStarAndRestoreAfterDeparture() {
        var g = Game(); var s = g.State.Stars[0];
        s.ExploredBy[1] = true;
        s.Planets.Add(new Planet(s) { Owner = Ownership.Player });
        g.State.FleetsSys.Place(1, 2, 0);
        g.State.FleetsSys.Place(0, 1, 0);
        var renderer = new ClassicScreen();
        Assert.Equal("@A*", renderer.Cell(g.State, 0, 0));
        g.State.FleetsSys.SetCourse(1, 1, 1);
        g.State.FleetsSys.SetCourse(1, 2, 1);
        g.Tick();
        Assert.Equal("@A ", renderer.Cell(g.State, 0, 0));
        Assert.Equal(" .*", renderer.Cell(g.State, 1, 0));
        g.Tick();
        Assert.Equal(" B*", renderer.Cell(g.State, 2, 0));
        Assert.Equal(" . ", renderer.Cell(g.State, 1, 0));
    }
    [Fact]
    public void SingleKeysOrderMoveRefreshAndConfirmQuit() {
        var g = Game(); var terminal = new Terminal("dabg gqnyqy");
        new ClassicGameLoop(g, terminal).Run();
        Assert.Equal(3, g.Turn);
        Assert.Equal(1, g.State.Players[1].Tf[1].Destination);
        Assert.Contains(terminal.Screens, s => s[18].StartsWith(" Destination tf:"));
        Assert.Contains(terminal.Screens, s => s[19].StartsWith("        to star:"));
        Assert.Contains(terminal.Screens, s => s[14].Substring(3, 3) == "?A " && s[14].Substring(6, 3) == " .a");
        Assert.Contains(terminal.Screens, s => s[14].Substring(9, 3) == " Ba");
        Assert.Equal("?Aa", terminal.Screens[0][14].Substring(3, 3));
    }
    [Fact]
    public void SummariesEditInputAndDoNotRevealUnknownStarOrEnemy() {
        var g = Game();
        g.State.FogEnabled = false;
        g.State.FleetsSys.Place(0, 1, 1);
        var terminal = new Terminal("sz\ba\nt\nqY");
        new ClassicGameLoop(g, terminal).Run();
        Assert.Contains(terminal.Screens, s => s[18].StartsWith(" Star summary:") && s[19].StartsWith(":A"));
        Assert.DoesNotContain(terminal.Screens, s => s.Any(row => row.Contains("--------- Star")));
        Assert.Contains(terminal.Screens, s => s[0].Contains("TFa:"));
        Assert.All(terminal.Screens.Where(s => !s[0].StartsWith("Quit game")), s => Assert.Equal("?B ", s[14].Substring(9, 3)));
    }
    [Fact]
    public void InvalidOrdersAndUnsupportedCommandsDoNotAdvanceTime() {
        var g = Game(); var terminal = new Terminal("dzb1daznqY");
        new ClassicGameLoop(g, terminal).Run();
        Assert.Equal(0, g.Turn);
        Assert.Equal(0, g.State.Players[1].Tf[1].Destination);
        Assert.Contains(terminal.Screens, s => s[24].Contains("!Illegal tf"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("d")]
    [InlineData("da")]
    [InlineData("s")]
    [InlineData("t")]
    [InlineData("r")]
    [InlineData("q")]
    public void EndOfInputExitsAtEveryPrompt(string input) {
        var g = Game(); var terminal = new Terminal(input);
        Assert.Equal(0, new ClassicGameLoop(g, terminal).Run());
        Assert.Equal(0, g.Turn);
        Assert.NotEmpty(terminal.Screens);
    }

    [Fact]
    public void ArrivalExplorationRefreshesStatusAndEnablesStarReport() {
        var g = Game(); var terminal = new Terminal("dabgg.sb\nqy");
        new ClassicGameLoop(g, terminal).Run();
        Assert.True(g.State.Stars[1].ExploredBy[1]);
        Assert.Contains(terminal.Screens, s => s[0][49..].StartsWith("--------- Star B"));
        Assert.Equal(" Ba", terminal.Screens.Last(s => !s[0].StartsWith("Quit game"))[14].Substring(9, 3));
    }

    [Fact]
    public void AttackStatusHasPrecedenceAndEnemyAloneRemainsHidden() {
        var g = Game(); var star = g.State.Stars[1];
        star.ExploredBy[1] = true;
        star.Planets.Add(new Planet(star) { Owner = Ownership.Player, UnderAttack = true });
        g.State.FleetsSys.Place(0, 1, 1);
        var renderer = new ClassicScreen();
        Assert.Equal("!B ", renderer.Cell(g.State, 2, 0));
        star.Planets[0].UnderAttack = false;
        Assert.Equal("@B ", renderer.Cell(g.State, 2, 0));
        star.Planets[0].Owner = Ownership.None;
        Assert.Equal(" B ", renderer.Cell(g.State, 2, 0));
    }
}
