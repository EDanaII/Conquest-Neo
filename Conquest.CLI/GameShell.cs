using Conquest.Core;
using Conquest.Core.Model;
using Conquest.Core.Random;

using System.Text;

namespace Conquest.CLI;

public sealed class GameShell {
    private readonly Game _game;
    private readonly IGameIO _io;
    private readonly IRng _rng;
    private readonly IMapRenderer _renderer;

    public GameShell(Game game, IGameIO io, IRng rng, IMapRenderer renderer) {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _io = io ?? throw new ArgumentNullException(nameof(io));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    public int Run( ) {
        _io.WriteLine("CONQUEST (C# port)");
        _io.WriteLine($"Board: {_game.State.Config.Width}x{_game.State.Config.Height}  Stars: {_game.State.Config.NumStars}  Turns: {_game.State.Config.MaxTurns}");
        _io.WriteLine("Type 'help' for commands, 'quit' to exit.");

        while (true) {
            var line = _io.ReadLine( );
            if (line is null) break;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            var cmd = parts[0].ToLowerInvariant( );

            switch (cmd) {
                case "q":
                case "quit":
                    return 0;

                case "seed":
                    Seed(parts);
                    continue;

                case "genstars":
                    _game.GenerateStars( );
                    continue;

                case "m":
                case "map":
                    _renderer.Render(_io, _game.State);
                    continue;

                case "fog":
                    Fog(parts);
                    continue;

                case "reveal":
                    Reveal(parts);
                    continue;

                case "discover":
                    Discover(parts);
                    continue;

                case "stars":
                    Stars( );
                    continue;

                case "h":
                case "help":
                    Help( );
                    continue;

                case "place":
                    Place(parts);
                    continue;

                case "move":
                case "course":
                    Course(parts);
                    continue;

                case "tick":
                    Tick(parts);
                    continue;

                case "tf":
                    TaskForce(parts);
                    continue;

                default:
                    _io.WriteLine("Unknown command. Try 'help'.");
                    continue;
            }
        }

        return 0;
    }

    private void Course(string[] parts) {
        // usage: move <tfIndex> <starId>
        if (parts.Length < 3) { _io.WriteLine("usage: move <tfIndex> <starId>"); return; }
        if (!int.TryParse(parts[1], out var tfIndex)) { _io.WriteLine("tfIndex must be int"); return; }
        if (!int.TryParse(parts[2], out var starId)) { _io.WriteLine("starId must be int"); return; }

        var tf = _game.State.Players[1].Tf[tfIndex];
        if (tf == null || (tf.X == 0 && tf.Y == 0)) {
            _io.WriteLine($"TF {tfIndex} is not placed. Use: place <starId> [tfIndex=1] [ships=3]");
            return;
        }
        if (tf.Scouts <= 0) {
            _io.WriteLine($"TF {tfIndex} has 0 ships. Use: place <starId> {tfIndex} <ships>");
            return;
        }

        _game.State.FleetsSys.SetCourse(team: 1, tfIndex: tfIndex, starIndex: starId);
        _io.WriteLine($"TF {tfIndex} course set to star {starId}. ETA {tf.ETA}.");
    }

    private void Discover(string[] parts) {
        if (parts.Length >= 3
            && int.TryParse(parts[1], out var dx)
            && int.TryParse(parts[2], out var dy)) {
            var r = (parts.Length >= 4 && int.TryParse(parts[3], out var rr)) ? rr : 0;
            _game.State.DiscoverRadius(dx, dy, r);
            _io.WriteLine($"Discovered {(r > 0 ? $"radius {r} at" : "cell")} ({dx},{dy}).");
        }
        else _io.WriteLine("usage: discover x y [radius]");
    }

    private int? FindStarAt(int x, int y) {
        for (int i = 1; i < _game.State.Stars.Count; i++)
            if (_game.State.Stars[i].X == x && _game.State.Stars[i].Y == y)
                return i;
        return null;
    }

    private void Fog(string[] parts) {
        if (parts.Length == 1) _game.State.FogEnabled = !_game.State.FogEnabled;
        else _game.State.FogEnabled = parts[1].Equals("on", StringComparison.OrdinalIgnoreCase);
        _io.WriteLine($"Fog: {(_game.State.FogEnabled ? "ON" : "OFF")}");
    }

    private void Help( ) {
        _io.WriteLine("Commands: quit (q) | seed N | genstars | map (m) | fog [on|off] | reveal [on|off|all] | discover x y [r] | stars | place <starId> [tfIndex] [ships] | move <tfIndex> <starId> | tick [n] | tf [map|list]");
    }

    private void Place(string[] parts) {
        if (parts.Length < 2) {
            _io.WriteLine("usage: place <starId> [tfIndex=1] [ships=3]");
            return;
        }

        if (!int.TryParse(parts[1], out var starId)) {
            _io.WriteLine("starId must be int");
            return;
        }

        var tfIndex = (parts.Length > 2 && int.TryParse(parts[2], out var tf)) ? tf : 1;
        var ships = (parts.Length > 3 && int.TryParse(parts[3], out var sh)) ? sh : 3;

        _game.State.FleetsSys.Place(team: 1, tfIndex: tfIndex, starIndex: starId, scouts: ships);
        _io.WriteLine($"Placed TF {tfIndex} at star {starId} with {ships} scouts.");
    }

    private void Reveal(string[] parts) {
        if (parts.Length == 1) {
            _game.State.RevealStars = !_game.State.RevealStars;
            _io.WriteLine($"Reveal mode: {(_game.State.RevealStars ? "ON" : "OFF")}");
            return;
        }

        var arg = parts[1].ToLowerInvariant( );

        switch (arg) {
            case "on":
                _game.State.RevealStars = true;
                _io.WriteLine("Reveal mode: ON");
                return;

            case "off":
                _game.State.RevealStars = false;
                _io.WriteLine("Reveal mode: OFF");
                return;

            case "all":
                _game.State.RevealAll( );
                _io.WriteLine("All cells marked visible.");
                return;

            default:
                _io.WriteLine("usage: reveal [on|off|all]");
                return;
        }
    }

    private void Seed(string[] parts) {
        if (parts.Length >= 2 && int.TryParse(parts[1], out var seed))
            (_rng as ISeedableRng)?.Seed(seed);
    }

    private void Stars( ) {
        for (int i = 0; i < _game.State.Stars.Count; i++)
            _io.WriteLine($"{i,2}: ({_game.State.Stars[i].X},{_game.State.Stars[i].Y})");
    }

    private void TaskForce(string[] parts) {
        if (parts.Length < 2) {
            _io.WriteLine("usage: tf [list|map]");
            return;
        }

        var arg = parts[1].ToLowerInvariant( );

        switch (arg) {
            case "list":
                TfList( );
                return;

            case "map":
                RenderMapWithFleets( );
                return;

            default:
                _io.WriteLine("usage: tf [list|map]");
                return;
        }
    }

    private void TfList( ) {
        var p = _game.State.Players[1];
        for (int i = 1; i < p.Tf.Length; i++) {
            var tf = p.Tf[i];
            if (tf == null) continue;
            if (tf.X == 0 && tf.Y == 0 && tf.Scouts == 0 && tf.Destination is null) continue;

            var atText = FindStarAt(tf.X, tf.Y) is int sid ? $" @ star {sid}" : "";
            var destText = (tf.Destination is int d) ? $" -> star {d} (ETA {tf.ETA})" : "";
            _io.WriteLine($"TF {i}: ({tf.X},{tf.Y}) scouts={tf.Scouts}{atText}{destText}");
        }
    }

    private void Tick(string[] parts) {
        var n = (parts.Length > 1 && int.TryParse(parts[1], out var nn)) ? nn : 1;
        var t = _game.Tick(n);
        _io.WriteLine($"Advanced {n} tick(s). Turn={t}");
    }
    
    /* TODO: Remove commented-out C-parity star generator if unused.
    private void GenerateStars(IRng rng, Conquest.Core.Model.GameState state) {
        var cfg = state.Config;

        if (cfg.Width <= 0 || cfg.Height <= 0)
            throw new InvalidOperationException("Board dimensions must be positive.");

        var capacity = (long)cfg.Width * cfg.Height;
        if (cfg.NumStars < 0 || cfg.NumStars > capacity)
            throw new InvalidOperationException("NumStars must be between 0 and Width*Height.");

        // Ensure Stars list has exactly NumStars elements.
        if (state.Stars.Count != cfg.NumStars) {
            state.Stars.Clear( );
            for (int i = 0; i < cfg.NumStars; i++)
                state.Stars.Add(new Star( ));
        }

        // Clear board star refs / marks.
        for (int y = 0; y < cfg.Height; y++)
            for (int x = 0; x < cfg.Width; x++) {
                var cell = state.Board[x, y];
                cell.StarRef = null;
                cell.Star = -1; // legacy index marker
            }

        var used = new HashSet<(int x, int y)>( );

        for (int i = 0; i < cfg.NumStars; i++) {
            int x, y;
            do {
                x = rng.Next(0, cfg.Width);   // 0..Width-1
                y = rng.Next(0, cfg.Height);  // 0..Height-1
            } while (!used.Add((x, y)));

            var s = state.Stars[i];
            s.X = x; s.Y = y;

            var cell = state.Board[x, y];
            cell.StarRef = s;
            cell.Star = i; // keep legacy/indexy field in sync
        }
    }
    */

    private void RenderMapWithFleets( ) {
        var io = _io;
        int W = _game.State.Config.Width, H = _game.State.Config.Height;

        var grid = new char[H + 1, W + 1];

        // background
        for (int y = 1; y <= H; y++)
            for (int x = 1; x <= W; x++)
                grid[y, x] = '.';  // later you can switch to ' ' for fog

        // stars first (A..Z)
        for (int i = 1; i < _game.State.Stars.Count; i++) {
            var s = _game.State.Stars[i];
            if (s.X >= 1 && s.X <= W && s.Y >= 1 && s.Y <= H)
                grid[s.Y, s.X] = (char)('A' + ((i - 1) % 26));
        }

        // fleets overlay (draw even if scouts==0; skip truly unplaced (0,0))
        for (int team = 0; team <= 1; team++) {
            var p = _game.State.Players[team];
            for (int i = 1; i < p.Tf.Length; i++) {
                var tf = p.Tf[i];
                if (tf == null) continue;
                int x = tf.X, y = tf.Y;
                if (x == 0 && y == 0) continue; // not placed yet
                if (x >= 1 && x <= W && y >= 1 && y <= H)
                    grid[y, x] = team == 1 ? '1' : '2';
            }
        }

        var header = "   " + string.Concat(Enumerable.Range(1, W).Select(c => (char)('0' + (c % 10))));
        io.WriteLine(header);
        for (int y = H; y >= 1; y--) {
            var sb = new System.Text.StringBuilder( );
            sb.Append(y.ToString( ).PadLeft(2)).Append(' ');
            for (int x = 1; x <= W; x++) sb.Append(grid[y, x]);
            sb.Append(' ').Append(y.ToString( ).PadLeft(2));
            io.WriteLine(sb.ToString( ));
        }
        io.WriteLine(header);
    }
}