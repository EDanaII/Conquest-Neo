using System;
using System.Globalization;

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

    public static GameConfig Default => new GameConfig( ).Validate( );

    public static GameConfig FromArgs(string[] args) {
        var cfg = new GameConfig( );
        for (int i = 0; i < args.Length - 1; i++) {
            switch (args[i]) {
                case "--width" or "-w": if (int.TryParse(args[i + 1], out var w)) cfg.Width = w; break;
                case "--height" or "-h": if (int.TryParse(args[i + 1], out var h)) cfg.Height = h; break;
                case "--stars" or "-s": if (int.TryParse(args[i + 1], out var n)) cfg.NumStars = n; break;
                case "--turns" or "-t": if (int.TryParse(args[i + 1], out var m)) cfg.MaxTurns = m; break;
                case "--victory" or "-v":
                    if (double.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var V))
                        cfg.VictoryMultiplier = V;
                    break;
                case "--overwhelm" or "-o":
                    if (double.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var O))
                        cfg.OverwhelmFactor = O;
                    break;
                case "--capacity" or "-c": if (int.TryParse(args[i + 1], out var C)) cfg.Capacity = C; break;
                case "--seed" or "-seed": if (int.TryParse(args[i + 1], out var sd)) cfg.Seed = sd; break;
            }
        }
        return cfg.Validate( );
    }

    public GameConfig Validate( ) {
        // Basic >0 checks
        if (Width <= 0) throw new ArgumentOutOfRangeException(nameof(Width));
        if (Height <= 0) throw new ArgumentOutOfRangeException(nameof(Height));
        if (MaxTurns <= 0) throw new ArgumentOutOfRangeException(nameof(MaxTurns));

        // Clamp to engine limits
        if (Width > Constants.Max.BoardSize) Width = Constants.Max.BoardSize;
        if (Height > Constants.Max.BoardSize) Height = Constants.Max.BoardSize;

        // Stars must be non-negative and not exceed available cells
        if (NumStars < 0) throw new ArgumentOutOfRangeException(/*nameof(NumStars)*/);
        var maxByCells = (long)Width * Height; // avoid overflow
        if (NumStars > maxByCells) NumStars = (int)maxByCells;

        // Optional sanity on tuning knobs
        if (VictoryMultiplier <= 0) VictoryMultiplier = 1.0;
        if (OverwhelmFactor <= 0) OverwhelmFactor = 1.0;

        // Capacity can be zero or positive; clamp to something sane if you have a max
        if (Capacity < 0) Capacity = 0;

        return this;
    }
}