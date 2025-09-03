namespace Conquest.Core.Model;

public partial class GameState {
    public GameConfig Config { get; }

    // All engine data is 0-based internally.
    public List<Star> Stars { get; }
    public Player[] Players { get; } = new Player[] { new Player( ), new Player( ) };

    // 0-based board: [0..Width-1, 0..Height-1]
    public Sector[,] Board { get; }

    public GameState(GameConfig? config = null) {
        Config = (config ?? new GameConfig( )).Validate( );

        // Exactly NumStars (0..NumStars-1) — no dummy element
        Stars = new List<Star>(Config.NumStars);
        for (int i = 0; i < Config.NumStars; i++)
            Stars.Add(new Star { Name = NameFor(i) });

        // Allocate exact size and init sectors
        Board = new Sector[Config.Width, Config.Height];
        for (int x = 0; x < Config.Width; x++)
            for (int y = 0; y < Config.Height; y++)
                Board[x, y] = new Sector { ViewingTeam = 1 }; // 1 = player viewpoint by default
    }

    // Place stars at unique coords; safe to call multiple times (it clears previous placement)
    public void PlaceStars(IRng rng) {
        // clear old placements
        for (int x = 0; x < Config.Width; x++)
            for (int y = 0; y < Config.Height; y++)
                Board[x, y].StarRef = null;

        var used = new HashSet<(int x, int y)>( );
        for (int i = 0; i < Config.NumStars; i++) {
            int x, y;
            do {
                x = rng.Next(0, Config.Width);   // 0..Width-1
                y = rng.Next(0, Config.Height);  // 0..Height-1
            } while (!used.Add((x, y)));

            var s = Stars[i];
            s.X = x; s.Y = y;

            var cell = Board[x, y];
            cell.StarRef = s;
            cell.Star = i; // index into Stars (0-based)
        }
    }

    private static string NameFor(int i) {
        // A..Z, then AA..AZ, BA.. etc. (i is 0-based)
        const string ABC = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string name = "";
        do { name = ABC[i % 26] + name; i = i / 26 - 1; } while (i >= 0);
        return name;
    }

    public bool FogEnabled { get; set; } = false;

    // 0-based bounds: valid x in [0,Width-1], y in [0,Height-1]
    public Star? StarAt(int x, int y) {
        if (x < 0 || x >= Config.Width || y < 0 || y >= Config.Height) return null;
        return Board[x, y].StarRef;
    }
}
