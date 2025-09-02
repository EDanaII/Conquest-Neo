using Conquest.Core.Random;
namespace Conquest.Core.Stars;

public sealed class StarGenerator {
    // literal knobs from the C code — adjust to your actual values
    public int Width { get; }
    public int Height { get; }
    public int MinSpacing { get; }

    public StarGenerator(int width, int height, int minSpacing) {
        Width = width;
        Height = height;
        MinSpacing = minSpacing;
    }

    public IReadOnlyList<(int x, int y)> Generate(int count, ICRandRng rng) {
        var stars = new List<(int x, int y)>(capacity: count);

        // 1:1 C-ish helpers to avoid .NET’s distribution differences
        int NextInt(int max) => rng.NextInt(max);              // 0..max-1 (C: rand() % max)
        int NextInRange(int lo, int hi) => lo + NextInt(hi - lo + 1); // inclusive range like typical C helpers

        int attempts = 0;
        while (stars.Count < count) {
            // match C’s order of operations: pick x then y, clamp if needed
            int x = NextInRange(0, Width - 1);
            int y = NextInRange(0, Height - 1);

            // spacing rule (literal port): reject if within MinSpacing (Manhattan or Euclidean — pick what C did)
            bool ok = true;
            foreach (var (sx, sy) in stars) {
                // if C used Manhattan:
                // int d = Math.Abs(sx - x) + Math.Abs(sy - y);
                // if (d < MinSpacing) { ok = false; break; }

                // if C used Euclidean (integer compare to avoid sqrt):
                int dx = sx - x, dy = sy - y;
                int d2 = dx * dx + dy * dy;
                if (d2 < MinSpacing * MinSpacing) { ok = false; break; }
            }

            if (ok)
                stars.Add((x, y));

            // (optional) bail-out guard to mirror C’s retry cap if it had one
            if (++attempts > 10_000_000)
                throw new InvalidOperationException("Star generation retry cap exceeded. Check spacing/board size.");
        }

        return stars;
    }
}
