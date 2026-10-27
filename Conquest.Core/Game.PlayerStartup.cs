using Conquest.Core.Model;

namespace Conquest.Core;

public partial class Game {
    public void BeginClassicPlayerStartup() {
        if (!State.ClassicEnemyInitializationCompleted
            || State.PlayerStartupStage != PlayerStartupStage.NotStarted || State.HasAdvancedTurns)
            throw new InvalidOperationException("Player startup requires completed enemy initialization and can begin only once before advancement.");
        if (State.Config.StartingMoney < 0 || State.Config.RequestedStartingCapacity < 0
            || State.Config.StartingTransports < 0)
            throw new ArgumentOutOfRangeException(nameof(State.Config), "Classic startup requires nonnegative money, capacity, and transports.");
        ValidatePlayerStartupGalaxy();
        ValidateUnplacedPlayerStartupFleets();

        State.PlayerStartupBalance = State.Config.StartingMoney;
        State.PlayerStartupStage = PlayerStartupStage.SelectingStar;
        if (State.Config.RequestedStartingCapacity > 0) {
            State.PlayerStartupStage = PlayerStartupStage.SelectingAutomatically;
            try {
                PlaceClassicPlayerAtStar(PlayerStartupRules.SelectByCapacity(
                    State.Stars, State.Config.RequestedStartingCapacity, _rng));
            }
            catch {
                State.PlayerStartupStage = PlayerStartupStage.Failed;
                throw;
            }
        }
    }

    public void SelectClassicPlayerStartingStar(int starIndex) {
        RequirePlayerStartupStage(PlayerStartupStage.SelectingStar);
        if ((uint)starIndex >= (uint)State.Stars.Count)
            throw new ArgumentOutOfRangeException(nameof(starIndex));
        ValidatePlayerStartupGalaxy();
        ValidateUnplacedPlayerStartupFleets();
        PlaceClassicPlayerAtStar(starIndex);
    }

    // False means insufficient funds; malformed requests throw before mutation.
    public bool PurchaseClassicStartupShips(StartupShip ship, int quantity = 1) {
        RequireSpendingLocation();
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        int cost = checked(PlayerStartupRules.ShipCost(ship) * quantity);
        var fleet = State.Players[1].Tf[1];
        int current = ship switch {
            StartupShip.Cruiser => fleet.Cruisers,
            StartupShip.Scout => fleet.Scouts,
            StartupShip.Battlestar => fleet.BattleStars,
            _ => throw new ArgumentOutOfRangeException(nameof(ship))
        };
        int total = checked(current + quantity);
        if (cost > State.PlayerStartupBalance) return false;
        switch (ship) {
            case StartupShip.Cruiser: fleet.Cruisers = total; break;
            case StartupShip.Scout: fleet.Scouts = total; break;
            case StartupShip.Battlestar: fleet.BattleStars = total; break;
        }
        State.PlayerStartupBalance -= cost;
        return true;
    }

    public bool InvestClassicStartupResearch(ResearchField field, int amount) {
        RequireSpendingLocation();
        if (!Enum.IsDefined(field)) throw new ArgumentOutOfRangeException(nameof(field));
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (amount > State.PlayerStartupBalance) return false;
        ResearchRules.ApplyPlayerInvestment(State.Players[1], field, amount);
        State.PlayerStartupBalance -= amount;
        return true;
    }

    public void FinalizeClassicPlayerStartup() {
        RequireSpendingLocation();
        if (State.PlayerStartupBalance != 0)
            throw new InvalidOperationException("The initial budget must be spent before finalization.");
        int index = State.PlayerStartingStar!.Value;
        var star = State.Stars[index];
        star.ExploredBy[1] = true;
        var cell = State.Board[star.X, star.Y];
        cell.Tf = 1; // Existing sector indices use fleet slots, not the C display character 'a'.
        cell.Status = ' ';
        State.PendingSharedStartEncounter = State.Players[0].Tf[1].Destination == index;
        State.PlayerStartupStage = PlayerStartupStage.Completed;
    }

    private void PlaceClassicPlayerAtStar(int index) {
        var star = State.Stars[index];
        var fleet = State.Players[1].Tf[1];
        fleet.Destination = index;
        fleet.X = star.X;
        fleet.Y = star.Y;
        fleet.ETA = fleet.EtaOriginal = 0;
        State.Board[star.X, star.Y].TaskForces[1] = fleet;
        State.PlayerStartingStar = index;
        State.PlayerStartupStage = PlayerStartupStage.Spending;
    }

    private void ValidateUnplacedPlayerStartupFleets() {
        if (State.Players[1].Tf.Skip(1).Select((tf, slot) => tf == null || tf.X != 0 || tf.Y != 0
            || tf.Destination != null || tf.ETA != 0 || tf.EtaOriginal != 0
            || tf.Scouts != 0 || tf.Cruisers != 0 || tf.BattleStars != 0
            || tf.Blasting != 0 || tf.Withdrew != 0
            || tf.Transports != (slot == 0 ? State.Config.StartingTransports : 0)).Any(used => used)
            || State.Board.Cast<Sector>().Any(c => c.TaskForces[1] != null || c.Tf != -1))
            throw new InvalidOperationException("Player startup requires unplaced starting fleets.");
    }

    private void RequirePlayerStartupStage(PlayerStartupStage stage) {
        if (State.PlayerStartupStage != stage || State.HasAdvancedTurns)
            throw new InvalidOperationException($"This operation requires player startup stage {stage} before advancement.");
    }

    private void RequireSpendingLocation() {
        RequirePlayerStartupStage(PlayerStartupStage.Spending);
        ValidatePlayerStartupGalaxy();
        if (State.PlayerStartingStar is not int index || (uint)index >= (uint)State.Stars.Count)
            throw new InvalidOperationException("Player startup has no valid selected star.");
        var star = State.Stars[index];
        var fleet = State.Players[1].Tf[1];
        if (fleet.Destination != index || fleet.X != star.X || fleet.Y != star.Y
            || fleet.ETA != 0 || fleet.EtaOriginal != 0
            || !ReferenceEquals(State.Board[star.X, star.Y].TaskForces[1], fleet))
            throw new InvalidOperationException("Player startup location was changed outside the session.");
    }

    private void ValidatePlayerStartupGalaxy() {
        if (State.Stars.Count == 0 || State.Stars.Count != State.Config.NumStars
            || State.Stars.Where((s, i) => (uint)s.X >= (uint)State.Board.GetLength(0)
                || (uint)s.Y >= (uint)State.Board.GetLength(1)
                || !ReferenceEquals(State.Board[s.X, s.Y].StarRef, s)
                || State.Board[s.X, s.Y].Star != i).Any()
            || State.Board.Cast<Sector>().Any(c => c.TaskForces.Length != 2))
            throw new InvalidOperationException("Player startup requires a valid generated galaxy.");
        var enemy = State.Players[0].Tf[1];
        if (enemy.Destination is not int index || (uint)index >= (uint)State.Stars.Count
            || enemy.ETA != 0 || enemy.EtaOriginal != 0
            || enemy.X != State.Stars[index].X || enemy.Y != State.Stars[index].Y
            || !ReferenceEquals(State.Board[enemy.X, enemy.Y].TaskForces[0], enemy))
            throw new InvalidOperationException("Enemy starting location was changed outside initialization.");
    }
}
