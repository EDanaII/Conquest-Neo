using Conquest.Core.Model;

namespace Conquest.Core;

internal static class PlayerStartupRules {
    // OriginalSource/init.c: pick_planet(). In particular, test count BEFORE increment.
    internal static int SelectByCapacity(IReadOnlyList<Star> stars, int capacity, IRng rng) {
        int count = 0;
        while (true) {
            int candidate = rng.Next(1, stars.Count + 1) - 1;
            bool found = capacity <= 0 || stars[candidate].Planets.Any(p => p.Capacity >= capacity);
            if (count++ > stars.Count) {
                capacity -= 10;
                count = 0;
            }
            if (found) return candidate;
        }
    }

    internal static int ShipCost(StartupShip ship) => ship switch {
        StartupShip.Cruiser => Constants.Cost.Cruiser,
        StartupShip.Scout => Constants.Cost.Scout,
        StartupShip.Battlestar => Constants.Cost.Battleship,
        _ => throw new ArgumentOutOfRangeException(nameof(ship))
    };
}
