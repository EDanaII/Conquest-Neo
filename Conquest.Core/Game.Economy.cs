using Conquest.Core.Model;

namespace Conquest.Core;

public partial class Game {
    /// <summary>Begin a Classic phase; reports/arrival acknowledgements precede investment and up_year.</summary>
    public void BeginClassicTurn() {
        State.EnsureStartupAllowsAdvancement();
        if (Turn >= State.Config.MaxTurns) {
            CheckClassicGameOver();
            throw new InvalidOperationException("The Classic game is over.");
        }
        bool due = State.ProductionYear == 4 && Turn + 1 < State.Config.MaxTurns;
        if (due && !HasPotentialClassicCombat()) ValidateProductionBoundary();
        State.EnemyArrivals.Clear(); State.EnemyDepartures.Clear();
        // main.c: inputplayer -> inputmach -> move_ships -> battle -> invest.
        if (State.ClassicEnemyInitializationCompleted) {
            State.HasAdvancedTurns = true;
            IssueClassicEnemyOrders();
        }
        State.FleetsSys.Tick(_rng);
        State.ClassicTurnInProgress = true;
        State.InvestmentCycleDue = due;
        State.EconomyPrepared = false;
        State.CombatPrepared = State.PostInvestmentCombatPrepared = false;
        // movement.c calls revolt after movement, before battle/investment.
        for (int star = 0; star < State.Stars.Count; star++) ApplyClassicRevolt(star);
    }

    private void ValidateProductionBoundary() {
        var planets = State.Stars.SelectMany(s => s.Planets).ToArray();
        if (planets.Any(p => p.Owner == Ownership.Enemy) && State.EnemyResearchField == null)
            throw new InvalidOperationException("Enemy investment requires initialized enemy research.");
        if (planets.Any(p => p.Owner != Ownership.None && p.Capacity <= 0))
            throw new InvalidOperationException("Owned zero/negative-capacity growth is undefined in C; production cannot proceed.");
        if (planets.Any(p => p.Owner == Ownership.Player) && State.FleetsSys.FirstFreePlayerSlot() < 1)
            throw new InvalidOperationException("Classic investment needs a free task-force slot; join fleets before continuing.");
    }

    /// <summary>update.c:invest: one whole growth pass, then ordered player colony sessions.</summary>
    public void PrepareClassicEconomy() {
        if (!State.ClassicTurnInProgress) throw new InvalidOperationException("No Classic turn is in progress.");
        PrepareClassicCombat();
        if (HasPendingClassicCombat) throw new InvalidOperationException("Resolve combat before investment.");
        if (!State.EconomyPrepared) {
            if (State.InvestmentCycleDue) {
                ValidateProductionBoundary();
                State.ProductionYear = 0;
                foreach (var star in State.Stars) foreach (var planet in star.Planets) {
                    if (planet.EnemySeenOwner == Ownership.Player && planet.Capacity > 10 && planet.EnemySeenDefense < 12)
                        planet.EnemySeenDefense++;
                    if (planet.Owner == Ownership.None) continue;
                    // Preserve C integer division, signed round(), then integer conquered halving.
                    double value = planet.Inhabitants * State.Players[(int)planet.Owner].PopulationGrowthRate
                        * (1 - planet.Inhabitants / planet.Capacity);
                    int newborn = (int)(value < 0 ? value - 0.5 : value + 0.5);
                    if (planet.Conquered) newborn /= 2;
                    planet.Inhabitants += newborn; planet.IndustrialUnits += newborn;
                }
                State.ProductionQueue = new Queue<(int, Planet)>(State.Stars.SelectMany((s, index) =>
                    s.Planets.Where(p => p.Owner != Ownership.None).Select(p => (index, p))));
            }
            State.EconomyPrepared = true;
        }
        OpenNextInvestment();
    }

    private void OpenNextInvestment() {
        if (State.EnemyProcessingFailure != null) throw new InvalidOperationException(State.EnemyProcessingFailure);
        if (State.Investment != null || State.ProductionQueue == null) return;
        while (State.ProductionQueue.Count > 0) {
            var (star, planet) = State.ProductionQueue.Peek();
            if (planet.Owner == Ownership.Enemy) {
                try { InvestEnemyColony(star, planet); }
                catch (InvalidOperationException ex) {
                    State.EnemyProcessingFailure = "Enemy investment stopped: " + ex.Message + " State retained; spending will not be replayed.";
                    throw new InvalidOperationException(State.EnemyProcessingFailure, ex);
                }
                State.ProductionQueue.Dequeue(); continue;
            }
            if (planet.Owner == Ownership.None) { State.ProductionQueue.Dequeue(); continue; }
            int slot = State.FleetsSys.AllocatePlayerFleetAtStar(star);
            State.ProductionQueue.Dequeue();
            State.Investment = new ColonyInvestment(star, planet, slot); return;
        }
    }

    // Called after ALL tokens in a submitted line, including tokens after budget exhaustion.
    public void FinishClassicInvestmentLine() {
        var session = State.Investment ?? throw new InvalidOperationException("No colony investment is active.");
        if (session.Balance > 0) return;
        State.FleetsSys.CompleteProductionFleet(session.FleetIndex, session.StarIndex);
        State.Investment = null;
        OpenNextInvestment();
    }

    public void CompleteClassicTurn() {
        if (State.ClassicTurnInProgress && State.EconomyPrepared && State.Investment == null && !(State.ProductionQueue?.Count > 0))
            PrepareClassicCombat(afterInvestment: true);
        if (!State.ClassicTurnInProgress || !State.EconomyPrepared || State.Investment != null
            || State.ProductionQueue?.Count > 0 || HasPendingClassicCombat)
            throw new InvalidOperationException("Finish all colony investment before completing this Classic turn.");
        State.CompletedTurns++; State.ProductionYear++;
        State.ClassicTurnInProgress = false; State.ProductionQueue = null;
    }

    /// <summary>command.c:inv_player. Returns the original error, with no charge for rejected tokens.</summary>
    public string? InvestInClassicColony(char key, int amount = 1, bool maximum = false) {
        var session = State.Investment ?? throw new InvalidOperationException("No colony investment is active.");
        if (amount < 0) return " !Invalid investment quantity";
        var p = session.Planet; var fleet = State.Players[1].Tf[session.FleetIndex];
        int balance = session.Balance;
        int unit = key switch {
            'A' => Constants.Cost.AdvMissileBase, 'B' => Constants.Cost.Battleship,
            'C' => Constants.Cost.Cruiser, 'S' => Constants.Cost.Scout,
            'M' => Constants.Cost.MissileBase, 'I' => Constants.Cost.Infrastructure, _ => 1
        };
        if (key is 'A' or 'M' or '-') {
            if (p.Inhabitants == 0) return "  !abandoned planet";
            if (p.Conquered) return key == '-' ? " !No defense on conquered colony"
                : key == 'M' ? " !No Mb  on conquered colony" : maximum ? " !No AMb  on conquered colony" : " !No amb  on conquered colony";
        }
        if (key == 'T' && p.Conquered) return "!No transports on conqered col";
        if (maximum) {
            if (key == 'T') amount = Math.Min(p.Inhabitants, balance);
            else if (key == 'I') {
                int room = p.Inhabitants * Constants.Init.UnitRatio - p.IndustrialUnits;
                if (room <= 0) return "!Can't support that many IU's.";
                amount = Math.Max(1, Math.Min(balance / unit, room));
            }
            else if (key is 'R' or 'W' or 'V') {
                var field = Field(key); var player = State.Players[1];
                int progress = key == 'R' ? player.RangeResearchProgress : key == 'V' ? player.VelocityResearchProgress : player.WeaponsResearchProgress;
                amount = ResearchRules.NextRequirement(player, field) is int need ? Math.Min(balance, need - progress) : 0;
            }
            else if (key is 'A' or 'B' or 'C' or 'M' or 'S') amount = Math.Max(1, balance / unit);
            else return key == ' ' ? null : $"Invalid investment field {key}";
        }
        if (key == 'I' && (long)p.IndustrialUnits + amount > (long)p.Inhabitants * Constants.Init.UnitRatio)
            return " !Can't support that many iu's";
        long wideCost = (long)amount * unit;
        if (wideCost > int.MaxValue) return " !Invalid investment quantity"; // C overflow is undefined.
        int cost = (int)wideCost;
        if (key is '+' or '-') {
            cost = 0;
            if (key == '+') {
                int battles = balance / Constants.Cost.Battleship;
                int cruisers = (balance - battles * Constants.Cost.Battleship) / Constants.Cost.Cruiser;
                fleet.BattleStars += battles; fleet.Cruisers += cruisers;
                cost = battles * Constants.Cost.Battleship + cruisers * Constants.Cost.Cruiser;
            }
            else {
                int advanced = balance / Constants.Cost.AdvMissileBase;
                int missiles = (balance - advanced * Constants.Cost.AdvMissileBase) / Constants.Cost.MissileBase;
                p.AdvancedMissileBases += advanced; p.MissileBases += missiles;
                cost = advanced * Constants.Cost.AdvMissileBase + missiles * Constants.Cost.MissileBase;
            }
        }
        else if (key is 'A' or 'B' or 'C' or 'S' or 'M' or 'I' or 'T' or 'R' or 'V' or 'W') {
            if (cost > balance) return $" !can't affort {amount,3}{key}";
            switch (key) {
                case 'A': p.AdvancedMissileBases += amount; break;
                case 'M': p.MissileBases += amount; break;
                case 'B': fleet.BattleStars += amount; break;
                case 'C': fleet.Cruisers += amount; break;
                case 'S': fleet.Scouts += amount; break;
                case 'I': p.IndustrialUnits += amount; break;
                case 'T':
                    if (amount > p.Inhabitants) return " ! Not enough people for ( trans";
                    fleet.Transports += amount; p.Inhabitants -= amount;
                    p.IndustrialUnits = Math.Min(p.IndustrialUnits - amount, p.Inhabitants * Constants.Init.UnitRatio);
                    if (p.Inhabitants == 0) { p.Owner = Ownership.None; p.MissileBases = p.AdvancedMissileBases = p.IndustrialUnits = 0; }
                    break;
                case 'R': case 'V': case 'W': ResearchRules.ApplyPlayerInvestment(State.Players[1], Field(key), amount); break;
            }
        }
        else return $" !Illegal field {key}";
        session.Balance -= cost;
        return null;
    }

    private static ResearchField Field(char key) => key switch {
        'R' => ResearchField.Range, 'V' => ResearchField.Velocity, _ => ResearchField.Weapons
    };
}
