/* Movement.c: Routines for moving fleets around */
#include <stdio.h>
#include "defs.h"
#include "machine.h"
#include "structs.h"
#include "vars.h"
#include "protos.h"

BOOL lose(int *ships, int typ, double percent) {
  int i,sleft;
  BOOL any_losses = FALSE;

  sleft = *ships;
  for (i = 1; i<=*ships; i++) {
    if (rnd(100)/100.0 > percent) {
      any_losses = TRUE;
      sleft = sleft - 1;
    }
  }
  if (sleft < *ships) {
    display(fmt(" %2d%c", *ships-sleft, typ));
    *ships = sleft;
  }

  return(any_losses);
}

void fleet_explore(int fleet) {
  int loss;
  double prob;

  left_line[20] = TRUE;
  clear_left();
  displayAt(1+INPVWTL+1,fmt("Task force %c exploring %c.    \n", dispTF(fleet),
	dispStar(tf[PLAYER][fleet].dest)));

  if (! conf.SafeExplore) {
    point(CURPOS+1);
    if (tf[PLAYER][fleet].t != 0)
      prob = (T_E_PROB + rnd(T_E_VAR) * tf[PLAYER][fleet].t) / 100.0;
    if (tf[PLAYER][fleet].s != 0)
      prob = (S_E_PROB + rnd(S_E_VAR) * tf[PLAYER][fleet].s) / 100.0;
    if (tf[PLAYER][fleet].c != 0)
      prob = (C_E_PROB + rnd(C_E_VAR) * tf[PLAYER][fleet].c) / 100.0;
    if (tf[PLAYER][fleet].b != 0)
      prob = (B_E_PROB + rnd(B_E_VAR) * tf[PLAYER][fleet].b) / 100.0;
    if (prob > 100) prob=100;

    loss = FALSE;
    loss |= lose(&tf[PLAYER][fleet].t, 't', prob);
    loss |= lose(&tf[PLAYER][fleet].s, 's', prob);
    loss |= lose(&tf[PLAYER][fleet].c, 'c', prob);
    loss |= lose(&tf[PLAYER][fleet].b, 'b', prob);
    if (!loss) 
      display("No ships");

    display(" destroyed.");

    // fool zero tf 
    tf[PLAYER][fleet].eta = 1;      
    zero_tf(PLAYER, fleet);
    // fool zero tf 
    tf[PLAYER][fleet].eta = 0;      

    left_line[23] = TRUE;
  }

  // Anything left? 
  if (tf[PLAYER][fleet].dest) {
    tf_stars[tf[PLAYER][fleet].dest][PLAYER]++;
    board[tf[PLAYER][fleet].x][tf[PLAYER][fleet].y].status=' ';
    update_board(tf[PLAYER][fleet].x,tf[PLAYER][fleet].y,left);
    stars[tf[PLAYER][fleet].dest].visit[PLAYER]=TRUE;
    clear_report_view();
    print_star(tf[PLAYER][fleet].dest);
  }
  pause();
}

void move_ships() {
  double ratio;
  int there,dx,dy,j;
  tteam tm;
  struct stplanet *pplanet;
  BOOL any;
  UINT i;

  // clear the board 
  for (i = 1; i<=MAX_FLEETS; i++) {
    if ((tf[PLAYER][i].dest != 0) && (tf[PLAYER][i].eta != 0)) {
      board[tf[PLAYER][i].x][tf[PLAYER][i].y].tf = ' ';
      update_board(tf[PLAYER][i].x,tf[PLAYER][i].y,right);
    }
  }

  // move ships of both teams 
  for (tm = ENEMY; tm < NONE; tm++) {
    for (i=1; i<=MAX_FLEETS; i++) {
      if (tf[tm][i].dest != 0 && tf[tm][i].eta != 0) {
	      tf[tm][i].eta--;

	      if (tm==PLAYER) {
	        dx = stars[tf[tm][i].dest].x;
	        dy = stars[tf[tm][i].dest].y;
	        ratio = 1.0 - ((double)tf[tm][i].eta / tf[tm][i].origeta);
	        tf[tm][i].x = tf[tm][i].xf + (int)round(ratio*(dx-tf[tm][i].xf));
	        tf[tm][i].y = tf[tm][i].yf + (int)round(ratio*(dy-tf[tm][i].yf));

          // Update for arrival 
	        if (tf[tm][i].eta == 0) {
	          if (!stars[tf[tm][i].dest].visit[tm])
	            fleet_explore(i);
            // Already knew this planet 
	          else
	            tf_stars[tf[tm][i].dest][tm]++;
            // Any survivors ? 
	          if (tf[tm][i].dest != 0) {
	            pplanet = stars[tf[tm][i].dest].first_planet;
	            while (pplanet != NIL) {
		            pplanet->psee_capacity = pplanet->capacity;
		            pplanet = pplanet->next;
	            }
	            player_arrivals[tf[tm][i].dest]=TRUE;
	          }
	        }
	      }
	      if (tm == ENEMY && tf[tm][i].eta == 0) {
	        pplanet=stars[tf[tm][i].dest].first_planet;
	        stars[tf[tm][i].dest].visit[ENEMY]=TRUE;
	        while (pplanet!=NIL) {
	          pplanet->esee_team = pplanet->team;
	          pplanet=pplanet->next;
	        }
	        if (tf_stars[tf[tm][i].dest][ENEMY]>0) {
	          for ( there = 1; 
                  (   tf[ENEMY][there].dest != tf[ENEMY][i].dest 
                   || tf[ENEMY][there].eta != 0 
                   || there == i 
                  ) && there < 26;
                  there++
                );
	          joinsilent(ENEMY,&tf[ENEMY][i],&tf[ENEMY][there]);
	        }
	        if (    tf_stars[tf[tm][i].dest][PLAYER]  > 0 
               || col_stars[tf[tm][i].dest][PLAYER] > 0
             )
	          enemy_arrivals[tf[tm][i].dest]=TRUE;
	        tf_stars[tf[tm][i].dest][tm]++;
	      }
      }
    }
  }

  // put the good guys on the board 
  for (i=1; i<=MAX_FLEETS; i++) {
    if (tf[PLAYER][i].dest != 0) {
      tf[PLAYER][i].blasting = FALSE;
      dx = tf[PLAYER][i].x;
      dy = tf[PLAYER][i].y;
      if (board[dx][dy].tf == ' ')
	      board[dx][dy].tf = dispTF(i);
      else 
        if (board[dx][dy].tf != dispTF(i))
	        board[dx][dy].tf='*';
      update_board(dx,dy,right);
    }
  }

  arrivalsShow(player_arrivals, "P Arrive: ", 3);
  arrivalsShow(enemy_arrivals, "E Arrive: ", 4);
  arrivalsShow(en_departures, "E Depart: ", 5);

  for (i = 1; i<=NUM_STARS; i++)
    revolt(i);
}

BOOL set_des(int tf_num) {
  int min_eta, dst, from_star;
  char istar;
  double r;
  UINT st_num; 

  // Cancelling previous orders 
  if (tf[PLAYER][tf_num].eta != 0) {
    tf[PLAYER][tf_num].eta = 0;

    from_star = board[tf[PLAYER][tf_num].x][tf[PLAYER][tf_num].y].star-'A'+1;
    tf[PLAYER][tf_num].dest = from_star;
    tf_stars[from_star][PLAYER]++;

    displayAt(1,++y_cursor,"(Cancelling previous orders)");
  }

  displayAt(1,++y_cursor,"        to star: ");

  istar = get_char();
  st_num = istar-'A'+1;
  display(fmt("%c",istar));

  if ((st_num < 0) || (st_num > NUM_STARS)) {
    error("  !illegal star");
    return(TRUE);
  }

  r = dist(st_num, tf[1][tf_num].dest);
  displayAt(1,++y_cursor,fmt("       distance: %5.1f", r));
  dst = (int)(r - 0.049 + 1);

  // Only scout can fly so far 
  if (    dst > range[PLAYER] 
       && (   tf[1][tf_num].b != 0 
           || tf[1][tf_num].c != 0 
           || tf[1][tf_num].t != 0
          )
     ) {
    error("  !maximum range is %2d", range[PLAYER]);
    return(TRUE);
  }

  if (r < 0.5) {
    displayAt(1,++y_cursor,"Tf remains at star.");
    return(TRUE);
  }

  min_eta = ((dst-1) / vel[PLAYER]) + 1;
  displayAt(1,++y_cursor,fmt("eta in %2d turns.", min_eta));

  tf_stars[tf[PLAYER][tf_num].dest][PLAYER]--;
  tf[PLAYER][tf_num].dest=st_num;
  tf[PLAYER][tf_num].eta = min_eta;
  tf[PLAYER][tf_num].origeta= tf[PLAYER][tf_num].eta;
  tf[PLAYER][tf_num].xf= tf[PLAYER][tf_num].x;
  tf[PLAYER][tf_num].yf=tf[PLAYER][tf_num].y;

  return(FALSE);
}
