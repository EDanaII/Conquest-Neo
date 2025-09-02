namespace Conquest.Core;

public class Game {
    private readonly IGameIO _io;
    private readonly IRng _rng;
    private readonly GameState _state;
    private readonly IMapRenderer _mapRenderer;

    public Game(IGameIO io, IRng rng, GameState state, IMapRenderer mapRenderer) {
        _io = io;
        _rng = rng;
        _state = state;
        _mapRenderer = mapRenderer;
    }

    public int Run( ) {
        _io.WriteLine("CONQUEST (C# port)");
        _io.WriteLine($"Board: {_state.Config.Width}x{_state.Config.Height}  Stars: {_state.Config.NumStars}  Turns: {_state.Config.MaxTurns}");
        _io.WriteLine("Type 'help' for commands, 'quit' to exit.");

        while (true) {
            _io.Write("> ");
            var line = (_io.ReadLine( ) ?? "").Trim( );
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            var cmd = parts[0].ToLowerInvariant( );
            if (cmd is "quit" or "q") break;

            switch (cmd) {
                case "help":
                    _io.WriteLine("Commands: help, roll, config, seed <n>, genstars, map, explore");
                    break;

                case "config":
                    var c = _state.Config;
                    _io.WriteLine($"Victory={c.VictoryMultiplier}, Overwhelm={c.OverwhelmFactor}, Capacity={c.Capacity}");
                    break;

                case "roll":
                    _io.WriteLine($"RNG: {_rng.Next(0, 100)}");
                    break;

                case "seed":
                    if (parts.Length >= 2 && int.TryParse(parts[1], out var s)) { _rng.Reseed(s); _io.WriteLine($"Seeded: {s}"); }
                    else _io.WriteLine("Usage: seed <int>");
                    break;

                case "genstars":
                    _state.PlaceStars(_rng);
                    _io.WriteLine("Stars placed.");
                    break;

                case "explore":
                    foreach (var star in _state.Stars.Skip(1)) star.ExploredBy[1] = true;
                    _io.WriteLine("Explored by player.");
                    break;

                case "map":
                    _mapRenderer.Render(_io, _state);
                    break;

                case "liststars":
                    foreach (var star in _state.Stars.Skip(1))
                        _io.WriteLine($"{star.Name} ({star.X},{star.Y}) {(star.ExploredBy[1] ? "[seen]" : "")}");
                    break;

                case "reveal":
                    if (parts.Length >= 2) {
                        var target = parts[1].ToUpperInvariant( );
                        var star = _state.Stars.Skip(1).FirstOrDefault(st => st.Name.Equals(target, StringComparison.OrdinalIgnoreCase));
                        if (star is null) _io.WriteLine($"No such star: {target}");
                        else { star.ExploredBy[1] = true; _io.WriteLine($"Revealed {star.Name}"); }
                    }
                    else _io.WriteLine("Usage: reveal <starName>");
                    break;

                case "fog":
                    if (parts.Length >= 2 && (parts[1].Equals("on", StringComparison.OrdinalIgnoreCase) || parts[1].Equals("off", StringComparison.OrdinalIgnoreCase))) {
                        _state.FogEnabled = parts[1].Equals("on", StringComparison.OrdinalIgnoreCase);
                        _io.WriteLine($"Fog {(_state.FogEnabled ? "ON" : "OFF")}");
                    }
                    else _io.WriteLine("Usage: fog on|off");
                    break;

                case "nameat":
                    if (parts.Length >= 3 && int.TryParse(parts[1], out var nx) && int.TryParse(parts[2], out var ny)) {
                        var star = _state.StarAt(nx, ny);
                        _io.WriteLine(star is null ? "(empty)" : star.Name);
                    }
                    else _io.WriteLine("Usage: nameat <x> <y>");
                    break;

                default:
                    _io.WriteLine($"Unknown command: {cmd}");
                    break;
            }
        }
        return 0;
    }
}
