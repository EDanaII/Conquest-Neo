namespace Conquest.Core;

public sealed class GameConfig {
    public int Width { get; set; } = Constants.Max.BoardSize;
    public int Height { get; set; } = Constants.Max.BoardSize;
    public int NumStars { get; set; } = Constants.Max.Stars;
    public int MaxTurns { get; set; } = Constants.Max.Turns;

    public double VictoryMultiplier { get; set; } = 1.5;
    public double OverwhelmFactor { get; set; } = 8.0;
    public int Capacity { get; set; } = 0;
    public int? Seed { get; set; } = null;

    public GameConfig Validate( ) { /* same checks */ return this; }

    public static GameConfig FromArgs(string[] args) {
        var cfg = new GameConfig( );
        for (int i = 0; i < args.Length - 1; i++) {
            switch (args[i]) {
                case "--width" or "-w": if (int.TryParse(args[i + 1], out var w)) cfg.Width = w; break;
                case "--height" or "-h": if (int.TryParse(args[i + 1], out var h)) cfg.Height = h; break;
                case "--stars" or "-s": if (int.TryParse(args[i + 1], out var n)) cfg.NumStars = n; break;
                case "--turns" or "-t": if (int.TryParse(args[i + 1], out var m)) cfg.MaxTurns = m; break;
                case "--victory" or "-v": if (double.TryParse(args[i + 1], out var V)) cfg.VictoryMultiplier = V; break;
                case "--overwhelm" or "-o": if (double.TryParse(args[i + 1], out var O)) cfg.OverwhelmFactor = O; break;
                case "--capacity" or "-c": if (int.TryParse(args[i + 1], out var C)) cfg.Capacity = C; break;
                case "--seed" or "-seed": if (int.TryParse(args[i + 1], out var sd)) cfg.Seed = sd; break;
            }
        }
        return cfg.Validate( );
    }
}
