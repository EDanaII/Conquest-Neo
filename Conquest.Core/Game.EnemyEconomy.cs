using Conquest.Core.Model;

namespace Conquest.Core;

public partial class Game {
    private void InvestEnemyColony(int star, Planet planet) {
        int slot = StationaryFleet(0, star);
        if (slot == 0) slot = State.FleetsSys.AllocateCombatFleet(0, star);
        var fleet = State.Players[0].Tf[slot];
        int balance = planet.IndustrialUnits;
        int minimumBases = planet.Capacity / 20;
        while (planet.AdvancedMissileBases == 0 && !planet.Conquered && planet.MissileBases < minimumBases && balance >= Constants.Cost.MissileBase) {
            balance -= Constants.Cost.MissileBase; planet.MissileBases++;
        }
        // Despite the C comment "Build an amb more", this constructs a battlestar.
        if (balance >= Constants.Cost.Battleship && planet.AdvancedMissileBases > 1
            && _rng.Next(1, 6) != 1 && _rng.Next(1, 8) <= planet.AdvancedMissileBases + 3) {
            balance -= Constants.Cost.Battleship; fleet.BattleStars++;
        }
        if (balance >= Constants.Cost.AdvMissileBase && !planet.Conquered
            && (planet.AdvancedMissileBases < 4 || _rng.Next(1, 3) == 2)) {
            balance -= Constants.Cost.AdvMissileBase; planet.AdvancedMissileBases++;
        }
        void Infrastructure() {
            int units = Math.Min(3, planet.Inhabitants * Constants.Init.UnitRatio - planet.IndustrialUnits);
            balance -= units * Constants.Cost.Infrastructure; planet.IndustrialUnits += units;
        }
        while (balance >= 9) {
            switch (_rng.Next(1, 13)) {
                case 1: case 2:
                    ResearchRules.ApplyEnemyInvestment(State, _rng, 8); balance -= 8; break;
                case 3: case 4: case 10:
                    if (balance >= Constants.Cost.Cruiser) { balance -= Constants.Cost.Cruiser; fleet.Cruisers++; }
                    else if (!planet.Conquered && balance >= Constants.Cost.MissileBase) {
                        balance -= Constants.Cost.MissileBase; planet.MissileBases++;
                    }
                    else { balance -= 9; ResearchRules.ApplyEnemyInvestment(State, _rng, 9); }
                    break;
                case 11: case 12:
                    if ((double)planet.Inhabitants / planet.Capacity < .6
                        || planet.Capacity >= Constants.Cost.Battleship / Constants.Init.UnitRatio && planet.IndustrialUnits < Constants.Cost.Battleship + 10)
                        Infrastructure();
                    else if (!planet.Conquered) {
                        int transports = Math.Min(_rng.Next(1, 3) + 6, planet.Inhabitants - 1);
                        if (planet.IndustrialUnits > Constants.Cost.Battleship) transports = Math.Min(transports, planet.IndustrialUnits - Constants.Cost.Battleship);
                        balance -= transports; planet.Inhabitants -= transports;
                        planet.IndustrialUnits = Math.Min(planet.IndustrialUnits - transports, planet.Inhabitants * Constants.Init.UnitRatio);
                        fleet.Transports += transports;
                    }
                    break;
                default: Infrastructure(); break;
            }
        }
        State.FleetsSys.CleanupClassicFleet(0, slot);
        ResearchRules.ApplyEnemyInvestment(State, _rng, balance);
    }
}
