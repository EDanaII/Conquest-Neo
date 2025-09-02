/* Enemy.c: Routines for the enemy PLAYER */
#include <stdio.h>
#include <stdlib.h>

#include "defs.h"
#include "machine.h"
#include "structs.h"
#include "vars.h"
#include "protos.h"

/* Return TRUE if we should all flee from this battle */
BOOL best_withdraw_plan(int starnum, double enodds) {
  tplanet *pplanet;
  int size;
  tteam team;

  team = NONE;
  size = 0;
  for (pplanet = stars[starnum].first_planet; pplanet; pplanet = pplanet->next) {
    if ( pplanet->capacity > size ) {
      size = pplanet->capacity;
      team = pplanet->team;
    }
  }

  if (    (enodds < 0.70 && size < 30) 
       || (enodds < 0.50 && team == PLAYER) 
       || (enodds < 0.30 && size < 60) 
       || enodds < 0.20
     )
    return(TRUE);
  else
    return(FALSE);
}

/* Enemy attack at a planet */
void enemy_attack(int starnum) {
  int attack_factors,def_factors;
  double odds,best_score;
  struct stplanet *pplanet,*best_planet;
  int en_tf,i;
  BOOL first[8];

  for (i = 1 ; i<=7; i++)
    first[i] = TRUE;

  /*
  en_tf = 1;
  while (    tf[ENEMY][en_tf].dest != starnum 
          || tf[ENEMY][en_tf].eta  !=  0 
        )
    en_tf++;
  */
  en_tf = GetTaskForceAtStar(ENEMY, starnum);

  do {
    attack_factors = tf[ENEMY][en_tf].c + 6*tf[ENEMY][en_tf].b;

    best_planet = NIL;
    best_score = 1000;

    /* Calculate the best one to attack */
    for ( pplanet = stars[starnum].first_planet; 
          pplanet != NULL;
	        pplanet = pplanet->next 
        ) {
      if (pplanet->team == PLAYER) {
	      def_factors = pplanet->esee_def;

	      odds = (double)def_factors / attack_factors;

      	if (pplanet->capacity > 30)
      	  odds = (odds - 2) * pplanet->capacity;
      	else
      	  odds = (odds - 1.5) * pplanet->capacity;

      	if (odds < best_score) {
      	  best_score = odds;
      	  best_planet = pplanet;
      	}
      }
    }

    /* Found one that was worth attacking */
    if (best_score < 0) {
      clear_left();
      display(conf.PenAttack);
      displayAt(INPVWTL, fmt(">>>>>> Enemy Attacks %c%d <<<<<<", dispStar(starnum),best_planet->number) );
      display(conf.PenNormal);
      clear_report_view();
      print_star(starnum);
      board[stars[starnum].x][ stars[starnum].y].status = '!';
      update_board(stars[starnum].x, stars[starnum].y, left);
      pause();

      fire_salvo(ENEMY, en_tf, best_planet, first[best_planet->number]);

      first[best_planet->number] = FALSE;
      zero_tf(ENEMY,en_tf);
      best_planet->esee_def = best_planet->mb + 6*best_planet->amb;

      clear_report_view();
      print_star(starnum);
      board[stars[starnum].x][ stars[starnum].y].status = '!';
      update_board(stars[starnum].x, stars[starnum].y, left);
      pause();
    }
  }
  while ((best_score < 0) && any_bc(ENEMY, starnum));
  on_board(stars[starnum].x, stars[starnum].y);

  clear_report_view();
  print_star(starnum);
  revolt(starnum);
}

void depart(int starnum) {
  if ((tf_stars[starnum][PLAYER] + col_stars[starnum][PLAYER]) > 0)
    en_departures[starnum]=TRUE;
}

/* How much do we care about moving ships to this planet */
int eval_bc_col(struct stplanet *planet) {
  int result;

  if (! stars[planet->pstar].visit[ENEMY])
    result = 600;
  else {
    switch (planet->esee_team) {
      case NONE:
        result=100;
        break;
     case ENEMY:
        if (planet->conquered)
          result = 1000;
        else {
          if (    (6*planet->amb + planet->mb) <= (planet->iu/15) 
               && (planet->iu >= MB_COST)
             )
            result = 300;
          else
            result = 0;
        }

        if (planet->amb >= 4)
          result = result - 250;
        break;

      case PLAYER:
        if (planet->conquered)
          result = 400;
        else
          result = 200;
        break;
    }

    if ((planet->capacity < 40) && (planet->iu < 15))
      result = result - 100;
  }
  result = result + rnd(20);
  return (result);
}

int eval_t_col(struct stplanet *planet, double range) {
  int result;

  if (!stars[planet->pstar].visit[ENEMY])
    result=60;
  else {
    switch (planet->esee_team) {
      case NONE:
        result = 40;
        break;
      case ENEMY:
        result= 30;
        break;
      case PLAYER:
        result = 0;
    }
    if (    planet->esee_team != PLAYER 
         && planet->capacity-planet->inhabitants > (int)(40-(turn/2))
       )
      result = result + 40;
  }

  result -= (int)(2*range + .5);

  return(result);
}

void inputmach() {
  int tfnum,starnum;
  double slist[MAX_NUM_STARS+1];

  for (tfnum = 1 ; tfnum<=MAX_FLEETS; tfnum++) {
    if ((tf[ENEMY][tfnum].eta==0) && (tf[ENEMY][tfnum].dest!=0)) {
      starnum = tf[ENEMY][tfnum].dest;
      get_stars(starnum,slist);
      send_scouts(slist,&tf[ENEMY][tfnum]);
      send_transports(slist,&tf[ENEMY][tfnum]);
      move_bc(&tf[ENEMY][tfnum],slist);
      zero_tf(ENEMY,tfnum);
    }
  }
}

void move_bc(struct sttf *task, double slist[]) {
  int best_star, top_score, score, factors;
  UINT starnum; 
  struct stplanet *pplanet, *best_planet;

  if (task->b > 0 || task->c > 0) {
    for (starnum = 1; starnum <= NUM_STARS; starnum++) {
      if (slist[starnum] > 0) {
	      best_star = starnum;
	      break;
      }
    }

    best_planet = NULL;
    top_score = -1000;

    for (starnum = 1; starnum<=NUM_STARS; starnum++) {
      if (slist[starnum] > 0 || starnum==task->dest) {
	      pplanet = stars[starnum].first_planet;
	      while (pplanet != NULL ) {
	        score = eval_bc_col(pplanet) ;
	        if (starnum == task->dest)
	          score += 250;

	        if (tf_stars[starnum][ENEMY] > 0)
	          score -= 150;

	        if (score > top_score) {
	          top_score = score;
	          best_planet = pplanet;
	          best_star = starnum;
	        }
	        pplanet = pplanet->next;
	      }
      }
    }
    // stay put 
    if (best_star == task->dest) { 
      if (    best_planet != NULL 
           && best_planet->team == ENEMY 
           && best_planet->conquered
         ) {
        // Blast it! 
	      if (best_planet->iu < 20) {
	        factors = weapons[ENEMY] * ((task->c*C_GUNS)+(task->b*B_GUNS));
	        factors = min(factors, 4 * best_planet->inhabitants);
	        blast(best_planet,factors);
	        if (    tf_stars[best_planet->pstar][PLAYER]  > 0 
               || col_stars[best_planet->pstar][PLAYER] > 0
             )
	          best_planet->psee_capacity = best_planet->capacity;
	      }
        // Decide whether to split
	      else { 
	        if (    ((task->b > 3 || task->c > 3) && rnd(4)==4) 
               || task->b > 8
             )
	          wander_bc(task,slist);
	      }
      }
    }
    // move
    else { 
      tf_stars[task->dest][ENEMY]--;
      depart(task->dest);
      task->dest = best_star;
      task->eta = (int)((slist[best_star]-0.01)/vel[ENEMY])+1;
    }
  }
}

void send_transports(double slist[], struct sttf *task) {
  int new_tf, to_land, sec_star, sec_score, best_star, top_score,score;
  UINT starnum;
  struct stplanet *pplan, *best_plan;
  int trash;

  if (task->t > 0) {
    best_star = 0;
    sec_star = 0;
    sec_score = -11000;
    top_score = -10000;
    best_plan = NIL;

    for (starnum = 1; starnum<=NUM_STARS; starnum++) {
      if (slist[starnum] > 0 || starnum == task->dest) {
	      pplan = stars[starnum].first_planet;
	      while (pplan != NIL) {
	        score = eval_t_col(pplan,slist[starnum]);
	        if (score > sec_score) {
	          if (score > top_score) {
	            sec_score = top_score;
	            sec_star = best_star;
	            top_score = score;
	            best_star = starnum;
	            best_plan = pplan;
	          }
	          else {
	            sec_score = score;
	            sec_star = starnum;
	          }
	        }
	        pplan = pplan->next;
	      }
      }
    } // End For 

    // Land 
    if (best_star == task->dest) { 
      if (tf_stars[best_star][PLAYER] == 0 && best_plan->team != PLAYER) {
	      trash = (best_plan->capacity - best_plan->inhabitants)/3;
	      to_land = min(task->t, trash);

	      if (to_land > 0) {
	        if (best_plan->inhabitants == 0) {
	          best_plan->team = ENEMY;
	          best_plan->esee_team = ENEMY;
	          col_stars[best_star][ENEMY]++;
	        }

	        best_plan->inhabitants += to_land;
	        best_plan->iu += to_land;
	        task->t -= to_land;
	        send_transports(slist, task);
	      }
      }
    }
    // Move 
    else { 
      // First send half to the best star, if two stars 
      if (task->t >= 10 && sec_star > 0) { 
	      new_tf = get_tf(ENEMY,task->dest);
	      tf[ENEMY][new_tf].t = task->t/2;
	      task->t = task->t-tf[ENEMY][new_tf].t;

	      if ((task->c > 0) && (!underdefended(task->dest))) {
	        tf[ENEMY][new_tf].c = 1;
	        task->c--;
	      }

	      send_t_tf(&tf[ENEMY][new_tf],slist,best_star);
	      best_star = sec_star;
      }

      new_tf = get_tf(ENEMY,task->dest);
      tf[ENEMY][new_tf].t = task->t;
      task->t = 0;

      if (task->c > 0 && ! underdefended(task->dest)) {
	      tf[ENEMY][new_tf].c = 1;
	      task->c--;
      }
      send_t_tf(&tf[ENEMY][new_tf], slist, best_star);
    }
  }
}

void send_t_tf(struct sttf *task, double slist[], int dest_star) {
  depart(task->dest);
  task->dest = dest_star;
  task->eta = (int)((slist[dest_star]-0.01)/vel[ENEMY])+1;
}

void send_scouts(double slist[], struct sttf *task) {
  int dest, new_tf, doind, doable[MAX_NUM_STARS+1];
  UINT j;

  if (task->s > 0) {
    doind=0;
    for (j = 1; j<=NUM_STARS; j++) {
      if ( ! stars[j].visit[ENEMY] && slist[j] > 0) {
	      doind++;
	      doable[doind]=j;
      }
    }

    while ((doind>0) && (task->s > 0)) {
      new_tf = get_tf(ENEMY,task->dest);
      tf[ENEMY][new_tf].s = 1;
      dest = rnd(doind);

      tf[ENEMY][new_tf].dest = doable[dest];
      tf[ENEMY][new_tf].eta = (int)((slist[doable[dest]]-0.01)/vel[ENEMY])+1;
      depart(task->dest);
      doable[dest] = doable[doind];
      doind--;
      task->s--;
    }

    while (task->s > 0) {
      do {
	      dest = rnd(NUM_STARS);
      }
      while (slist[dest] <= 0);

      new_tf = get_tf(ENEMY,task->dest);
      tf[ENEMY][new_tf].s = 1;
      tf[ENEMY][new_tf].dest = dest;
      tf[ENEMY][new_tf].eta = (int)((slist[dest]-0.01)/vel[ENEMY])+1;
      depart(task->dest);
      task->s--;
    }
  }
}

BOOL underdefended(int starnum) {
  struct stplanet *pplanet;
  BOOL result;
  result = FALSE;

  pplanet = stars[starnum].first_planet;
  while ((pplanet != NULL) && (!result)) {
    if ((pplanet->team==ENEMY) && (pplanet->iu > 10) && ((6*pplanet->amb+pplanet->mb) < round(pplanet->iu/15.0)))
      return(TRUE);
    pplanet = pplanet->next;
  }
  return(FALSE);
}

void wander_bc(struct sttf *task, double slist[]) {
  int ships,count,dest,new_tf;
  UINT i;

  if ((task->b > 1) || (task->c > 1)) {
    count = 0;
    for (i = 1 ; i<=NUM_STARS; i++) {
      if (slist[i] != 0)
	      count++;
    }
    if (count > 0) {
      dest = rnd(count);
      for (count = 0, i = 0; count != dest; i++)
	      if (slist[i]>0) 
          count++;

      new_tf = get_tf(ENEMY, task->dest);

      ships = task->b/2;
      tf[ENEMY][new_tf].b = ships;
      task->b = task->b - ships;

      ships = task->c/2;
      tf[ENEMY][new_tf].c = ships;
      task->c = task->c - ships;

      if (task->t > 3) {
	      tf[ENEMY][new_tf].t = 2;
	      task->t = task->t-2;
      }

      tf[ENEMY][new_tf].dest = i;
      tf[ENEMY][new_tf].eta = (int)((slist[i]-0.01)/vel[ENEMY])+1;
      depart(task->dest);
    }
  }
}

void inv_enemy(int x, int y, struct stplanet *planet) {
  int inv_amount,balance,min_mb,transports,new_tf;

  balance = planet->iu;
  if (tf_stars[planet->pstar][ENEMY] == 0) {
    new_tf = get_tf(ENEMY,planet->pstar);
    tf_stars[planet->pstar][ENEMY]=1;
  }
  // use present tf
  else { // Subscript out of bounds 242...
    /*
    new_tf = 1;
    while ((tf[ENEMY][new_tf].dest != planet->pstar || tf[ENEMY][new_tf].eta != 0) && new_tf < 26)
      new_tf++;
    */
    new_tf = GetTaskForceAtStar(ENEMY, planet->pstar);
  }

  // Build a minimum of missile bases 
  min_mb = planet->capacity/20;
  while ((planet->amb == 0) && (!planet->conquered) && (planet->mb < min_mb) && (balance >= MB_COST)) {
    balance = balance - MB_COST;
    planet->mb++;
  }

  // Build an amb more 
  if ((balance >= B_COST) && (planet->amb > 1) && (rnd(5) != 1) && (rnd(7) <= planet->amb+3)) {
    balance = balance - B_COST;
    tf[ENEMY][new_tf].b++;
  }

  /* Maybe build the first one */
  if ((balance >= AMB_COST) && (!planet->conquered) && ((planet->amb < 4) || (rnd(2)==2))) {
    balance = balance - AMB_COST;
    planet->amb++;
  }

  while (balance >= 9) {
    switch (rnd(12)) {
      case 1: /* Research something */
      case 2:
        research(ENEMY, en_research, 8);
        balance = balance-8;
        break;
      case 3:
      case 4:
      case 10: /* Build some small things */
      if (balance >= C_COST) {
        balance = balance - C_COST;
        tf[ENEMY][new_tf].c++;
      }
      else 
        if ((!planet->conquered) && (balance >= MB_COST)) {
          balance = balance - MB_COST;
          planet->mb++;
        }
        else {
          balance = balance - 9;
          research(ENEMY, en_research, 9);
        }
      break;

      case 11: /* Build some transports */
      case 12:
        // No t's - invest instead 
        if (((double)planet->inhabitants/planet->capacity < 0.6) || ((planet->capacity >= B_COST/IU_RATIO) && (planet->iu < B_COST+10))) { 
          inv_amount = min(3, planet->inhabitants*IU_RATIO - planet->iu);
          balance = balance - inv_amount*I_COST;
          planet->iu = planet->iu + inv_amount;
        }
        // build transports 
        else 
        if(!(planet->conquered)) {
          transports = min(rnd(2)+6, planet->inhabitants-1);
          if (planet->iu > B_COST)
            transports = min(transports,planet->iu - B_COST);

          balance = balance - transports;
          planet->inhabitants = planet->inhabitants - transports;
          planet->iu = min(planet->iu-transports,planet->inhabitants*IU_RATIO);
          tf[ENEMY][new_tf].t = tf[ENEMY][new_tf].t + transports;
        }
        break;
      default: /* Just invest some */
        inv_amount = min(3,planet->inhabitants*IU_RATIO - planet->iu);
        balance = balance - I_COST*inv_amount;
        planet->iu = planet->iu + inv_amount;
        break;

    } // End of switch 
  } // End of while 
  zero_tf(ENEMY,new_tf);
  research(ENEMY,en_research,balance);
}
