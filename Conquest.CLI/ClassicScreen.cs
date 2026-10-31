using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.CLI;

/// <summary>Pure composition of the original 80x25 display, without mutating board markers.</summary>
public sealed class ClassicScreen {
    public string Cell(GameState state, int x, int y) {
        var star = state.Board[x, y].StarRef;
        char status = star == null || star.ExploredBy[1] ? ' ' : '?';
        if (star?.ExploredBy[1] == true && star.Planets.Any(p => p.Owner == Ownership.Player)) status = '@';
        if (star?.ExploredBy[1] == true && star.Planets.Any(p => p.UnderAttack)) status = '!';
        if (state.Attack is { AwaitingAcknowledgement: false } attack && ReferenceEquals(star, state.Stars[attack.StarIndex])) status = '!';
        if (state.Battle is { } battle && ReferenceEquals(star, state.Stars[battle.StarIndex])) status = '!';
        if (state.EnemyAttack is { } enemy && ReferenceEquals(star, state.Stars[enemy.StarIndex])) status = '!';
        var fleets = state.Players[1].Tf.Skip(1).Select((tf, i) => (tf, i))
            .Where(f => f.tf.Destination != null && f.tf.X == x && f.tf.Y == y
                && f.i + 1 != state.Investment?.FleetIndex).ToArray();
        char fleet = fleets.Length switch { 0 => ' ', 1 => (char)('a' + fleets[0].i), _ => '*' };
        return $"{status}{star?.Name ?? "."}{fleet}";
    }

    public string[] Render(Game game, IEnumerable<string>? report = null, string prompt = "?", string message = "",
        IEnumerable<string>? input = null, string arrivals = "", string? heading = null) {
        var rows = Enumerable.Range(0, 25).Select(_ => Enumerable.Repeat(' ', 79).ToArray()).ToArray();
        void Put(int x, int y, string text) {
            if (y < 0 || y >= rows.Length) return;
            for (int i = 0; i < text.Length && x + i < 79; i++)
                if (x + i >= 0) rows[y][x + i] = text[i];
        }
        var state = game.State;
        for (int y = 0; y < state.Config.Height; y++) {
            int row = 14 - y;
            Put(0, row, y == 0 || (y + 1) % 5 == 0 ? $"{y + 1,2}|" : "  |");
            for (int x = 0; x < state.Config.Width; x++) Put(3 + x * 3, row, Cell(state, x, y));
            Put(3 + state.Config.Width * 3, row, "|");
        }
        Put(3, 15, new string('-', state.Config.Width * 3));
        for (int x = 0; x < state.Config.Width; x++)
            if (x == 0 || (x + 1) % 5 == 0) Put(3 + x * 3, 16, $"{x + 1,2}");
        Put(0, 17, heading ?? (state.Battle != null ? ">>>>>> Taskforce Battle <<<<<<"
            : state.EnemyAttack is { } enemy ? $">>>>>> Enemy Attacks {state.Stars[enemy.StarIndex].Name}{enemy.Planet.Number} <<<<<<"
            : state.Attack is { } attack ? $">>>>>> Attack at Star {state.Stars[attack.StarIndex].Name} <<<<<<"
            : state.Investment != null ? "$$$$$$$$$ Investment $$$$$$$$$" : "---------- Movement ----------"));
        Put(0, 24, message);
        Put(31, 17, "------- Events -------");
        Put(31, 18, $"Turn:              {game.Turn + 1,3}");
        Put(31, 19, $"Prod yr:             {state.ProductionYear}");
        Put(31, 20, arrivals);
        string EnemyEvent(string label, IEnumerable<int> stars) {
            var names = string.Concat(stars.Select(i => state.Stars[i].Name));
            return names.Length == 0 ? "" : label + names.PadLeft(12);
        }
        Put(31, 21, EnemyEvent("E Arrive: ", state.VisibleEnemyArrivals));
        Put(31, 22, EnemyEvent("E Depart: ", state.VisibleEnemyDepartures));
        var p = state.Players[1];
        Put(54, 17, "------- Research ------");
        string Research(char token, int level, int progress, ResearchField field) =>
            $"{token}:{level,2}" + (ResearchRules.NextRequirement(p, field) is int need ? $" res: {progress,3} need:{need,4}" : "");
        Put(54, 18, Research('V', p.Velocity, p.VelocityResearchProgress, ResearchField.Velocity));
        Put(54, 19, Research('R', p.Range, p.RangeResearchProgress, ResearchField.Range));
        Put(54, 20, Research('W', p.Weapons, p.WeaponsResearchProgress, ResearchField.Weapons));
        // C echoes input over the existing screen, including long selector lines.
        Put(0, 18, prompt);
        int inputRow = 19;
        foreach (var text in input ?? []) {
            Put(0, inputRow++, text);
            if (inputRow == 24) break;
        }
        int line = 0;
        foreach (var text in report ?? []) { Put(49, line++, text); if (line == (state.Investment != null ? 18 : 17)) break; }
        return rows.Select(row => new string(row)).ToArray();
    }
}
