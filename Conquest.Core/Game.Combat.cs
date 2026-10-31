using Conquest.Core.Model;

namespace Conquest.Core;

public partial class Game {
    public int ValidateBombardmentFleet(int index) {
        if (index is < 1 or > 26) throw new InvalidOperationException(" !Illegal tf");
        var fleet = State.Players[1].Tf[index];
        if (fleet.Destination is not int star || (uint)star >= (uint)State.Stars.Count)
            throw new InvalidOperationException(" !Nonexistent tf");
        if (fleet.ETA != 0) throw new InvalidOperationException(" !Tf is not in normal space");
        if (fleet.Blasting != 0) throw new InvalidOperationException(" !Tf is already blasting");
        if (fleet.Cruisers == 0 && fleet.BattleStars == 0) throw new InvalidOperationException(" !Tf has no warships");
        if (State.Stars[star].Planets.Count == 0) throw new InvalidOperationException($" !No planets at star {State.Stars[star].Name}");
        return star;
    }
    public Planet ValidateBombardmentPlanet(int fleetIndex, int number) {
        int star = ValidateBombardmentFleet(fleetIndex);
        var planet = State.Stars[star].Planets.FirstOrDefault(p => p.Number == number)
            ?? throw new InvalidOperationException(" !No such planet at this star");
        if (planet.Owner == Ownership.Enemy) throw new InvalidOperationException(" !Conquer it first!");
        if (planet.Owner == Ownership.Player && !planet.Conquered)
            throw new InvalidOperationException(" !That is a human colony!!");
        return planet;
    }
    public int BombardmentMaximum(int fleetIndex) {
        ValidateBombardmentFleet(fleetIndex);
        return WarshipForces(State.Players[1].Tf[fleetIndex], 1) / 4;
    }
    public int BombardPlanet(int fleetIndex, int number, int units) {
        var planet = ValidateBombardmentPlanet(fleetIndex, number);
        if (units <= 0) return 0;
        if (units > int.MaxValue / 4) throw new InvalidOperationException(" !Invalid blast quantity");
        var fleet = State.Players[1].Tf[fleetIndex];
        int factors = Math.Min(WarshipForces(fleet, 1), units * 4);
        if (factors <= 0) return 0;
        fleet.Blasting = 1;
        int killed = BlastPlanet(planet, factors);
        planet.PlayerSeenCapacity = planet.Capacity;
        return killed;
    }
    private static int BlastPlanet(Planet planet, int factors) {
        int killed = Math.Min(planet.Capacity, factors / 4);
        planet.Inhabitants = Math.Min(planet.Inhabitants, planet.Capacity) - killed;
        planet.IndustrialUnits = Math.Min(planet.IndustrialUnits - killed, planet.Inhabitants * Constants.Init.UnitRatio);
        planet.Capacity -= killed;
        if (planet.Inhabitants <= 0) {
            planet.Inhabitants = planet.IndustrialUnits = planet.MissileBases = planet.AdvancedMissileBases = 0;
            if (planet.Owner != Ownership.None) {
                planet.Owner = Ownership.None; planet.EnemySeenOwner = Ownership.None; planet.Conquered = false;
            }
        }
        return killed;
    }

    private int WarshipForces(TaskForce fleet, int team) => State.Players[team].Weapons
        * (fleet.Cruisers * Constants.Guns.Cruiser + fleet.BattleStars * Constants.Guns.Battleship);
    private bool HasWarships(int team, int star) => State.Players[team].Tf.Skip(1)
        .Any(f => f.Destination == star && f.ETA == 0 && (f.Cruisers > 0 || f.BattleStars > 0));
    internal bool HasPotentialClassicCombat() => Enumerable.Range(0, State.Stars.Count).Any(star => {
        var enemy = State.Players[0].Tf.Skip(1).Where(f => f.Destination == star && f.ETA is 0 or 1).ToArray();
        var player = State.Players[1].Tf.Skip(1).Where(f => f.Destination == star && f.ETA is 0 or 1).ToArray();
        bool ew = enemy.Any(f => f.Cruisers + f.BattleStars > 0), pw = player.Any(f => f.Cruisers + f.BattleStars > 0);
        return enemy.Length > 0 && player.Length > 0 && (ew || pw)
            || ew && State.Stars[star].Planets.Any(p => p.Owner == Ownership.Player)
            || pw && State.Stars[star].Planets.Any(p => p.Owner == Ownership.Enemy);
    });

    public bool HasPendingClassicCombat => State.Battle != null || State.EnemyAttack != null || State.Attack != null;

    public void PrepareSharedStartCombat() {
        if (!State.PendingSharedStartEncounter || State.PlayerStartupStage != PlayerStartupStage.Completed)
            throw new InvalidOperationException("No completed shared-start encounter is pending.");
        if (!State.StartupCombatInProgress) {
            State.StartupCombatInProgress = true; State.CombatPrepared = false;
        }
        PrepareClassicCombat();
    }

    public void PrepareClassicCombat(bool afterInvestment = false) {
        if (!State.ClassicTurnInProgress && !State.StartupCombatInProgress) throw new InvalidOperationException("No Classic turn is in progress.");
        if (afterInvestment && State.InvestmentCycleDue && !State.PostInvestmentCombatPrepared) {
            if (!State.EconomyPrepared || State.Investment != null || State.ProductionQueue?.Count > 0)
                throw new InvalidOperationException("Finish investment before its combat pass.");
            State.PostInvestmentCombatPrepared = true; State.CombatPrepared = false;
        }
        if (State.CombatPrepared) { ContinueClassicCombat(); return; }
        State.CombatPrepared = true; State.CombatStar = State.CombatStep = 0;
        ContinueClassicCombat();
    }
    private bool CanAttackPlanet(int star) => HasWarships(1, star) && State.Stars[star].Planets.Any(p => p.Owner == Ownership.Enemy);
    public Planet ValidateSalvoPlanet(int number) {
        var attack = State.Attack ?? throw new InvalidOperationException("No player planetary attack is active.");
        if (attack.AwaitingAcknowledgement) throw new InvalidOperationException("Planet attack has concluded.");
        var planet = State.Stars[attack.StarIndex].Planets.FirstOrDefault(p => p.Number == number)
            ?? throw new InvalidOperationException("! That is not a useable planet");
        if (planet.Owner != Ownership.Enemy) throw new InvalidOperationException(" !Not an enemy colony");
        return planet;
    }
    public void ValidateSalvoFleet(int index) {
        var attack = State.Attack ?? throw new InvalidOperationException("No player planetary attack is active.");
        if (index is < 1 or > 26) throw new InvalidOperationException(" !Illegal tf");
        var f = State.Players[1].Tf[index];
        if (f.Destination == null) throw new InvalidOperationException(" !Nonexistent tf");
        if (f.Destination != attack.StarIndex || f.ETA != 0) throw new InvalidOperationException(" !Tf is not at this star");
        if (f.Cruisers + f.BattleStars == 0) throw new InvalidOperationException(" !Tf has no warships");
    }
    public PlanetarySalvoResult FirePlayerSalvo(int fleetIndex, int number) {
        var planet = ValidateSalvoPlanet(number); ValidateSalvoFleet(fleetIndex);
        var attack = State.Attack!;
        bool first = !planet.UnderAttack; planet.UnderAttack = true;
        var result = FirePlanetSalvo(1, fleetIndex, planet, first);
        if (!CanAttackPlanet(attack.StarIndex)) EndPlayerAttack();
        return result;
    }
    private PlanetarySalvoResult FirePlanetSalvo(int team, int fleetIndex, Planet planet, bool first) {
        var fleet = State.Players[team].Tf[fleetIndex]; int star = fleet.Destination!.Value;
        int force = WarshipForces(fleet, team);
        int defense = State.Players[1 - team].Weapons * (planet.MissileBases * Constants.Guns.Cruiser + planet.AdvancedMissileBases * Constants.Guns.Battleship);
        double? attackSave = null, defendSave = null;
        int oldC = fleet.Cruisers, oldB = fleet.BattleStars, oldM = planet.MissileBases, oldA = planet.AdvancedMissileBases, passes = 0;
        if (defense > 0) {
            attackSave = Math.Exp(Math.Log(0.8) * Math.Min((double)defense / force, 14.0));
            defendSave = Math.Exp(Math.Log(0.8) * Math.Min((double)force / defense, 14.0));
            bool aLoss = false, pLoss = false;
            do {
                int c = fleet.Cruisers, b = fleet.BattleStars, m = planet.MissileBases, a = planet.AdvancedMissileBases;
                fleet.Cruisers = CombatSurvivors(c, attackSave.Value);
                fleet.BattleStars = CombatSurvivors(b, attackSave.Value);
                planet.MissileBases = CombatSurvivors(m, defendSave.Value);
                planet.AdvancedMissileBases = CombatSurvivors(a, defendSave.Value);
                aLoss |= c != fleet.Cruisers || b != fleet.BattleStars;
                pLoss |= m != planet.MissileBases || a != planet.AdvancedMissileBases;
                passes++;
            } while (!first && !pLoss && aLoss); // Preserve the C asymmetry, including its frozen probabilities.
        }
        bool captured = planet.MissileBases + planet.AdvancedMissileBases == 0 && HasWarships(team, star);
        if (captured) { planet.Owner = (Ownership)team; planet.EnemySeenOwner = (Ownership)team; planet.Conquered = true; }
        State.FleetsSys.CleanupCombatFleets();
        return new(fleetIndex, planet.Number, force, defense, attackSave, defendSave, oldC - fleet.Cruisers, oldB - fleet.BattleStars,
            oldM - planet.MissileBases, oldA - planet.AdvancedMissileBases, passes, captured);
    }
    private int CombatSurvivors(int count, double survival) {
        int left = count;
        for (int i = 0; i < count; i++) if (_rng.Next(1, 101) / 100.0 > survival) left--;
        return left;
    }
    public void EndPlayerAttack() {
        var attack = State.Attack ?? throw new InvalidOperationException("No player planetary attack is active.");
        foreach (var planet in State.Stars[attack.StarIndex].Planets) planet.UnderAttack = false;
        ApplyClassicRevolt(attack.StarIndex);
        attack.AwaitingAcknowledgement = true;
    }
    public void AcknowledgePlayerAttack() {
        if (State.Attack?.AwaitingAcknowledgement != true) throw new InvalidOperationException("Planet attack has not concluded.");
        State.Attack = null; ContinueClassicCombat();
    }
    private void ApplyClassicRevolt(int star) {
        foreach (var planet in State.Stars[star].Planets) {
            if (planet.Owner == Ownership.None || !planet.Conquered || HasWarships((int)planet.Owner, star)) continue;
            planet.Owner = planet.Owner == Ownership.Enemy ? Ownership.Player : Ownership.Enemy;
            planet.Conquered = false; planet.PlayerSeenCapacity = planet.Capacity;
        }
    }
}
