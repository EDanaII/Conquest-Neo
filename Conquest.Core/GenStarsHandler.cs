using Conquest.Core;
using Conquest.Core.Random;
using Conquest.Core.Stars;

public interface ICommandHandler { string Name { get; } void Run(string[] args, IGameIO io); }

public sealed class GenStarsHandler : ICommandHandler {
    public string Name => "genstars";

    public void Run(string[] args, IGameIO io) {
        int seed = args.GetInt("--seed", 12345);
        int count = args.GetInt("--count", 20);
        int w = args.GetInt("--w", 64);
        int h = args.GetInt("--h", 64);
        int space = args.GetInt("--space", 3);

        var rng = new CRandRng( );
        rng.Seed(seed);               

        var gen = new StarGenerator(w, h, space);
        var stars = gen.Generate(count, rng);

        for (int i = 0; i < stars.Count; i++)
            io.WriteLine($"{i,2}: ({stars[i].x},{stars[i].y})");
    }
}
