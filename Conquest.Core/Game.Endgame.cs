using Conquest.Core.Model;

namespace Conquest.Core;

public partial class Game {
    /// <summary>update.c:check_game_over; called after up_year, or confirmed quit without up_year.</summary>
    public ClassicGameResult? CheckClassicGameOver(bool quit = false) {
        if (State.Result != null) return State.Result;
        if (State.ClassicTurnInProgress || HasPendingClassicCombat || State.PendingSharedStartEncounter
            || State.PlayerStartupStage is not (PlayerStartupStage.NotStarted or PlayerStartupStage.Completed))
            throw new InvalidOperationException("Finish startup and the pending Classic phase before checking the game result.");
        if (!quit && State.EnemyProcessingFailure != null) throw new InvalidOperationException(State.EnemyProcessingFailure);
        try {
            ClassicScore Score(int team) {
                int transports = 0, industry = 0;
                foreach (var fleet in State.Players[team].Tf.Skip(1))
                    if (fleet.Destination != null) transports = checked(transports + fleet.Transports);
                foreach (var planet in State.Stars.SelectMany(s => s.Planets))
                    if (planet.Owner == (Ownership)team) industry = checked(industry + planet.IndustrialUnits);
                return new ClassicScore(transports, industry);
            }
            var player = Score(1); var enemy = Score(0);
            int p = player.Total, e = enemy.Total, turn = checked(Turn + 1);
            var reasons = quit ? ClassicEndReason.Quit : ClassicEndReason.None;
            if (p == 0) reasons |= ClassicEndReason.PlayerEliminated;
            if (e == 0) reasons |= ClassicEndReason.EnemyEliminated;
            if (p != 0 && e != 0 && turn >= 40) {
                // Both C operands are int: truncate the ratio before comparing to double overwhelm.
                if (p / e >= State.Config.OverwhelmFactor) reasons |= ClassicEndReason.EnemyOverwhelmed;
                if (e / p >= State.Config.OverwhelmFactor) reasons |= ClassicEndReason.PlayerOverwhelmed;
            }
            if (turn > State.Config.MaxTurns) reasons |= ClassicEndReason.TurnLimit;
            if (reasons == ClassicEndReason.None) return null;
            int VictoryThreshold(int score) {
                double threshold = score * State.Config.VictoryMultiplier;
                if (!double.IsFinite(threshold) || threshold < int.MinValue || threshold > int.MaxValue)
                    throw new InvalidOperationException("Classic final-score conversion is undefined in C for these values.");
                return (int)threshold;
            }
            // Preserve strict comparisons and enemy-first precedence, including on voluntary quit.
            var winner = e > VictoryThreshold(p) ? ClassicWinner.Enemy
                : p > VictoryThreshold(e) ? ClassicWinner.Player : ClassicWinner.Draw;
            State.Result = new ClassicGameResult(turn, player, enemy, reasons, winner);
            return State.Result;
        }
        catch (OverflowException ex) {
            throw new InvalidOperationException("Classic score arithmetic overflow is undefined in C.", ex);
        }
    }
}
