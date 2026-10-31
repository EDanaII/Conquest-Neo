namespace Conquest.Core.Model;

public enum ClassicWinner { Draw, Player, Enemy }

[Flags]
public enum ClassicEndReason {
    None = 0,
    PlayerEliminated = 1,
    EnemyEliminated = 2,
    PlayerOverwhelmed = 4,
    EnemyOverwhelmed = 8,
    TurnLimit = 16,
    Quit = 32
}

public sealed record ClassicScore(int Transports, int IndustrialUnits) {
    public int Total => checked(Transports + IndustrialUnits);
}

public sealed record ClassicGameResult(int DisplayedTurn, ClassicScore Player, ClassicScore Enemy,
    ClassicEndReason Reasons, ClassicWinner Winner);
