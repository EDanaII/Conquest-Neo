// Conquest.Core/View/SectorView.cs  (adapter for UI/debug)
using Conquest.Core.Models;

namespace Conquest.Core.View;

public sealed class SectorView {
    private readonly Sector _raw;
    private readonly GameState _state;
    public int ViewingTeam { get; set; }

    public SectorView(Sector raw, GameState state, int viewingTeam) {
        _raw = raw; _state = state; ViewingTeam = viewingTeam;
    }

    public Star? Star => _raw.Star > 0 ? _state.Stars[_raw.Star] : null;

    public TaskForce? TaskForceForViewer =>
        (ViewingTeam is 0 or 1) ? _state.Players[ViewingTeam].Tf[_raw.Tf] : null;

    public override string ToString( ) {
        var star = Star;
        var tf = TaskForceForViewer;
        var starMark = star is null
            ? " "
            : (star.ExploredBy[ViewingTeam] ? " " : "?");
        var starName = star?.Name ?? ".";
        var tfName = tf?.Name ?? " ";
        return $"{starMark}{starName}{tfName}";
    }
}
