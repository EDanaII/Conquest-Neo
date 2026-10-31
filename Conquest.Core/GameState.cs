namespace Conquest.Core.Model;

public partial class GameState {
    internal bool HasGeneratedGalaxy { get; set; }
    internal bool ClassicPlayerInitializationStarted { get; set; }
    internal bool ClassicPlayerInitializationCompleted { get; set; }
    internal bool EnemyStartupResearchStarted { get; set; }
    internal bool ClassicEnemyInitializationStarted { get; set; }
    internal bool ClassicEnemyInitializationCompleted { get; set; }
    internal bool HasAdvancedTurns { get; set; }
    public ResearchField? EnemyResearchField { get; internal set; }
    public PlayerStartupStage PlayerStartupStage { get; internal set; }
    public int? PlayerStartingStar { get; internal set; }
    public int PlayerStartupBalance { get; internal set; }
    public bool PendingSharedStartEncounter { get; internal set; }
    public int CompletedTurns { get; internal set; }
    public ClassicGameResult? Result { get; internal set; }
    public int ProductionYear { get; internal set; } = 1;
    public bool ClassicTurnInProgress { get; internal set; }
    public ColonyInvestment? Investment { get; internal set; }
    internal bool InvestmentCycleDue { get; set; }
    internal bool EconomyPrepared { get; set; }
    internal Queue<(int StarIndex, Planet Planet)>? ProductionQueue { get; set; }
    public PlanetaryAttack? Attack { get; internal set; }
    internal bool CombatPrepared { get; set; }
    internal bool PostInvestmentCombatPrepared { get; set; }
    public FleetBattle? Battle { get; internal set; }
    public EnemyPlanetAttack? EnemyAttack { get; internal set; }
    internal int CombatStar { get; set; }
    internal int CombatStep { get; set; }
    internal bool StartupCombatInProgress { get; set; }
    internal string? EnemyProcessingFailure { get; set; }
    internal HashSet<int> EnemyArrivals { get; } = [];
    internal HashSet<int> EnemyDepartures { get; } = [];
    public IEnumerable<int> VisibleEnemyArrivals => EnemyArrivals.Order();
    public IEnumerable<int> VisibleEnemyDepartures => EnemyDepartures.Order();

    internal void EnsureStartupAllowsAdvancement() {
        if (Result != null) throw new InvalidOperationException("The Classic game is over.");
        if (EnemyProcessingFailure != null) throw new InvalidOperationException(EnemyProcessingFailure);
        if (ClassicTurnInProgress)
            throw new InvalidOperationException("Complete the pending Classic turn/investment before advancing movement.");
        if (PendingSharedStartEncounter
            || PlayerStartupStage is not (Model.PlayerStartupStage.NotStarted or Model.PlayerStartupStage.Completed))
            throw new InvalidOperationException("Player startup must finish and any shared-start encounter must be resolved before advancement.");
    }

    public GameConfig Config { get; }

    // All engine data is 0-based internally.
    public List<Star> Stars { get; }
    public Player[] Players { get; } = new Player[] { new Player( ), new Player( ) };

    // 0-based board: [0..Width-1, 0..Height-1]
    public Sector[,] Board { get; }

    public GameState(GameConfig? config = null) {
        Config = (config ?? new GameConfig( )).Validate( );

        // Exactly NumStars (0..NumStars-1) — no dummy element
        Stars = new List<Star>(Config.NumStars);
        for (int i = 0; i < Config.NumStars; i++)
            Stars.Add(new Star { Name = NameFor(i) });

        // Allocate exact size and init sectors
        Board = new Sector[Config.Width, Config.Height];
        for (int x = 0; x < Config.Width; x++)
            for (int y = 0; y < Config.Height; y++)
                Board[x, y] = new Sector { ViewingTeam = 1 }; // 1 = player viewpoint by default
    }

    // Place stars at unique coords; repeat placement is allowed before Classic initialization.
    public void PlaceStars(IRng rng) {
        if (ClassicPlayerInitializationStarted)
            throw new InvalidOperationException("Cannot regenerate the galaxy after Classic player initialization has started.");
        // clear old placements
        for (int x = 0; x < Config.Width; x++)
            for (int y = 0; y < Config.Height; y++)
                Board[x, y].StarRef = null;

        var used = new HashSet<(int x, int y)>( );
        for (int i = 0; i < Config.NumStars; i++) {
            int x, y;
            do {
                x = rng.Next(0, Config.Width);   // 0..Width-1
                y = rng.Next(0, Config.Height);  // 0..Height-1
            } while (!used.Add((x, y)));

            var s = Stars[i];
            s.X = x; s.Y = y;

            var cell = Board[x, y];
            cell.StarRef = s;
            cell.Star = i; // index into Stars (0-based)
        }
    }

    private static string NameFor(int i) {
        // A..Z, then AA..AZ, BA.. etc. (i is 0-based)
        const string ABC = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string name = "";
        do { name = ABC[i % 26] + name; i = i / 26 - 1; } while (i >= 0);
        return name;
    }

    public bool FogEnabled { get; set; } = false;

    // 0-based bounds: valid x in [0,Width-1], y in [0,Height-1]
    public Star? StarAt(int x, int y) {
        if (x < 0 || x >= Config.Width || y < 0 || y >= Config.Height) return null;
        return Board[x, y].StarRef;
    }
}
