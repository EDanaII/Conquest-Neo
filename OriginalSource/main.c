/* Main.c: main() and *all* global variables */
#include <stdio.h>
#include <stdlib.h>

#include "defs.h"
#include "machine.h"
#include "structs.h"
#include "vars.h"
#include "protos.h"

BOOL game_over = FALSE;

// Initialization of global variables, to 0/FALSE default 
struct config conf;

int tf_stars[MAX_NUM_STARS+1][2], col_stars[MAX_NUM_STARS+1][2];

char en_research = ' ';

#define EMPSEC {' ', '.', ' '}
#define EMPTY_LINE { {0,0,0}, EMPSEC, EMPSEC, EMPSEC, EMPSEC, EMPSEC, EMPSEC,\
EMPSEC, EMPSEC, EMPSEC, EMPSEC, EMPSEC, EMPSEC, EMPSEC, EMPSEC, EMPSEC }

struct ssector board[MAX_BOARD_SIZE+1][MAX_BOARD_SIZE+1] 
= { /* Empty line in top (should go) */EMPTY_LINE,
    EMPTY_LINE, EMPTY_LINE, EMPTY_LINE, EMPTY_LINE, EMPTY_LINE, EMPTY_LINE,
    EMPTY_LINE, EMPTY_LINE, EMPTY_LINE, EMPTY_LINE, EMPTY_LINE, EMPTY_LINE,
    EMPTY_LINE, EMPTY_LINE, EMPTY_LINE
  };

#define EMPSTAR {0,0,NULL,{0,0}}

struct ststar stars[MAX_NUM_STARS+1] 
= { EMPSTAR, EMPSTAR, EMPSTAR, EMPSTAR, EMPSTAR, EMPSTAR, EMPSTAR, EMPSTAR,
    EMPSTAR, EMPSTAR, EMPSTAR, EMPSTAR, EMPSTAR, EMPSTAR, EMPSTAR, EMPSTAR,
    EMPSTAR, EMPSTAR, EMPSTAR, EMPSTAR, EMPSTAR, EMPSTAR
  };

struct sttf tf[2][27];

double growth_rate[2] = { 0.5, 0.3}; /* Enemy and players growth rate! */

int weap_req[11] 
= {0, 0, 0, 0, 50, 70, 90, 120, 150, 250, 350};

int ran_req[MAX_BOARD_SIZE+1] 
= {0, 0, 0, 0, 0, 0, 20, 40, 70, 100, 150, 200, 300, 400, 600, 900};

int vel_req[MAX_VELOCITY+1] 
= {0, 0, 40, 60, 80, 120, 150, 200, 250, 300, 400, 500, 600};

// Enemy and players initial research 
int vel[2] = {INIT_VEL, INIT_VEL}, range[2] = {INIT_RANGE+2, INIT_RANGE}, weapons[2] = {INIT_WEAPONS, INIT_WEAPONS};
int weap_working[2],ran_working[2],vel_working[2];
UINT turn = 1; 
int production_year = 1;
int enemy_arrivals[MAX_NUM_STARS+1], en_departures[MAX_NUM_STARS+1], player_arrivals[MAX_NUM_STARS+1];
int bottom_field;

termtype terminal_type = unknown;

int x_cursor, y_cursor, saved_game, left_line[25], debug;

FILE *stdIn;
FILE *stdOut;

int main(int argc, char *argv[]) {
#if CQ_TEE_LOG
    cq_log_open();
    atexit(cq_log_close);
    // sanity ping so we know the file is writable:
    printf("[log] transcript started\n");
#endif

  startup(argc, argv);
  do {
    inputplayer();
    if (!(game_over)) {
      inputmach();
      move_ships();
      battle();
      if (production_year == 4 && turn < conf.num_turns)
        invest();
      up_year();
    }
    check_game_over();
  }
  while (!game_over);
  exit(0);
}
