namespace Conquest.Core.Model {
    public partial class GameState {
        private Conquest.Core.FleetSystem? _fleets;
        public Conquest.Core.FleetSystem FleetsSys => _fleets ??= new Conquest.Core.FleetSystem(this);
    }
}
