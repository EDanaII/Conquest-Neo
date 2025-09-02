using Conquest.Core.Models;

namespace Conquest.Core;

public class GameState {
    public GameConfig Config { get; }

    // 1-based arrays, C-style (slot 0 unused)
    public List<Star> Stars { get; }
    public Player[] Players { get; } = new Player[] { new Player( ), new Player( ) };

    // Board is also 1-based for parity with the C code
    public Sector[,] Board { get; }

    public GameState(GameConfig? config = null) {
        Config = (config ?? new GameConfig( )).Validate( );

        Stars = new List<Star>();
        for (int i = 0; i <= Config.NumStars; i++)
            Stars.Add(new Star { Name = NameFor(i) });

        Board = new Sector[Config.Width + 1, Config.Height + 1];
        for (int x = 1; x <= Config.Width; x++)
            for (int y = 1; y <= Config.Height; y++)
                Board[x, y] = new Sector { ViewingTeam = 1 }; // 1 = player viewpoint by default
    }

    // Place stars at unique coords; safe to call multiple times (it clears previous placement)
    public void PlaceStars(IRng rng) {
        // clear old placements
        for (int x = 1; x <= Config.Width; x++)
            for (int y = 1; y <= Config.Height; y++)
                Board[x, y].StarRef = null;

        var used = new HashSet<(int x, int y)>( );
        for (int i = 0; i <= Config.NumStars; i++) {
            int x, y;
            do {
                x = rng.Next(1, Config.Width + 1);
                y = rng.Next(1, Config.Height + 1);
            } while (!used.Add((x, y)));

            var s = Stars[i];
            s.X = x; s.Y = y;

            var cell = Board[x, y];
            cell.StarRef = s;
            cell.Star = i; // for any legacy/indexy code
        }
    }

    private static string NameFor(int i) {
        // A..Z, then AA..AZ, BA.. etc. (simple)
        const string ABC = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
//        i--; // zero-based WTF???
        string name = "";
        do { name = ABC[i % 26] + name; i = i / 26 - 1; } while (i >= 0);
        return name;
    }

    public bool FogEnabled { get; set; } = false;

    public Star? StarAt(int x, int y) {
        if (x < 1 || x > Config.Width || y < 1 || y > Config.Height) return null;
        return Board[x, y].StarRef;
    }
}