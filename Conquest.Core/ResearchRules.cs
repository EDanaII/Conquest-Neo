using Conquest.Core.Model;

namespace Conquest.Core;

internal static class ResearchRules {
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
