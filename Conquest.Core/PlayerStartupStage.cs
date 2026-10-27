namespace Conquest.Core.Model;

public enum PlayerStartupStage {
    NotStarted,
    SelectingStar,
    SelectingAutomatically,
    Spending,
    Completed,
    Failed
}

public enum StartupShip {
    Cruiser,
    Scout,
    Battlestar
}
