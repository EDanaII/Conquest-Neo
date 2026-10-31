namespace Conquest.Core {
	public partial class Game {
		public int Turn => State.CompletedTurns;

		public int Tick(int n = 1) {
			State.EnsureStartupAllowsAdvancement();
			if (n < 1) n = 1;
			for (int i = 0; i < n; i++) {
				State.HasAdvancedTurns = true;
				State.FleetsSys.Tick(_rng);
				State.CompletedTurns++;
			}
			return Turn;
		}
	}
}
