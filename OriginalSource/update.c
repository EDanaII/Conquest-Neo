/* Update.c: Routines for updating */
#include <stdio.h>
#include "defs.h"
#include "machine.h"
#include "structs.h"
#include "vars.h"
#include "protos.h"

void update_board(int x, int y, toption option) {
  int screen_x, screen_y;

  if (terminal_type != hardcopy) {
    screen_x = 3*x + 1;
    screen_y = 16 - y;
/*    switch (option) {
     case left:
      point(screen_x,screen_y);
      putchar(board[x][y].status);
      break;

     case right:
      point(screen_x+2,screen_y);
      putchar(board[x][y].tf);
      break;

     case both:*/
      point(screen_x, screen_y);
      if (board[x][y].status == '?') {
        display(conf.PenUnexpl);
      }
      if (board[x][y].status == '@') {
        display(conf.PenColony);
      }
      if (board[x][y].status == '!') {
        display(conf.PenAttack);
      }
      display(fmt("%c%c%c", board[x][y].status,board[x][y].star, board[x][y].tf));
      display(conf.PenNormal);
      /*      break;
    }*/
  }
}

void up_year() {
  displayAt(EVNVWTL+1, fmt("Turn:              %3d", ++turn) );
  displayAt(EVNVWTL+2, fmt("Prod yr:             %d", ++production_year) );
}

/* Remove empty taskforces */
void zero_tf(tteam tm, int tf_num) {
  int x, y, i;

  if (tf[tm][tf_num].dest != 0) {
    x = tf[tm][tf_num].x;
    y = tf[tm][tf_num].y;
    if ((tf[tm][tf_num].s+tf[tm][tf_num].t + tf[tm][tf_num].c+tf[tm][tf_num].b) == 0) {
      if (tf[tm][tf_num].eta == 0)
	      tf_stars[tf[tm][tf_num].dest][tm]--;

      tf[tm][tf_num].dest=0;

      if (tm==PLAYER) {
	      board[x][y].tf=' ';

	      for(i=1; i<=MAX_FLEETS; i++) {
	        if ((tf[PLAYER][i].dest !=0) && (tf[PLAYER][i].x == x) && (tf[PLAYER][i].y == y)) {
	          if (board[x][y].tf == ' ')
	            board[x][y].tf = i + 'a' - 1;
	          else 
              if (board[x][y].tf != i + 'a' - 1)
	              board[x][y].tf = '*';
	        }
	      }
	      update_board(x,y,right);
      }
    }
  }
}

void check_game_over() {
  int tfnum, total[2], transports[2], inhabs[2];
  tteam team;
  struct stplanet *pplan;
  BOOL dead[2] = {FALSE,FALSE}, quit_game = game_over;
  UINT starnum;

  for (team = ENEMY; team <= PLAYER; team++) {
    transports[team] = 0;
    inhabs[team] = 0;
    for (tfnum = 1 ; tfnum<=MAX_FLEETS; tfnum++) {
      if (tf[team][tfnum].dest != 0)
        transports[team] += tf[team][tfnum].t;
    }
  }
  for (starnum = 1 ; starnum<=NUM_STARS; starnum++) {
    pplan = stars[starnum].first_planet;
    while (pplan != NIL) {
      switch (pplan->team) {
        case PLAYER:
          inhabs[PLAYER] += pplan->iu;
          break;
        case ENEMY:
          inhabs[ENEMY] += pplan->iu;
          break;
      }
      pplan = pplan->next;
    }
  }

  for (team = ENEMY ; team<=PLAYER; team++) {
    total[team] = inhabs[team] + transports[team];
    dead[team] = (total[team]==0);
  }
  if ((!dead[PLAYER]) && (!dead[ENEMY]) && (turn >= 40)) {
    dead[ENEMY]  = total[PLAYER] / total[ENEMY]  >= conf.overwhelm;
    dead[PLAYER] = total[ENEMY]  / total[PLAYER] >= conf.overwhelm;
  }

  game_over = (dead[PLAYER] || dead[ENEMY] || (turn>conf.num_turns) || quit_game);
  if (game_over) {
printf( "%d = (%d || %d || %d > %d || %d\n", game_over, dead[PLAYER], dead[ENEMY], turn, conf.num_turns, quit_game);
    clear_screen();
    display("*** Game over ***\n");
    display(fmt("Player: Population in transports:%3d", transports[PLAYER]));
    display(fmt("  IU's on colonies: %3d  TOTAL: %3d\n", inhabs[PLAYER], total[PLAYER]));
    putchar('\n');
    display(fmt("Enemy:  Population in transports:%3d", transports[ENEMY]));
    display(fmt("  IU's on colonies: %3d  TOTAL: %3d\n", inhabs[ENEMY], total[ENEMY]));
    if (total[ENEMY] > (int)(total[PLAYER] * conf.victory))
      printf("**** THE ENEMY HAS CONQUERED THE GALAXY ***\n");
    else
      if (total[PLAYER] > (int)(total[ENEMY] * conf.victory))
      printf("*** PLAYER WINS - YOU HAVE SAVED THE GALAXY! ***\n");
    else
      printf("*** DRAWN ***\n");

    get_char();
  }
}

void revolt(int starnum) {
  tplanet *pplan;

  if (col_stars[starnum][ENEMY]+col_stars[starnum][PLAYER] == 0)
    return;

  for (pplan = stars[starnum].first_planet; pplan; pplan = pplan->next) {
    if ((pplan->conquered) && (!any_bc(pplan->team, starnum))) {
      col_stars[starnum][pplan->team]--;
      col_stars[starnum][OTHER_PLAYER(pplan->team)]++;
      pplan->team = OTHER_PLAYER(pplan->team);
      pplan->conquered = FALSE;
      pplan->psee_capacity = pplan->capacity;
      on_board(stars[starnum].x,stars[starnum].y);
    }
  }
}

void invest() {
  int newborn;
  struct stplanet  *pplan;
  UINT starnum;

  production_year = 0;
  displayAt(INPVWTL,"$$$$$$$$$ Investment $$$$$$$$$");

//Grow Populations
  for (starnum = 1; starnum<=NUM_STARS; starnum++) {
    for (pplan = stars[starnum].first_planet; pplan; pplan=pplan->next) {
      if (pplan->esee_team == PLAYER && pplan->capacity > 10 && pplan->esee_def < 12)
         pplan->esee_def++;

      if ((pplan->team) != NONE) {
        newborn = (int)round( pplan->inhabitants
                              * growth_rate[pplan->team]
                              * (1-(pplan->inhabitants/pplan->capacity))
                            );
        if (pplan->conquered)
          newborn = newborn / 2;

        pplan->inhabitants= (pplan->inhabitants) + newborn;
        pplan->iu = pplan->iu + newborn;
      }
    }
  }

//Plan spending
  for (starnum = 1; starnum<=NUM_STARS; starnum++) {
    for (pplan = stars[starnum].first_planet; pplan; pplan=pplan->next) {
      if ((pplan->team) != NONE) {
        if (pplan->team==ENEMY)
          inv_enemy(stars[starnum].x, stars[starnum].y,pplan);
        else
          inv_player(stars[starnum].x, stars[starnum].y,pplan);
      }
    }
  }

  battle();
}
