using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.CLI;

/// <summary>init.c:init_player purchase screen; Core owns all costs and effects.</summary>
public sealed class ClassicStartupPurchases(Game game, IClassicTerminal terminal) {
    private readonly ClassicScreen _screen = new();
    private string[] _help = [];
    private string _message = "";

    private void Draw(string prompt) {
        var fleet = game.State.Players[1].Tf[1];
        var star = game.State.Stars[game.State.PlayerStartingStar!.Value];
        string Ships(int count, char kind) => count > 0 ? $"{count,2}{kind}" : "   ";
        string inventory = $"TFa:{star.Name}({fleet.X + 1,2},{fleet.Y + 1,2})"
            + Ships(fleet.Transports, 't') + Ships(fleet.Scouts, 's') + Ships(fleet.Cruisers, 'c') + Ships(fleet.BattleStars, 'b');
        var rows = _screen.Render(game, _help, prompt, _message,
            [inventory, $"you have {game.State.Config.StartingTransports} transports",
                $" && {game.State.Config.StartingMoney} units to spend", "on ships or research."],
            heading: "******* Initialization *******");
        // C puts 'a' on the map only AFTER purchasing/finalization. The shared
        // gameplay renderer remains unchanged; suppress this startup-only glyph.
        int row = 14 - star.Y, column = 5 + 3 * star.X;
        rows[row] = rows[row][..column] + " " + rows[row][(column + 1)..];
        terminal.Present(rows);
        terminal.MoveCursor(Math.Min(prompt.Length, 78), 18);
    }

    private int Key() {
        while (true) {
            int key = terminal.ReadKey();
            if (key < 0) return -1;
            // Console.ReadKey maps Enter to LF. Ignore the CR of redirected
            // CRLF so no leftover LF leaks into gameplay after the last line.
            if (key == '\r') continue;
            return char.ToUpperInvariant((char)key);
        }
    }

    private string? Line(string prompt) {
        string text = "";
        while (true) {
            Draw(prompt + text);
            int key = Key();
            if (key < 0) return null;
            if (key == '\n') return text;
            if (key is '\b' or 127) { if (text.Length > 0) text = text[..^1]; }
            else if (!char.IsControl((char)key)) {
                text += (char)key;
                if (text.Length == 25) { Draw(prompt + text); return text; }
            }
        }
    }

    public bool Run() {
        if (game.State.PlayerStartupStage != PlayerStartupStage.Spending)
            throw new InvalidOperationException("Select a starting star before purchasing.");
        Draw($"{game.State.PlayerStartupBalance,3}? ");
        while (game.State.PlayerStartupBalance > 0) {
            var line = Line($"{game.State.PlayerStartupBalance,3}? ");
            if (line == null) return false;
            _message = "";
            int index = 0;
            while (index < line.Length) {
                while (index < line.Length && char.IsWhiteSpace(line[index])) index++;
                if (index == line.Length) break;
                int start = index;
                while (index < line.Length && char.IsAsciiDigit(line[index])) index++;
                int amount = 1;
                bool valid = start == index || int.TryParse(line.AsSpan(start, index - start), out amount);
                if (index == line.Length) break; // get_token returns space after a bare quantity.
                char key = line[index++];
                if (key == ' ') break;
                if (!valid) { _message = " !Invalid purchase quantity"; continue; }
                if (key == 'H') ShowHelp();
                else if (key == '>') {
                    Draw(">?"); int report = Key();
                    if (report < 0) return false;
                    if (report == 'M') { _help = []; terminal.Clear(); }
                    else if (report == 'R') { if (Line(" Research field(s):") == null) return false; }
                    else _message = " !Only M,R during initialize";
                }
                else {
                    try {
                        bool bought;
                        switch (key) {
                            case 'C': bought = game.PurchaseClassicStartupShips(StartupShip.Cruiser, amount); break;
                            case 'S': bought = game.PurchaseClassicStartupShips(StartupShip.Scout, amount); break;
                            case 'B': bought = game.PurchaseClassicStartupShips(StartupShip.Battlestar, amount); break;
                            case 'W': bought = game.InvestClassicStartupResearch(ResearchField.Weapons, amount); break;
                            case 'V': bought = game.InvestClassicStartupResearch(ResearchField.Velocity, amount); break;
                            case 'R': bought = game.InvestClassicStartupResearch(ResearchField.Range, amount); break;
                            default: _message = $" !Illegal field {key}"; continue;
                        }
                        if (!bought) _message = $"  !can't afford {amount,3}{key}";
                    }
                    catch (OverflowException) { _message = " !Invalid purchase quantity"; }
                    catch (ArgumentException) { _message = " !Invalid purchase quantity"; }
                }
                Draw($"{game.State.PlayerStartupBalance,3}? ");
            }
        }
        return true;
    }

    private void ShowHelp() => _help = new[] { ("B", "Bld Battlestar(s)    75"), ("C", "Bld Cruiser(s)       16"),
        ("H", "Help"), ("R", "Range Research"), ("S", "Bld Scout(s)          6"), ("V", "Velocity Research"),
        ("W", "Weapons Research"), (">M", "Redraw Map"), (">R", "Research summary") }
        .Select(h => $"{h.Item1,2} - {h.Item2,-25}").ToArray();
}
