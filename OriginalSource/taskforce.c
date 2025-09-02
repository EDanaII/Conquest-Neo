/* Taskforce.c: Routines for taskforces */

#include <stdio.h>
#include "defs.h"
#include "machine.h"
#include "structs.h"
#include "vars.h"
#include "protos.h"

void make_tf() {
  int tf_num, new_tf, starnum;
  char tmpChar;

  displayAt(1+INPVWTL+1,"New tf- from tf: ");
  display(fmt("%c",tmpChar=get_char()));
  tf_num = tmpChar-'A'+1;
  clear_left();

  if (    tf_num < 1 
       || tf_num>MAX_FLEETS 
       || tf[PLAYER][tf_num].eta!=0 
       || tf[PLAYER][tf_num].dest==0 
     ) {
    error("  !illegal tf");
    return;
  }

  if (tf[PLAYER][tf_num].blasting) {
    error(" !Tf is blasting a planet     ");
    return;
  }

  starnum = tf[PLAYER][tf_num].dest;

  point(10+INPVWTL+2);
  new_tf = split_tf(tf_num);

  clear_left();
  clear_report_view();
  print_star(starnum);
}

int split_tf(int tf_num) {
  int new_tf;
  char ships; 
  int x,y,n_ships;
  char iline[81];

  new_tf = get_tf(PLAYER,tf[PLAYER][tf_num].dest);

  if (new_tf==0) {
    error( "! Taskforce limit reached!" );
    return(0);
  }
   
  tf_stars[tf[PLAYER][tf_num].dest][PLAYER]++;

  displayAt(CURPOS," ships:");
  point(8+CURPOS);

  get_line(iline);
  ships = get_token(iline,&n_ships);

  /* Split entire fleet */
  if (ships==' ') {
    tf[PLAYER][new_tf].s=tf[PLAYER][tf_num].s;
    tf[PLAYER][new_tf].t=tf[PLAYER][tf_num].t;
    tf[PLAYER][new_tf].c=tf[PLAYER][tf_num].c;
    tf[PLAYER][new_tf].b=tf[PLAYER][tf_num].b;
    tf[PLAYER][tf_num].s=0;
    tf[PLAYER][tf_num].t=0;
    tf[PLAYER][tf_num].c=0;
    tf[PLAYER][tf_num].b=0;
  } 
  else {
    do {
      switch (ships) {
        case 'T': 
          if (tf[PLAYER][tf_num].t < n_ships)
            n_ships= tf[PLAYER][tf_num].t;
          tf[PLAYER][tf_num].t=tf[PLAYER][tf_num].t-n_ships;
          tf[PLAYER][new_tf].t=tf[PLAYER][new_tf].t+n_ships;
          break;
        case 'S': 
          if (tf[PLAYER][tf_num].s < n_ships)
            n_ships=tf[PLAYER][tf_num].s;
          tf[PLAYER][tf_num].s=tf[PLAYER][tf_num].s-n_ships;
          tf[PLAYER][new_tf].s=tf[PLAYER][new_tf].s+n_ships;
          break;
        case 'C': 
          if (tf[PLAYER][tf_num].c < n_ships)
            n_ships=tf[PLAYER][tf_num].c;
          tf[PLAYER][tf_num].c=tf[PLAYER][tf_num].c-n_ships;
          tf[PLAYER][new_tf].c=tf[PLAYER][new_tf].c+n_ships;
          break;
        case 'B': 
          if (tf[PLAYER][tf_num].b < n_ships)
            n_ships=tf[PLAYER][tf_num].b;
          tf[PLAYER][tf_num].b=tf[PLAYER][tf_num].b-n_ships;
          tf[PLAYER][new_tf].b=tf[PLAYER][new_tf].b+n_ships;
          break;
        default:
          error("  ! Illegal field %c",ships);
      } // End of switch 
      ships = get_token(iline,&n_ships);
    } 
    while (ships != ' ');
  }

  x = tf[PLAYER][tf_num].x; 
  y = tf[PLAYER][tf_num].y;

  zero_tf(PLAYER,tf_num);
  zero_tf(PLAYER,new_tf);

  on_board(x,y);

  return(new_tf);
}

void join_tf() {
  char tf1,tf2; 
  int tf1n,tf2n,value;
  char iline[81];

  displayAt(1+INPVWTL+1,"Join tfs:");
  get_line(iline);
  clear_left();
  tf1 = get_token(iline,&value);
  tf1n=tf1-'A'+1;

  if ((tf1n<1) || (tf1n>MAX_FLEETS)) {
    error("  ! illegal tf %c",tf1);
    return;
  }
  if ((tf[PLAYER][tf1n].eta)>0) {
    error("  !tf%c is not in normal space ", tf1);
    return;
  }
  if (tf[PLAYER][tf1n].dest==0) {
    error("  !nonexistent tf");
    return;
  }
  if (tf[PLAYER][tf1n].blasting) {
    error("  !Tf is blasting a planet    ");
    return;
  }

  while ((tf2 = get_token(iline, &value)) != ' ') {
    tf2n = tf2-'A'+1;
    if (tf2n<1 || tf2n>MAX_FLEETS) {
      error("  !illegal tf %c",tf2);
      continue;
    }
    if (tf2n == tf1n) {
      error("!Duplicate tf %c",tf2);
      continue;
    }
    else 
      if (tf[PLAYER][tf2n].dest == 0) {
      error("!Nonexistant TF%c",tf2);
      continue;
    }
    if (tf[PLAYER][tf2n].x != tf[PLAYER][tf1n].x || tf[PLAYER][tf2n].y!= tf[PLAYER][tf2n].y) {
      error("  !tf%c bad location", tf2);
      continue;
    }
    else 
      if (tf[PLAYER][tf2n].eta != 0) {
      error("  !tf%c is not in normal space ", tf2);
      continue;
    }
    if (tf[PLAYER][tf2n].blasting) {
      error(" !Tf%c is blasting a planet    ", tf2);
      continue;
    }
    joinsilent(PLAYER,&tf[PLAYER][tf1n],&tf[PLAYER][tf2n]);
  }
  on_board(tf[PLAYER][tf1n].x,tf[PLAYER][tf1n].y);
  clear_report_view();
  print_star(tf[PLAYER][tf1n].dest);
}

int get_tf(tteam tm, int starnum) {
  int i;

  for (i=1; (i<=MAX_FLEETS) && (tf[tm][i].dest != 0); i++);
  if (i>MAX_FLEETS) return(0);

  tf[tm][i].s = 0;
  tf[tm][i].t = 0;
  tf[tm][i].c = 0;
  tf[tm][i].b = 0;
  tf[tm][i].eta = 0;
  tf[tm][i].x = stars[starnum].x;
  tf[tm][i].y = stars[starnum].y;
  tf[tm][i].xf = tf[tm][i].x;
  tf[tm][i].yf = tf[tm][i].y;
  tf[tm][i].dest = starnum;
  tf[tm][i].origeta = 0;
  tf[tm][i].blasting = FALSE;
  
  return(i);
}

void joinsilent(tteam team, struct sttf *parent, struct sttf *child) {
  parent->t = parent->t + child->t;
  parent->s = parent->s + child->s;
  parent->c = parent->c + child->c;
  parent->b = parent->b + child->b;

  if ((parent->dest!=0) && (child->dest!=0))
    tf_stars[parent->dest][team]--;

  child->dest = 0;
}

int GetTaskForceAtStar(tteam prmTeam, int prmStar) {
  int tfno = 0;
  while (    (tf[prmTeam][tfno].dest != prmStar || tf[prmTeam][tfno].eta != 0)
          && tfno < MAX_FLEETS
        )
    tfno++;
  return tfno;
}
