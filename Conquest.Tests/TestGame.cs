using Conquest.Core;
using Conquest.Core.Random;

namespace Conquest.Tests.TestHelpers;

/// <summary>
/// Convenience factory for constructing a testable Game with deterministic RNG,
/// scripted I/O, and a spy renderer. Favor state assertions over output text.
/// </summary>
public sealed class TestGame {
    public DummyIO IO { get; }
    public CRandRng Rng { get; }
    public GameState State { get; }
    public SpyRenderer Renderer { get; }
    public Game Game { get; }

    private TestGame(DummyIO io, CRandRng rng, GameState state, SpyRenderer renderer, Game game) {
        IO = io; Rng = rng; State = state; Renderer = renderer; Game = game;
    }

    /// <summary>Create a fresh game with optional config and seed.</summary>
    public static TestGame New(GameConfig? cfg = default, int? seed = 1, IEnumerable<string>? scriptedInput = null) {
        var config = cfg ?? GameConfig.Default; // adjust if your config provides a Default
        var io = new DummyIO(scriptedInput ?? Array.Empty<string>( ));
        var rng = new CRandRng( );
        if (seed is int s) rng.Reseed(s);

        var state = new GameState(config);
        var renderer = new SpyRenderer( );
        var game = new Game(io, rng, state, renderer);
        return new TestGame(io, rng, state, renderer, game);
    }

    /// <summary>
    /// Enqueue commands and run the main loop to completion.
    /// If you don’t enqueue "quit", DummyIO will auto-return "quit" to prevent hangs.
    /// </summary>
    public int RunCommands(params string[] commands) {
        if (commands is { Length: > 0 })
            IO.Enqueue(commands);

        // Ensure quit so we exit deterministically
        IO.Enqueue("quit");

        return Game.Run( );
    }

    public static TestGame WithProgress( ) {
        // Start from a deterministic seed so results are reproducible
        var G = New(seed: 1);

        // Use the real game loop to mutate state (no string asserts needed)
        G.RunCommands("genstars", "fog on");

        // If you have a reserved [0] element, skip it; otherwise remove Skip(1)
        var first = G.State.Stars.Skip(1).FirstOrDefault( );
        if (first is not null) {
            // If your Star uses a different structure, tweak this line accordingly
            first.ExploredBy[1] = true;
        }

        return G;
    }

    /// <summary>
    /// Convenience to run a single command (the loop will exit via injected "quit").
    /// </summary>
    public int RunCommand(string command) => RunCommands(command);
}
