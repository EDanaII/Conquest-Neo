using Conquest.Core;
using Conquest.Core.Model;
using System.Globalization;

namespace Conquest.CLI;

public sealed class ClassicGameLoop(Game game, IClassicTerminal terminal) {
    private readonly ClassicScreen _screen = new();
    private string[] _report = [];
    private string _message = "H - Help";
    private string[] _input = [];
    private string _arrivals = "";
    private bool _skipLineFeed;
    private string? _heading;
    private void Draw(string prompt = "?", int cursorColumn = -1, int cursorRow = 18) {
        terminal.Present(_screen.Render(game, _report, prompt, _message, _input, _arrivals, _heading));
        terminal.MoveCursor(cursorColumn < 0 ? Math.Min(prompt.Length, 78) : cursorColumn, cursorRow);
    }
    private int Key() {
        while (true) {
            int key = terminal.ReadKey();
            if (key < 0) return -1;
            if (_skipLineFeed) { _skipLineFeed = false; if (key == '\n') continue; }
            if (key == '\r') { _skipLineFeed = true; return '\n'; }
            return char.ToUpperInvariant((char)key);
        }
    }
    private string? Line(string prompt, bool nextRow = false, string? lowerPrompt = null, int lowerRow = 19) {
        string text = "";
        string? prefix = nextRow ? ":" : lowerPrompt;
        void Echo() {
            if (prefix != null) {
                _input = [.. _input.Take(lowerRow - 19), prefix + text];
                Draw(prompt, prefix.Length + text.Length, lowerRow);
            }
            else Draw(prompt + text);
        }
        while (true) {
            Echo();
            int key = Key();
            if (key < 0) return null;
            if (key == '\n') return text;
            if (key is '\b' or 127) { if (text.Length > 0) text = text[..^1]; }
            else if (!char.IsControl((char)key)) {
                text += (char)key;
                // input.c:get_line returns immediately at 25, without consuming Enter.
                if (text.Length == 25) {
                    Echo();
                    return text;
                }
            }
        }
    }
    public int Run() {
        if (game.State.Result is { } finished) { ShowGameResult(finished); return 0; }
        if (game.State.PlayerStartingStar is int start) _report = Stars([start]).ToArray();
        if (game.State.PendingSharedStartEncounter) {
            game.PrepareSharedStartCombat();
            if (!ResolveClassicCombat()) return 0;
        }
        if (game.State.ClassicTurnInProgress) {
            try { if (!FinishEconomicTurn()) return 0; }
            catch (InvalidOperationException ex) { _message = ex.Message; }
        }
        while (true) {
            _heading = null;
            Draw();
            int key = Key();
            if (key < 0) return 0;
            _message = "";
            _input = [];
            try {
                switch (key) {
                    case 'Q':
                        terminal.Clear();
                        var quitScreen = Enumerable.Repeat(new string(' ', 79), 25).ToArray();
                        quitScreen[0] = "Quit game....[verify]".PadRight(79);
                        terminal.Present(quitScreen); terminal.MoveCursor(0, 1);
                        int verification = Key();
                        if (verification == -1) return 0;
                        if (verification == 'Y') {
                            if (game.State.PlayerStartupStage == PlayerStartupStage.Completed
                                && !game.State.ClassicTurnInProgress && !game.HasPendingClassicCombat)
                                ShowGameResult(game.CheckClassicGameOver(quit: true)!);
                            return 0;
                        }
                        RedrawMap();
                        // inputplayer sets fin before quit(), even when verification is declined.
                        if (!AdvanceMovement()) return 0;
                        break;
                    case 'D': if (!Destination()) return 0; break;
                    case 'G': case ' ': if (!AdvanceMovement()) return 0; break;
                    case 'M': RedrawMap(); break;
                    case 'H':
                        _report = new[] { ("B", "Blast Planet"), ("C", "Colony summary"), ("D", "TaskForce Destination"),
                            ("G", "Go on (done)"), ("H", "Help"), ("J", "Join TaskForces"), ("L", "Land transports"),
                            ("M", "Redraw Map"), ("N", "New TaskForce"), ("Q", "Quit"), ("R", "Research summary"),
                            ("S", "Star summary"), ("T", "TaskForce summary") }
                            .Select(h => $"{h.Item1,2} - {h.Item2,-25}").ToArray();
                        _message = "";
                        break;
                    case 'S': case 'T':
                        if (!Summarize(key == 'S')) return 0;
                        break;
                    case 'C':
                        if (!ShowReports(Colonies().Select(row => new[] { row }.AsEnumerable()))) return 0;
                        break;
                    case 'R':
                        if (Line(" Research field(s):") == null) return 0;
                        // print_res updates the persistent research pane, not the report pane.
                        break;
                    case 'N': if (!NewFleet()) return 0; break;
                    case 'J': if (!JoinFleets()) return 0; break;
                    case 'L': if (!Land()) return 0; break;
                    case 'B': if (!Bombard()) return 0; break;
                    case '?': break;
                    default: _message = "  !illegal command"; break;
                }
            }
            catch (InvalidOperationException ex) { _message = ex.Message; }
        }
    }

    private void ShowGameResult(ClassicGameResult result) {
        terminal.Clear();
        var rows = Enumerable.Repeat(new string(' ', 79), 25).ToArray();
        void Put(int row, string text) => rows[row] = text.PadRight(79)[..79];
        string Score(string label, ClassicScore score) => $"{label}Population in transports:{score.Transports,3}"
            + $"  IU's on colonies:{score.IndustrialUnits,3}  TOTAL:{score.Total,3}";
        Put(0, "*** Game over ***"); Put(1, Score("Player: ", result.Player)); Put(3, Score("Enemy:  ", result.Enemy));
        Put(4, result.Winner switch {
            ClassicWinner.Enemy => "**** THE ENEMY HAS CONQUERED THE GALAXY ***",
            ClassicWinner.Player => "*** PLAYER WINS - YOU HAVE SAVED THE GALAXY! ***",
            _ => "*** DRAWN ***"
        });
        terminal.Present(rows); terminal.MoveCursor(0, 5); Key();
    }

    private bool Summarize(bool stars) {
        var input = Line(stars ? " Star summary:" : " Tf summary :", nextRow: stars);
        if (input == null) return false;
        _input = [];
        var ids = Tokens(input).Select(c => stars ? c - 'A' : c - 'A' + 1).ToArray();
        var blocks = stars
            ? (ids.Length == 0 ? Enumerable.Range(0, game.State.Stars.Count) : ids).Select(i => Stars([i]))
            : (ids.Length == 0 ? Enumerable.Range(1, 26) : ids).Select(i => Fleets([i]));
        return ShowReports(blocks);
    }

    private void RedrawMap() {
        _report = []; _input = []; _arrivals = ""; _message = "";
        terminal.Clear();
    }

    // input.c:get_token: quantities matter for N; reports and J ignore them.
    private static IEnumerable<char> Tokens(string text) => CommandTokens(text).Select(t => t.Symbol);
    private static IEnumerable<(char Symbol, int Quantity)> CommandTokens(string text) {
        int index = 0;
        while (index < text.Length) {
            while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
            int start = index;
            while (index < text.Length && char.IsAsciiDigit(text[index])) index++;
            if (index == text.Length || text[index] == ' ') yield break;
            int quantity = start == index ? 1 : int.TryParse(text.AsSpan(start, index - start), out int value) ? value : -1;
            yield return (text[index++], quantity);
        }
    }

    private bool NewFleet() {
        const string prompt = " New tf- from tf: ";
        Draw(prompt); int key = Key();
        if (key < 0) return false;
        int source = key - 'A' + 1;
        int available = game.State.FleetsSys.ValidatePlayerSplit(source);
        int star = game.State.Players[1].Tf[source].Destination!.Value;
        if (available == 0) {
            _message = "! Taskforce limit reached!";
            _report = Stars([star]).ToArray();
            return true;
        }
        var line = Line(prompt + (char)key, lowerPrompt: "           ships: ");
        if (line == null) return false;
        var result = game.State.FleetsSys.SplitPlayerFleet(source, CommandTokens(line).Select(t => (t.Symbol, t.Quantity)));
        _message = result.Error ?? "";
        _input = [];
        _report = Stars([star]).ToArray();
        return true;
    }

    private bool JoinFleets() {
        var line = Line(" Join tfs:");
        if (line == null) return false;
        var tokens = Tokens(line).ToArray();
        int parent = (tokens.Length == 0 ? ' ' : tokens[0]) - 'A' + 1;
        _message = game.State.FleetsSys.JoinPlayerFleets(parent, tokens.Skip(1).Select(c => c - 'A' + 1)) ?? "";
        _report = Stars([game.State.Players[1].Tf[parent].Destination!.Value]).ToArray();
        return true;
    }

    private bool Land() {
        Draw(" Land Taskforce: "); int key = Key();
        if (key < 0) return false;
        int fleet = key - 'A' + 1;
        int star = game.State.FleetsSys.ValidatePlayerLandingFleet(fleet);
        _report = Stars([star]).ToArray();
        var planets = game.State.FleetsSys.PlayerLandingPlanets(fleet);
        string prompt = " Land Taskforce:" + (char)key; // C echoes over the label's trailing space.
        _input = ["         planet: "];
        int number;
        if (planets.Count == 1) number = planets[0].Number;
        else {
            Draw(prompt, 17, 19);
            int choice = Key();
            if (choice < 0) return false;
            number = choice - '0'; // C reads one character, not a planet-number line.
        }
        _input[0] += number.ToString(CultureInfo.InvariantCulture);
        game.State.FleetsSys.ValidatePlayerLandingPlanet(fleet, number);
        var line = Line(prompt, lowerPrompt: "     transports: ", lowerRow: 20);
        if (line == null) return false;
        // land() ignores get_token's symbol and all subsequent tokens, retaining
        // its numeric value even when the line ends immediately after digits.
        int quantity = FirstQuantity(line) ?? -1;
        game.State.FleetsSys.LandPlayerTransports(fleet, number, quantity);
        _report = Stars([star]).ToArray();
        return true;
    }

    private bool ShowReports(IEnumerable<IEnumerable<string>> blocks) {
        var page = new List<string>();
        _report = [];
        foreach (var block in blocks) {
            var rows = block.ToArray();
            if (rows.Length <= 17 && page.Count + rows.Length > 17) {
                _report = page.ToArray(); _message = "Press any key to continue"; Draw();
                if (Key() < 0) return false;
                _message = ""; page.Clear();
            }
            foreach (var row in rows) {
                if (page.Count == 17) {
                    _report = page.ToArray(); _message = "Press any key to continue"; Draw();
                    if (Key() < 0) return false;
                    _message = ""; page.Clear();
                }
                page.Add(row);
            }
        }
        _report = page.ToArray();
        return true;
    }

    private bool Destination() {
        Draw(" Destination tf: "); int choice = Key();
        if (choice < 0) return false;
        int fleet = choice - 'A' + 1;
        if (fleet is < 1 or > 26) throw new InvalidOperationException(" !illegal tf");
        var tf = game.State.Players[1].Tf[fleet];
        bool cancelled = game.State.FleetsSys.PreparePlayerCourse(fleet);
        var transcript = new List<string>();
        if (cancelled) transcript.Add("(Cancelling previous orders)");
        transcript.Add("        to star: ");
        _input = transcript.ToArray(); Draw(" Destination tf: " + (char)choice, 17, 19 + transcript.Count - 1);
        int key = Key();
        if (key < 0) return false;
        if (!char.IsControl((char)key)) transcript[^1] += (char)key;
        _input = transcript.ToArray();
        int star = key - 'A';
        if (star < 0 || star >= game.State.Stars.Count) throw new InvalidOperationException("  !illegal star");
        var target = game.State.Stars[star];
        double dx = target.X - tf.X, dy = target.Y - tf.Y;
        transcript.Add("       distance: " + Math.Sqrt(dx * dx + dy * dy).ToString("F1", CultureInfo.InvariantCulture).PadLeft(5));
        _input = transcript.ToArray();
        try { game.State.FleetsSys.SetCourse(1, fleet, star); }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Maximum range is ", StringComparison.Ordinal)) {
            int range = game.State.Players[1].Range == 0 ? Constants.Init.Range : game.State.Players[1].Range;
            throw new InvalidOperationException($"  !maximum range is {range,2}");
        }
        transcript.Add(tf.ETA == 0 ? "Tf remains at star." : $"eta in {tf.ETA,2} turns.");
        _input = transcript.ToArray();
        return true;
    }

    private static int? FirstQuantity(string line) {
        var text = line.AsSpan().TrimStart(); int digits = 0;
        while (digits < text.Length && char.IsAsciiDigit(text[digits])) digits++;
        return text.Length == 0 ? 0 : digits == 0 ? 1 : int.TryParse(text[..digits], out int parsed) ? parsed : null;
    }

    private bool Bombard() {
        _heading = ">>>>>>>> Blast Planet <<<<<<<<";
        const string prompt = " Firing TF:";
        Draw(prompt); int key = Key(); if (key < 0) return false;
        int fleet = key - 'A' + 1; int star = game.ValidateBombardmentFleet(fleet);
        var planets = game.State.Stars[star].Planets;
        _input = ["Target colony " + (planets.Count == 1 ? $"{planets[0].Number,2}" : ":")];
        int number;
        if (planets.Count == 1) number = planets[0].Number;
        else {
            Draw(prompt + (char)key, _input[0].Length, 19);
            int choice = Key(); if (choice < 0) return false;
            number = choice - '0'; _input[0] += number.ToString(CultureInfo.InvariantCulture);
        }
        game.ValidateBombardmentPlanet(fleet, number);
        int maximum = game.BombardmentMaximum(fleet);
        var line = Line(prompt + (char)key, lowerPrompt: $"Units (max {maximum,3}) :", lowerRow: 20);
        if (line == null) return false;
        int amount = FirstQuantity(line) ?? throw new InvalidOperationException(" !Invalid blast quantity");
        game.BombardPlanet(fleet, number, amount);
        if (game.State.Players[1].Tf[fleet].Blasting != 0)
            _input = [.. _input, $"Blasting {Math.Min(maximum, amount),3} units"];
        _report = Stars([star]).ToArray();
        return true;
    }

    private bool ResolveClassicCombat() {
        while (game.HasPendingClassicCombat) {
            if (game.State.Battle != null) { if (!ResolveFleetBattle()) return false; continue; }
            if (game.State.EnemyAttack != null) { if (!ResolveEnemyAttack()) return false; continue; }
            var attack = game.State.Attack!;
            _heading = null;
            _report = Stars([attack.StarIndex]).ToArray();
            if (attack.AwaitingAcknowledgement) {
                _input = [];
                _message = "Planet attack concluded"; Draw("P?");
                _message = "Press any key to continue"; Draw("P?");
                if (Key() < 0) return false;
                game.AcknowledgePlayerAttack(); _message = ""; _input = [];
                continue;
            }
            Draw("P?", 2); int key = Key(); if (key < 0) return false;
            _message = "";
            try {
                switch (key) {
                    case 'G': case ' ': if (!PlayerSalvo()) return false; break;
                    case 'B': _input = ["Break off attack"]; game.EndPlayerAttack(); Draw("P?"); break;
                    case 'N': if (!NewFleet()) return false; break;
                    case 'J': if (!JoinFleets()) return false; break;
                    case 'S': if (!Summarize(true)) return false; break;
                    case 'T': if (!Summarize(false)) return false; break;
                    case 'C': if (!ShowReports(Colonies().Select(row => new[] { row }.AsEnumerable()))) return false; break;
                    case 'R': if (Line(" Research field(s):") == null) return false; break;
                    case 'M': RedrawMap(); break;
                    case 'H':
                        _report = new[] { ("B", "Break off Attack"), ("C", "Colony summary"), ("G", "Go on (done)"), ("H", "Help"),
                            ("J", "Join TFs"), ("M", "Redraw Map"), ("N", "New TF"), ("R", "Research summary"), ("S", "Star summary"), ("T", "TaskForce summary") }
                            .Select(h => $"{h.Item1,2} - {h.Item2,-25}").ToArray();
                        _message = "Press any key to continue"; Draw("P?"); if (Key() < 0) return false;
                        _message = ""; break;
                    default: _input = []; _message = " !Illegal command"; break;
                }
            }
            catch (InvalidOperationException ex) { _message = ex.Message; }
        }
        return true;
    }

    private static string FleetLossText(ShipLosses losses) {
        string text = "";
        void Add(int count, string symbol) { if (count > 0) text += $" {count,2}{symbol}"; }
        Add(losses.Transports, "t"); Add(losses.Scouts, "s"); Add(losses.Cruisers, "c"); Add(losses.BattleStars, "b");
        return text.Length == 0 ? "(none)" : text;
    }
    private void BattleFields(FleetBattle b) {
        string Force(string label, int force, int weapons, double save) => $"{label} {force,5}"
            + (force > 0 ? $"(weap {weapons,2})" : "         ") + $"sur: {save * 100,4:F0}";
        _input = [Force("enemy ", b.EnemyForces, game.State.Players[0].Weapons, b.EnemySurvival),
            Force("player", b.PlayerForces, game.State.Players[1].Weapons, b.PlayerSurvival)];
        if (b.Stage != FleetBattleStage.InitialPause)
            _input = [.. _input, " Enemy losses:" + FleetLossText(b.EnemyLosses), "Player losses:" + FleetLossText(b.PlayerLosses)];
    }
    private bool ResolveFleetBattle() {
        bool keepReport = false;
        while (game.State.Battle is { } b) {
            _heading = null;
            if (!keepReport) _report = Stars([b.StarIndex]).ToArray();
            keepReport = false;
            BattleFields(b);
            if (b.Stage != FleetBattleStage.Orders) {
                _message = "Press any key to continue"; Draw("B?", 2);
                if (Key() < 0) return false;
                _message = "";
                try { game.AcknowledgeFleetBattle(); }
                catch (InvalidOperationException ex) { _message = ex.Message; }
                continue;
            }
            try { game.PrepareEnemyWithdrawal(); }
            catch (InvalidOperationException ex) { _message = ex.Message; }
            if (b.PlayerWithdrawalFleet != null) {
                if (!WithdrawalDestination()) return false;
                continue;
            }
            Draw("B?", 2); int key = Key(); if (key < 0) return false;
            _message = "";
            try {
                switch (key) {
                    case 'G': case ' ':
                        string? withdrawal = b.EnemyWithdrawalFleet is int slot
                            ? "en withdraws " + StarShips(game.State.Players[0].Tf[slot]) : null;
                        game.ContinueFleetBattle();
                        if (withdrawal != null) {
                            BattleFields(b); _input = [.. _input, withdrawal]; Draw("B?");
                        }
                        break;
                    case 'W':
                        if (game.State.FleetsSys.ValidatePlayerSplit(b.PlayerFleet) == 0)
                            throw new InvalidOperationException("! Taskforce limit reached!");
                        var line = Line("Withdraw   ships: ");
                        if (line == null) return false;
                        _message = game.BeginBattleWithdrawal(CommandTokens(line).Select(t => (t.Symbol, t.Quantity))) ?? "";
                        if (b.PlayerWithdrawalFleet != null && !WithdrawalDestination()) return false;
                        break;
                    case 'O': game.RefreshBattleForces(); break;
                    case '?': break;
                    case 'M': RedrawMap(); break;
                    case 'S': if (!Summarize(true)) return false; keepReport = true; break;
                    case 'T': if (!Summarize(false)) return false; keepReport = true; break;
                    case 'C': if (!ShowReports(Colonies().Select(row => new[] { row }.AsEnumerable()))) return false; keepReport = true; break;
                    case 'R': if (Line(" Research field(s):") == null) return false; break;
                    case 'H':
                        _report = new[] { ("C", "Colonies"), ("G", "Go on (done)"), ("H", "Help"), ("M", "Map"), ("O", "Odds"),
                            ("R", "Research summary"), ("S", "Star summary"), ("T", "TaskForce summary"), ("W", "Withdraw") }
                            .Select(h => $"{h.Item1,2} - {h.Item2,-25}").ToArray();
                        keepReport = true; break;
                    default: _message = "!illegal command"; break;
                }
            }
            catch (InvalidOperationException ex) { _message = ex.Message; }
        }
        _input = []; _message = ""; return true;
    }
    private bool WithdrawalDestination() {
        var b = game.State.Battle!; int slot = b.PlayerWithdrawalFleet!.Value;
        _input = ["        to star: "]; Draw("Withdraw ", 17, 19);
        int key = Key(); if (key < 0) return false;
        int target = key - 'A'; var f = game.State.Players[1].Tf[slot];
        _input[0] += (char)key;
        if ((uint)target < (uint)game.State.Stars.Count) {
            var s = game.State.Stars[target]; double distance = Math.Sqrt(Math.Pow(s.X - f.X, 2) + Math.Pow(s.Y - f.Y, 2));
            _input = [.. _input, $"       distance: {distance,5:F1}"];
        }
        try {
            game.FinishBattleWithdrawal(target);
            _input = [.. _input, $"eta in {f.ETA,2} turns."];
        }
        catch (InvalidOperationException ex) { _message = ex.Message; }
        Draw("Withdraw "); return true;
    }
    private bool ResolveEnemyAttack() {
        while (game.State.EnemyAttack is { } attack) {
            _heading = null; _report = Stars([attack.StarIndex]).ToArray(); _input = [];
            if (attack.Salvo is { } result) {
                string Loss(int n, string symbol) => n > 0 ? $" {n,2}{symbol}" : "";
                if (result.AttackerSurvival is double a && result.DefenderSurvival is double d) {
                    string al = Loss(result.CruisersLost, "c") + Loss(result.BattleStarsLost, "b");
                    string dl = Loss(result.MissileBasesLost, "mb") + Loss(result.AdvancedBasesLost, "amb");
                    _input = [$" EN: {result.AttackerForces,4}(weap {game.State.Players[0].Weapons,2})sur: {a * 100,4:F0}",
                        $" {game.State.Stars[attack.StarIndex].Name}{attack.Planet.Number}:{result.DefenderForces,4} (weap {game.State.Players[1].Weapons,2})sur: {d * 100,4:F0}",
                        "Attacker losses:" + (al.Length == 0 ? "(none)" : al), " Planet losses :" + (dl.Length == 0 ? "(none)" : dl)];
                }
                if (result.Captured) _input = [.. _input, .. Enumerable.Repeat("", 4 - _input.Length), $"                Planet {attack.Planet.Number} falls!"];
            }
            _message = "Press any key to continue"; Draw("B?", 2);
            if (Key() < 0) return false;
            _message = ""; game.AcknowledgeEnemyAttack();
        }
        _input = []; return true;
    }

    private bool PlayerSalvo() {
        var attack = game.State.Attack!; var star = game.State.Stars[attack.StarIndex];
        var enemies = star.Planets.Where(p => p.Owner == Ownership.Enemy).ToArray();
        string prompt = "P?Attack planet "; int number;
        if (enemies.Length > 1) {
            Draw(prompt + ":"); int key = Key(); if (key < 0) return false; number = key - '0';
        }
        else number = enemies[0].Number;
        Draw(prompt + (enemies.Length > 1 ? ":" : "") + number);
        game.ValidateSalvoPlanet(number);
        var fleets = game.State.Players[1].Tf.Select((f, i) => (f, i)).Skip(1)
            .Where(x => x.f.Destination == attack.StarIndex && x.f.ETA == 0).ToArray();
        int fleet;
        if (fleets.Length > 1) {
            Draw(" attacking tf :"); int key = Key(); if (key < 0) return false; fleet = key - 'A' + 1;
        }
        else fleet = fleets[0].i;
        Draw(" attacking tf " + (fleets.Length > 1 ? ":" : "") + (char)('a' + fleet - 1));
        var result = game.FirePlayerSalvo(fleet, number);
        _input = [];
        if (result.AttackerSurvival is double attacker && result.DefenderSurvival is double defender) {
            string Loss(int count, string symbol) => count == 0 ? "" : $" {count,2}{symbol}";
            string aLoss = Loss(result.CruisersLost, "c") + Loss(result.BattleStarsLost, "b");
            string pLoss = Loss(result.MissileBasesLost, "mb") + Loss(result.AdvancedBasesLost, "amb");
            _input = [ $"TF{(char)('a' + fleet - 1)}: {result.AttackerForces,4}(weap {game.State.Players[1].Weapons,2})sur: {attacker * 100,4:F0}",
                $" {star.Name}{number}:{result.DefenderForces,4} (weap {game.State.Players[0].Weapons,2})sur: {defender * 100,4:F0}",
                "Attacker losses:" + (aLoss.Length == 0 ? "(none)" : aLoss), " Planet losses :" + (pLoss.Length == 0 ? "(none)" : pLoss) ];
        }
        if (result.Captured) _input = [.. _input, .. Enumerable.Repeat("", 4 - _input.Length), $"                Planet {number} falls!"];
        _report = Stars([attack.StarIndex]).ToArray();
        Draw(" attacking tf " + (char)('a' + fleet - 1));
        return true;
    }

    private bool AdvanceMovement() {
        if (game.State.ClassicTurnInProgress) return FinishEconomicTurn();
        var before = game.State.Players[1].Tf.Skip(1).Select((f, index) => new {
            Slot = index + 1, f.Destination, f.ETA, f.Transports, f.Scouts, f.Cruisers, f.BattleStars,
            Unknown = f.Destination is int star && !game.State.Stars[star].ExploredBy[1]
        }).ToArray();
        game.BeginClassicTurn();
        var arrived = new SortedSet<int>();
        var explored = new HashSet<int>();
        foreach (var old in before.Where(f => f.ETA == 1 && f.Destination != null)) {
            var fleet = game.State.Players[1].Tf[old.Slot];
            int star = old.Destination!.Value;
            if (fleet.Destination != null) arrived.Add(star);
            // If the first arrival survived, later arrivals at the same star are known.
            if (!old.Unknown || explored.Contains(star)) continue;
            _input = [];
            if (!game.State.Config.SafeExplore) {
                string losses = "";
                void Loss(int count, char symbol) { if (count > 0) losses += $" {count,2}{symbol}"; }
                Loss(old.Transports - fleet.Transports, 't'); Loss(old.Scouts - fleet.Scouts, 's');
                Loss(old.Cruisers - fleet.Cruisers, 'c'); Loss(old.BattleStars - fleet.BattleStars, 'b');
                _input = [(losses.Length == 0 ? "No ships" : losses) + " destroyed."];
            }
            if (fleet.Destination != null) { explored.Add(star); _report = Stars([star]).ToArray(); }
            _message = "Press any key to continue";
            Draw($"Task force {(char)('a' + old.Slot - 1)} exploring {game.State.Stars[star].Name}.");
            if (Key() < 0) return false;
            _message = ""; _input = [];
        }
        _arrivals = arrived.Count == 0 ? "" : "P Arrive: " + string.Concat(arrived.Select(i => game.State.Stars[i].Name)).PadLeft(12);
        return FinishEconomicTurn();
    }

    private bool FinishEconomicTurn() {
        game.PrepareClassicCombat();
        if (!ResolveClassicCombat()) return false;
        game.PrepareClassicEconomy();
        bool keepReport = false;
        while (game.State.Investment is { } session) {
            if (!keepReport) _report = Stars([session.StarIndex]).ToArray();
            keepReport = false;
            _input = [];
            string prompt = $"{game.State.Stars[session.StarIndex].Name}{session.Planet.Number}>{session.Balance,3}?";
            var line = Line(prompt);
            if (line == null) return false;
            _message = "";
            var tokens = CommandTokens(line).ToArray();
            for (int i = 0; i < tokens.Length; i++) {
                var (key, quantity) = tokens[i];
                if (key == 'H') {
                    _report = new[] { ("A", "Bld Adv. Missle Base 35"), ("B", "Bld Battlestar(s)    70"), ("C", "Bld Cruiser(s)       16"),
                        ("H", "Help"), ("I", "Invest                3"), ("M", "Bld Missle Base(s)    8"), ("R", "Range Research"),
                        ("S", "Bld Scout(s)          6"), ("T", "Bld Transports"), ("V", "Vel Research"), ("W", "Weapons Research"),
                        ("*x", "Bld max. # of x"), ("+", "Bld max. offensive"), ("-", "Bld max. defensive"),
                        (">C", "Colony summary"), (">M", "Redraw Map"), (">R", "Research summary"), (">S", "Star summary") }
                        .Select(h => $"{h.Item1,2} - {h.Item2,-25}").ToArray();
                    keepReport = true;
                }
                else if (key == '>') {
                    Draw(">?", 2, 18); int command = Key();
                    if (command < 0) return false;
                    switch (command) {
                        case 'M': RedrawMap(); break;
                        case 'S': if (!Summarize(true)) return false; keepReport = true; break;
                        case 'C': if (!ShowReports(Colonies().Select(row => new[] { row }.AsEnumerable()))) return false; keepReport = true; break;
                        case 'R': if (Line(" Research field(s):") == null) return false; break;
                        default: _message = " !Only M,S,C,R allowed"; break;
                    }
                }
                else {
                    bool maximum = key == '*';
                    if (maximum) { if (++i == tokens.Length) break; (key, quantity) = tokens[i]; }
                    string? error = game.InvestInClassicColony(key, quantity, maximum);
                    if (error != null) _message = error;
                    keepReport = false;
                }
            }
            game.FinishClassicInvestmentLine();
            if (!keepReport || game.State.Investment == null) _report = Stars([session.StarIndex]).ToArray();
        }
        game.PrepareClassicCombat(afterInvestment: true);
        if (!ResolveClassicCombat()) return false;
        game.CompleteClassicTurn();
        _input = [];
        // main.c checks the outcome after the whole turn, not in movement/combat/investment.
        // NotStarted models are the existing partial development/test facility, not completed startup games.
        if (game.State.PlayerStartupStage == PlayerStartupStage.Completed && game.CheckClassicGameOver() is { } result) {
            ShowGameResult(result); return false;
        }
        return true;
    }
    private IEnumerable<string> Fleets(IEnumerable<int> ids) {
        foreach (int i in ids) {
            if (i is < 1 or > 26) continue;
            var f = game.State.Players[1].Tf[i];
            if (f.Destination is not int dest) continue;
            yield return $"TF{(char)('a' + i - 1)}:{(f.ETA == 0 ? game.State.Stars[dest].Name : " ")}({f.X + 1,2},{f.Y + 1,2})"
                + StarShips(f) + (f.ETA > 0 ? $" {game.State.Stars[dest].Name}{f.ETA}" : "");
        }
    }
    private IEnumerable<string> Colonies() {
        yield return "Colonies:";
        foreach (var star in game.State.Stars)
            foreach (var planet in star.Planets.Where(p => p.Owner == Ownership.Player))
                yield return $"{star.Name}{planet.Number}:{planet.PlayerSeenCapacity,2}"
                    + $"({planet.Inhabitants,2},/{planet.IndustrialUnits,3})"
                    + (planet.Conquered ? "Con" : "   ") + StarDefenses(planet);
    }
    private IEnumerable<string> Stars(IEnumerable<int> ids) {
        foreach (int i in ids) {
            if (i < 0 || i >= game.State.Stars.Count) continue;
            var s = game.State.Stars[i];
            if (!s.ExploredBy[1]) continue;
            yield return $"--------- Star {s.Name} ---------";
            // display.c:print_star uses a different row layout from print_tf.
            var stationed = game.State.Players[1].Tf.Skip(1)
                .Select((fleet, index) => (fleet, index))
                .Where(f => f.fleet.Destination == i && f.fleet.ETA == 0).ToArray();
            bool see = stationed.Length != 0 || s.Planets.Any(p => p.Owner == Ownership.Player);
            foreach (var (fleet, index) in stationed) {
                if (fleet.Transports > 0 || fleet.Scouts > 0 || fleet.Cruisers > 0 || fleet.BattleStars > 0)
                    yield return $"TF{(char)('a' + index)}  {StarShips(fleet)}";
            }
            // C shows the first stationary enemy fleet; enemy arrivals normally join.
            var enemy = game.State.Players[0].Tf.Skip(1).FirstOrDefault(f => f.Destination == i && f.ETA == 0);
            if (see && enemy != null) yield return $" EN: {StarShips(enemy)}";
            if (s.Planets.Count == 0) yield return "  no useable planets";
            foreach (var p in s.Planets) {
                string details = p.PlayerSeenCapacity == 0 ? " Decimated" : p.Owner switch {
                    Ownership.None => see ? " No colony" : "",
                    Ownership.Player => $"({p.Inhabitants,2},/{p.IndustrialUnits,3})"
                        + (p.Conquered ? "Con" : "   ") + StarDefenses(p),
                    Ownership.Enemy when see => "*EN*" + (p.Conquered ? "Conquered" : "   ")
                        + (p.UnderAttack ? StarDefenses(p) : ""),
                    _ => ""
                };
                yield return $" {p.Number}:{p.PlayerSeenCapacity,2}" + details;
            }
        }
    }

    private static string StarShips(TaskForce f) => StarShipCount(f.Transports, 't')
        + StarShipCount(f.Scouts, 's') + StarShipCount(f.Cruisers, 'c') + StarShipCount(f.BattleStars, 'b');
    private static string StarShipCount(int count, char suffix) => count > 0 ? $"{count,2}{suffix}" : "   ";
    private static string StarDefenses(Planet p) => (p.MissileBases != 0 ? $"{p.MissileBases,2}mb" : "    ")
        + (p.AdvancedMissileBases != 0 ? $"{p.AdvancedMissileBases,2}amb" : "");
}
