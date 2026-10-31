using System.Globalization;
using System.Text;
using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.CLI;

internal sealed class ClassicStartupFlow {
    private readonly IGameIO _io;
    internal ClassicStartupFlow(IGameIO io) { _io = io; }

    internal bool Run(Game game) {
        _io.WriteLine("Classic startup");
        WriteSelectionMap(game.State);
        game.BeginClassicPlayerStartup();
        while (game.State.PlayerStartupStage == PlayerStartupStage.SelectingStar) {
            _io.WriteLine("Start at star (letter; quit exits):");
            var choice = _io.ReadLine();
            // Q is a valid star letter; only the full word exits this prompt.
            if (choice == null || choice.Trim().Equals("quit", StringComparison.OrdinalIgnoreCase)) return false;
            int index = game.State.Stars.FindIndex(s => s.Name.Equals(choice.Trim(), StringComparison.OrdinalIgnoreCase));
            if (index < 0) {
                _io.WriteLine("Invalid starting star. Enter a letter shown on the map.");
                continue;
            }
            game.SelectClassicPlayerStartingStar(index);
        }
        _io.WriteLine($"Starting star: {game.State.Stars[game.State.PlayerStartingStar!.Value].Name}");
        WritePurchaseHelp();
        WriteStatus(game);
        while (game.State.PlayerStartupBalance > 0) {
            _io.WriteLine("Purchases:");
            var line = _io.ReadLine();
            if (line == null || IsQuit(line)) return false;
            if (line.Trim().Equals("help", StringComparison.OrdinalIgnoreCase) || line.Trim().Equals("H", StringComparison.OrdinalIgnoreCase))
                WritePurchaseHelp();
            else ApplyPurchases(game, line);
            WriteStatus(game);
        }
        game.FinalizeClassicPlayerStartup();
        _io.WriteLine($"Classic startup complete at star {game.State.Stars[game.State.PlayerStartingStar.Value].Name}.");
        WriteStatus(game);
        if (game.State.PendingSharedStartEncounter)
            _io.WriteLine("Shared-start encounter pending. Classic gameplay resolves combat before movement.");
        return true;
    }

    private static bool IsQuit(string text) => text.Trim().Equals("quit", StringComparison.OrdinalIgnoreCase)
        || text.Trim().Equals("q", StringComparison.OrdinalIgnoreCase);

    private void WritePurchaseHelp() => _io.WriteLine(
        "C cruiser | S scout | B battlestar | W weapons research | V velocity research | R range research. " +
        "Put quantities before tokens: 2S, 16R, C2S2R. Spend all money to finish. H/help shows this help; quit exits.");

    private void ApplyPurchases(Game game, string line) {
        int index = 0;
        while (index < line.Length) {
            while (index < line.Length && char.IsWhiteSpace(line[index])) index++;
            if (index == line.Length) break;
            int start = index;
            if (line[index] == '-') index++;
            while (index < line.Length && char.IsAsciiDigit(line[index])) index++;
            bool hasQuantity = index > start;
            if (index == line.Length || char.IsWhiteSpace(line[index])) {
                _io.WriteLine("Invalid purchase: a quantity must be immediately followed by C, S, B, W, V or R.");
                break;
            }
            int quantity = 1;
            bool validQuantity = !hasQuantity || int.TryParse(line.AsSpan(start, index - start),
                NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out quantity);
            char token = char.ToUpperInvariant(line[index++]);
            if (!validQuantity) {
                _io.WriteLine("Invalid purchase quantity.");
                continue;
            }
            try {
                bool purchased;
                switch (token) {
                    case 'C': purchased = game.PurchaseClassicStartupShips(StartupShip.Cruiser, quantity); break;
                    case 'S': purchased = game.PurchaseClassicStartupShips(StartupShip.Scout, quantity); break;
                    case 'B': purchased = game.PurchaseClassicStartupShips(StartupShip.Battlestar, quantity); break;
                    case 'W': purchased = game.InvestClassicStartupResearch(ResearchField.Weapons, quantity); break;
                    case 'V': purchased = game.InvestClassicStartupResearch(ResearchField.Velocity, quantity); break;
                    case 'R': purchased = game.InvestClassicStartupResearch(ResearchField.Range, quantity); break;
                    case 'H': WritePurchaseHelp(); continue;
                    default: _io.WriteLine($"Invalid purchase token: {token}."); continue;
                }
                if (!purchased) _io.WriteLine($"Insufficient funds for {quantity}{token}.");
            }
            catch (ArgumentException) { _io.WriteLine($"Invalid purchase: {quantity}{token}."); }
            catch (OverflowException) { _io.WriteLine("Invalid purchase quantity: too large."); }
        }
    }

    private void WriteStatus(Game game) {
        var player = game.State.Players[1];
        var fleet = player.Tf[1];
        _io.WriteLine($"Fleet 1: transports={fleet.Transports} cruisers={fleet.Cruisers} scouts={fleet.Scouts} battlestars={fleet.BattleStars}");
        _io.WriteLine($"Weapons: {player.Weapons} (research {player.WeaponsResearchProgress}) | " +
            $"Velocity: {player.Velocity} (research {player.VelocityResearchProgress}) | " +
            $"Range: {player.Range} (research {player.RangeResearchProgress})");
        _io.WriteLine($"Startup money remaining: {game.State.PlayerStartupBalance}");
    }

    // Selection exposes only public star letters/coordinates, never fleet or planet data.
    private void WriteSelectionMap(GameState state) {
        _io.WriteLine("Starting-star map (zero-based coordinates):");
        _io.WriteLine("   " + string.Concat(Enumerable.Range(0, state.Config.Width).Select(x => $"{x,3}")));
        for (int y = state.Config.Height - 1; y >= 0; y--) {
            var row = new StringBuilder($"{y,2} ");
            for (int x = 0; x < state.Config.Width; x++)
                row.Append($"{state.Board[x, y].StarRef?.Name ?? ".",3}");
            _io.WriteLine(row.ToString());
        }
    }
}
