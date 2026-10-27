using Conquest.Core.Model;

namespace Conquest.Core;

internal static class ResearchRules {
    // main.c: requirements indexed by the target level, not cumulative totals.
    private static readonly int[] WeaponsRequirements = { 0, 0, 0, 0, 50, 70, 90, 120, 150, 250, 350 };
    private static readonly int[] RangeRequirements = { 0, 0, 0, 0, 0, 0, 20, 40, 70, 100, 150, 200, 300, 400, 600, 900 };
    private static readonly int[] VelocityRequirements = { 0, 0, 40, 60, 80, 120, 150, 200, 250, 300, 400, 500, 600 };

    // Player-only research() / research_limited(). No enemy field decisions or RNG.
    internal static void ApplyPlayerInvestment(Player player, ResearchField field, int amount) {
        var (level, progress, minimum, requirements) = field switch {
            ResearchField.Weapons => (player.Weapons, player.WeaponsResearchProgress, 3, WeaponsRequirements),
            ResearchField.Range => (player.Range, player.RangeResearchProgress, 5, RangeRequirements),
            ResearchField.Velocity => (player.Velocity, player.VelocityResearchProgress, 1, VelocityRequirements),
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        int maximum = requirements.Length - 1;
        if (level < minimum || level > maximum || progress < 0
            || (level == maximum ? progress != 0 : progress >= requirements[level + 1]))
            throw new InvalidOperationException("Player technology or research progress is outside the supported Classic state.");

        while (amount > 0 && level < maximum) {
            int used = Math.Min(amount, requirements[level + 1] - progress);
            progress += used;
            amount -= used;
            if (progress >= requirements[level + 1]) {
                level++;
                progress = 0;
            }
        }
        switch (field) {
            case ResearchField.Weapons: player.Weapons = level; player.WeaponsResearchProgress = progress; break;
            case ResearchField.Range: player.Range = level; player.RangeResearchProgress = progress; break;
            case ResearchField.Velocity: player.Velocity = level; player.VelocityResearchProgress = progress; break;
        }
    }

    // initmach() invests 2 in V. At startup, velocity is 1..3 with zero
    // progress; the next thresholds in main.c are 40, 60, 80. No breakthrough.
    internal static ResearchField ApplyEnemyStartupResearch(Player enemy, Player player, IRng rng) {
        enemy.VelocityResearchProgress += 2;

        // research() calls new_research() even after consuming the whole investment.
        if (enemy.Weapons < Constants.Max.Weapons
            && player.Weapons - enemy.Weapons + 5 > rng.Next(1, 11))
            return ResearchField.Weapons;

        if (enemy.Velocity < Constants.Max.Velocity
            && enemy.Range / enemy.Velocity > rng.Next(1, 4))
            return ResearchField.Velocity;

        return ResearchField.Range;
    }
}
