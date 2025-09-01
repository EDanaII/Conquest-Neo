namespace Conquest.Core;

public static class Constants {
    public static class Cost {
        public const int Infrastructure = 3;   // I_COST
        public const int MissileBase = 8;   // MB_COST
        public const int Cruiser = 16;  // C_COST
        public const int Scout = 6;   // S_COST
        public const int AdvMissileBase = 35;  // AMB_COST
        public const int Battleship = 70;  // B_COST
    }

    public static class Guns {
        public const int Cruiser = 7;
        public const int Battleship = 40;
    }

    public static class Defense {
        public const int Scout = 2;
        public const int Transport = 1;
    }

    public static class Max {
        public const int BoardSize = 15;
        public const int Velocity = 12;
        public const int Range = BoardSize;
        public const int Weapons = 10;
        public const int Stars = 26;
        public const int Turns = 100;
        public const int Fleets = 26;
    }

    public static class Init {
        public const int Units = 35;
        public const int Money = 30;
        public const int Velocity = 1;
        public const int Range = 5;
        public const int Weapons = 3;
        public const int UnitRatio = 2;
    }

    public static class EventProb {
        public const double Transport = 10.0;
        public const double Scout = 70.0;
        public const int Cruiser = 90;
        public const double Battleship = 97.0;
    }

    public static class EventVar {
        public const int Transport = 5;
        public const int Scout = 10;
        public const int Cruiser = 10;
        public const int Battleship = 3;
    }

    public static class UI {
        public const string BlankLine = "                              ";
    }

    public static int OtherPlayer(int team) => 1 - team;
}
