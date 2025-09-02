#ifndef EXEC_TYPES_H
typedef int BOOL;
#endif

typedef unsigned int UINT;

typedef struct ssector {
  int status, star, tf;
} sector;

typedef struct ssector tboard[MAX_BOARD_SIZE+1][MAX_BOARD_SIZE+1];

/* This one is to be removed where possible */
typedef int tteam;

#define ENEMY   0
#define PLAYER  1
#define NONE    2

typedef struct sttf {
  int x,y,xf,yf,s,t,c,b,dest,eta,origeta;
  int blasting, withdrew;
} ttf;

typedef struct ststar {
  int x, y;
  struct stplanet *first_planet;
  int visit[2];
} tstar;

typedef struct stplanet {
  int number,capacity,psee_capacity;
  int team;
  int inhabitants,iu,mb,amb;
  int conquered,under_attack;
  int esee_team;  /*the   team when the enemy last saw it*/
  int esee_def;   /*the   mbs when enemy last saw it*/
  int pstar;
  struct stplanet *next;
} tplanet;

typedef struct stplayer {
  int tf_stars[MAX_NUM_STARS+1];
  int col_stars[MAX_NUM_STARS+1];
  struct sttf tf[27];
  double growth_rate[2];
  int vel, range, weapons, weap_working, ran_working, vel_working;
} tplayer;

struct config {
  UINT    difficulty;     /* How much does the computer cheat? */
  UINT    board_width;    /* Dimensions of the active part */
  UINT    board_height;
  UINT    num_stars;      /* Number of stars, max 27 */
  UINT    num_turns;      /* Number of turns */
  BOOL    SafeExplore;
  BOOL    stars_ordered; /* Are the starts ordered by quality? */
  int     transports;
  int     money;
  int     capacity;
  double  victory;
  double  overwhelm;
  char PenUnexpl[8];
  char PenColony[8];
  char PenAttack[8];
  char PenNormal[8];
  char invest[81];
};

typedef int toption;
#define right   0
#define left    1
#define both    2

typedef int termtype;
#define hardcopy        0
#define adm3    1
#define vt52    2
#define concept 3
#define vi      4
#define vis400  5
#define unknown 6

/* Don't know diddly about these, really, so I just assume... */
#define vt100   vt52
#define xterm   vt52
#define hpterm  vt52
