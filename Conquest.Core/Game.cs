// File: Conquest.Core/Game.cs
// Canonical Game class with 4-arg ctor and a tiny command loop.
// Works with IMapRenderer (external renderer) and calls GenerateStars(...) from the partial.

using System;
using Conquest.Core.Model;

namespace Conquest.Core {
    public partial class Game {
        private readonly IGameIO _io;
        private readonly IRng _rng;
        private readonly IMapRenderer _renderer;

        public Game(IGameIO io, IRng rng, GameState state, IMapRenderer renderer) {
            _io = io ?? throw new ArgumentNullException(nameof(io));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            State = state ?? throw new ArgumentNullException(nameof(state));
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        }

        public GameState State { get; }

        private int? FindStarAt(int x, int y) {
            for (int i = 1; i < State.Stars.Count; i++)
                if (State.Stars[i].X == x && State.Stars[i].Y == y)
                    return i;
            return null;
        }

        /// <summary>
        /// Minimal command loop for tests:
        /// seed N | genstars | map | reveal [on|off] | quit
        /// </summary>
        // File: Conquest.Core/Game.cs  (inside public partial class Game)
        public int Run( ) {
            _io.WriteLine("CONQUEST (C# port)");
            _io.WriteLine($"Board: {State.Config.Width}x{State.Config.Height}  Stars: {State.Config.NumStars}  Turns: {State.Config.MaxTurns}");
            _io.WriteLine("Type 'help' for commands, 'quit' to exit.");

            while (true) {
                var line = _io.ReadLine( );
                if (line is null) break;

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                var cmd = parts[0].ToLowerInvariant( );

                switch (cmd) {
                    case "quit":
                        return 0;

                    case "seed":
                        if (parts.Length >= 2 && int.TryParse(parts[1], out var seed))
                            (_rng as ISeedableRng)?.Seed(seed);
                        continue;

                    case "genstars":
                        GenerateStars(_rng, State);
                        continue;

                    case "map":
                        _renderer.Render(_io, State);
                        continue;

                    // Fog: on/off/toggle
                    case "fog":
                        if (parts.Length == 1) State.FogEnabled = !State.FogEnabled;
                        else if (parts[1].Equals("on", StringComparison.OrdinalIgnoreCase)) State.FogEnabled = true;
                        else if (parts[1].Equals("off", StringComparison.OrdinalIgnoreCase)) State.FogEnabled = false;
                        _io.WriteLine($"Fog: {(State.FogEnabled ? "ON" : "OFF")}");
                        continue;

                    // Reveal debug overlay: on/off/toggle
                    case "reveal":
                        if (parts.Length == 1) State.RevealStars = !State.RevealStars;
                        else State.RevealStars = parts[1].Equals("on", StringComparison.OrdinalIgnoreCase);
                        _io.WriteLine($"Reveal mode: {(State.RevealStars ? "ON" : "OFF")}");
                        continue;

                    // Mark entire map visible (keeps fog flag true; this just fills the Visible grid).
                    case "revealall":
                        State.RevealAll( );
                        _io.WriteLine("All cells marked visible.");
                        continue;

                    /* TODO: ? Clear visibility grid to all-false.
                    case "hideall":
                        State.HideAll( );
                        _io.WriteLine("All cells hidden.");
                        continue;
                    */

                    // Discover x y [r]  (handy for testing fog without gameplay)
                    case "discover":
                        if (parts.Length >= 3
                            && int.TryParse(parts[1], out var dx)
                            && int.TryParse(parts[2], out var dy)) {
                            var r = (parts.Length >= 4 && int.TryParse(parts[3], out var rr)) ? rr : 0;
                            State.DiscoverRadius(dx, dy, r);
                            _io.WriteLine($"Discovered {(r > 0 ? $"radius {r} at" : "cell")} ({dx},{dy}).");
                        }
                        else _io.WriteLine("usage: discover x y [radius]");
                        continue;

                    // List star positions (debug)
                    case "stars":
                        for (int i = 0; i < State.Stars.Count; i++)
                            _io.WriteLine($"{i,2}: ({State.Stars[i].X},{State.Stars[i].Y})");
                        continue;

                    // Quick help
                    case "help":
                        _io.WriteLine("Commands: seed N | genstars | map | fog [on|off] | reveal [on|off] | revealall | hideall | discover x y [r] | stars | quit");
                        continue;

                    case "place": {
                            // usage: place <starId> [tfIndex=1] [ships=3]
                            if (parts.Length < 2) { _io.WriteLine("usage: place <starId> [tfIndex=1] [ships=3]"); continue; }
                            if (!int.TryParse(parts[1], out var starId)) { _io.WriteLine("starId must be int"); continue; }
                            var tfIndex = (parts.Length > 2 && int.TryParse(parts[2], out var tf) ? tf : 1);
                            var ships = (parts.Length > 3 && int.TryParse(parts[3], out var sh) ? sh : 3);

                            var tfObj = State.FleetsSys.Place(team: 1, tfIndex: tfIndex, starIndex: starId, scouts: ships);
                            _io.WriteLine($"Placed TF {tfIndex} at star {starId} with {ships} scouts.");
                            continue;
                        }

                    case "move":
                    case "course": {
                            // usage: move <tfIndex> <starId>
                            if (parts.Length < 3) { _io.WriteLine("usage: move <tfIndex> <starId>"); continue; }
                            if (!int.TryParse(parts[1], out var tfIndex)) { _io.WriteLine("tfIndex must be int"); continue; }
                            if (!int.TryParse(parts[2], out var starId)) { _io.WriteLine("starId must be int"); continue; }

                            var tf = State.Players[1].Tf[tfIndex];
                            if (tf == null || (tf.X == 0 && tf.Y == 0)) {
                                _io.WriteLine($"TF {tfIndex} is not placed. Use: place <starId> [tfIndex=1] [ships=3]");
                                continue;
                            }
                            if (tf.Scouts <= 0) {
                                _io.WriteLine($"TF {tfIndex} has 0 ships. Use: place <starId> {tfIndex} <ships>");
                                continue;
                            }

                            State.FleetsSys.SetCourse(team: 1, tfIndex: tfIndex, starIndex: starId);
                            _io.WriteLine($"TF {tfIndex} course set to star {starId}. ETA {tf.ETA}.");
                            continue;
                        }

                    case "tick": {
                            // usage: tick [n]
                            var n = (parts.Length > 1 && int.TryParse(parts[1], out var nn)) ? nn : 1;
                            var t = this.Tick(n);
                            _io.WriteLine($"Advanced {n} tick(s). Turn={t}");
                            continue;
                        }

                    case "maptf": {
                            RenderMapWithFleets( );   // no parameter
                            continue;
                        }

                    case "listtf": {
                            var p = State.Players[1];
                            for (int i = 1; i < p.Tf.Length; i++) {
                                var tf = p.Tf[i];
                                if (tf == null) continue;
                                if (tf.X == 0 && tf.Y == 0 && tf.Scouts == 0 && tf.Destination is null) continue;

                                var atText = FindStarAt(tf.X, tf.Y) is int sid ? $" @ star {sid}" : "";
                                var destText = (tf.Destination is int d) ? $" -> star {d} (ETA {tf.ETA})" : "";
                                _io.WriteLine($"TF {i}: ({tf.X},{tf.Y}) scouts={tf.Scouts}{atText}{destText}");
                            }
                            continue;
                        }

                    default:
                        _io.WriteLine("Unknown command. Try 'help'.");
                        continue;
                }
            }

            return 0;
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
            int W = State.Config.Width, H = State.Config.Height;

            var grid = new char[H + 1, W + 1];

            // background
            for (int y = 1; y <= H; y++)
                for (int x = 1; x <= W; x++)
                    grid[y, x] = '.';  // later you can switch to ' ' for fog

            // stars first (A..Z)
            for (int i = 1; i < State.Stars.Count; i++) {
                var s = State.Stars[i];
                if (s.X >= 1 && s.X <= W && s.Y >= 1 && s.Y <= H)
                    grid[s.Y, s.X] = (char)('A' + ((i - 1) % 26));
            }

            // fleets overlay (draw even if scouts==0; skip truly unplaced (0,0))
            for (int team = 0; team <= 1; team++) {
                var p = State.Players[team];
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
}
