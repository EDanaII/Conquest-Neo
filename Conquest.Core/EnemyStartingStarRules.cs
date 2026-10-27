using Conquest.Core.Model;

namespace Conquest.Core;

public static class EnemyStartingStarRules {
    // OriginalSource/input.c: dist() and get_stars(). The candidate counts itself.
    public static int CountReachableStars(IReadOnlyList<Star> stars, int candidateIndex, int range) {
        var candidate = stars[candidateIndex];
        return stars.Count(star => {
            double dx = candidate.X - star.X;
            double dy = candidate.Y - star.Y;
            return Math.Sqrt(dx * dx + dy * dy) <= range;
        });
    }

    // OriginalSource/init.c: initmach(). Evaluate every candidate, retaining first ties.
    public static int SelectStartingStar(IReadOnlyList<Star> stars, int range, IRng rng) {
        if (stars.Count == 0) throw new ArgumentException("A starting star requires a nonempty galaxy.", nameof(stars));
        int winner = 0, highestScore = 0;
        for (int i = 0; i < stars.Count; i++) {
            int score = CountReachableStars(stars, i, range) + rng.Next(1, 6);
            if (score > highestScore) {
                highestScore = score;
                winner = i;
            }
        }
        return winner;
    }
}
