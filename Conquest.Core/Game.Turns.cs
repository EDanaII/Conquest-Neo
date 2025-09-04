namespace Conquest.Core {
	public partial class Game {
		public int Turn { get; private set; }

		public int Tick(int n = 1) {
			if (n < 1) n = 1;
			for (int i = 0; i < n; i++) {
				State.FleetsSys.Tick( );
				Turn++;
			}
			return Turn;
		}
	}
}
