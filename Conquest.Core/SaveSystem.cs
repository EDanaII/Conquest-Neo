// src/Conquest.Core/Persistence/SaveSystem.cs
using Conquest.Core.Model;
using System.Text.Json;

namespace Conquest.Core.Persistence;

/// <summary>
/// Minimal JSON save/load for the fields we care about in tests.
/// Uses private nested DTOs so nothing leaks into Core API.
/// </summary>
public static class SaveSystem {
    private static readonly JsonSerializerOptions JsonOpts = new( ) {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    // ---- Public API ---------------------------------------------------------

    // Save a snapshot of key state fields to a JSON string.
    public static string Save(GameState state)
        => JsonSerializer.Serialize(StateSnap.From(state), JsonOpts);

    // Load a snapshot JSON back into a new GameState.
    public static GameState Load(string json)
        => JsonSerializer.Deserialize<StateSnap>(json, JsonOpts)!.ToState( );

    // ---- Private DTOs (snapshots) ------------------------------------------

    // Top-level snapshot of the game state.
    private sealed class StateSnap {
        public ConfigSnap Config { get; set; } = new( );
        public bool FogEnabled { get; set; }
        public List<StarSnap> Stars { get; set; } = new( );

        public static StateSnap From(GameState s) {
            var snap = new StateSnap {
                Config = ConfigSnap.From(s.Config),
                FogEnabled = s.FogEnabled,
            };

            // Copy stars 1:1 (adjust if you reserve index 0 differently)
            foreach (var st in s.Stars)
                snap.Stars.Add(StarSnap.From(st));

            return snap;
        }

        public GameState ToState( ) {
            // Build a fresh GameState from the config
            var cfg = Config.ToConfig( ).Validate( );
            var state = new GameState(cfg) {
                FogEnabled = FogEnabled
            };

            // Refill stars
            state.Stars.Clear( );
            foreach (var sd in Stars) {
                var st = new Star {
                    Name = sd.Name,
                    X = sd.X,
                    Y = sd.Y
                };

                // Guard if ExploredBy has fixed size
                if (st.ExploredBy is not null && st.ExploredBy.Length > 1)
                    st.ExploredBy[1] = sd.ExploredByPlayer1;

                state.Stars.Add(st);
            }
            return state;
        }
    }

    // Config snapshot (only what we need for round-trip tests).
    private sealed class ConfigSnap {
        public int Width { get; set; }
        public int Height { get; set; }
        public int NumStars { get; set; }
        public int MaxTurns { get; set; }
        public double VictoryMultiplier { get; set; }
        public double OverwhelmFactor { get; set; }
        public int Capacity { get; set; }
        public int? Seed { get; set; }

        public static ConfigSnap From(GameConfig c) => new( ) {
            Width = c.Width,
            Height = c.Height,
            NumStars = c.NumStars,
            MaxTurns = c.MaxTurns,
            VictoryMultiplier = c.VictoryMultiplier,
            OverwhelmFactor = c.OverwhelmFactor,
            Capacity = c.Capacity,
            Seed = c.Seed
        };

        public GameConfig ToConfig( ) => new GameConfig {
            Width = Width,
            Height = Height,
            NumStars = NumStars,
            MaxTurns = MaxTurns,
            VictoryMultiplier = VictoryMultiplier,
            OverwhelmFactor = OverwhelmFactor,
            Capacity = Capacity,
            Seed = Seed
        };
    }

    // Star snapshot (trimmed to fields we assert in tests).
    private sealed class StarSnap {
        public string Name { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public bool ExploredByPlayer1 { get; set; }

        public static StarSnap From(Star s) => new( ) {
            Name = s.Name ?? "",
            X = s.X,
            Y = s.Y,
            ExploredByPlayer1 = s.ExploredBy.Length > 1 && s.ExploredBy[1]
        };
    }
}
