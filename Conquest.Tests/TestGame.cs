using Conquest.CLI;
using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.Tests.TestHelpers;

public sealed class TestGame {
    public DummyIO IO { get; }
    public CRandRng Rng { get; }
    public GameState State { get; }
    public SpyRenderer Renderer { get; }
    public Game Game { get; }
    public GameShell Shell { get; }

    private TestGame(
        DummyIO io,
        CRandRng rng,
        GameState state,
        SpyRenderer renderer,
        Game game,
        GameShell shell
    ) {
        IO = io;
        Rng = rng;
        State = state;
        Renderer = renderer;
        Game = game;
        Shell = shell;
    }

    public static TestGame New(GameConfig? cfg = default, int? seed = 1, IEnumerable<string>? scriptedInput = null) {
        var config = cfg ?? GameConfig.Default;
        var io = new DummyIO(scriptedInput ?? Array.Empty<string>( ));
        var rng = new CRandRng( );
        if (seed is int s) rng.Seed(s);

        var state = new GameState(config);
        var renderer = new SpyRenderer( );
        var game = new Game(rng, state);
        var shell = new GameShell(game, io, rng, renderer);

        return new TestGame(io, rng, state, renderer, game, shell);
    }

    public int RunCommands(params string[] commands) {
        if (commands is { Length: > 0 })
            IO.Enqueue(commands);

        IO.Enqueue("quit");

        return Shell.Run( );
    }

    public static TestGame WithProgress(
        GameConfig? cfg = default,
        int? seed = 1,
        int ticks = 1,
        IEnumerable<string>? scriptedInput = null) {
        var g = New(cfg, seed, scriptedInput);

        g.Game.GenerateStars( );

        if (scriptedInput is not null) {
            g.IO.Enqueue("quit");
            g.Shell.Run( );
        }

        if (ticks > 0)
            g.Game.Tick(ticks);

        return g;
    }
}
