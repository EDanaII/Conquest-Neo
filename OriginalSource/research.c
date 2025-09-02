/* Research.c: Procedures pertaining to research */

#include <stdio.h>
#include <stdlib.h>

#include "defs.h"
#include "machine.h"
#include "structs.h"
#include "vars.h"
#include "protos.h"

void ressum() {
  char key;
  int value;
  char iline[81];

  displayAt(1+INPVWTL+1,"Research field(s):");
  get_line(iline);
  key = get_token(iline,&value);
  clear_left();
  if (key==' ') {
    print_res('R');
    print_res('V');
    print_res('W');
  } 
  else {
    do {
      print_res(key);
      key = get_token(iline,&value);
    } 
    while (key != ' ');
  }
}

void print_res(char field) {
  displayAt(RESVWTL,"------- Research ------" );
  switch (field) {
   case 'V':
    displayAt(RESVWTL+1,fmt("V:%2d",vel[PLAYER]));
    if (vel[PLAYER] < MAX_VELOCITY)
      display(fmt(" res: %3d need:%4d",vel_working[PLAYER], vel_req[vel[PLAYER]+1]));
    else 
      display("                   ");
    break;
   case 'R':
    displayAt(RESVWTL+2,fmt("R:%2d",range[PLAYER]));
    if (range[PLAYER] < MAX_RANGE)
      display(fmt(" res: %3d need:%4d",ran_working[PLAYER], ran_req[range[PLAYER]+1]));
    else 
      display("                   ");
    break;
   case 'W':
    displayAt(RESVWTL+3,fmt("W:%2d",weapons[PLAYER]));
    if (weapons[PLAYER] < MAX_WEAPONS)
      display(fmt(" res: %3d need:%4d",weap_working[PLAYER], weap_req[weapons[PLAYER]+1]));
    else 
      display("                   ");
    break;
  } 
}

/* Research up to next breakthrough. Return amount used */
int research_limited(int team, char field, int max_amt) {
  int used = 0;

  switch (field) {
    case 'W':
      if (weapons[team]<MAX_WEAPONS) {
        used = min(max_amt, weap_req[weapons[team]+1]-weap_working[team]);
        weap_working[team] += used;
        if (weap_working[team] >= weap_req[weapons[team]+1]) {
          weapons[team]++;
          if (team==ENEMY) {
            new_research();
            field = en_research;
          }
          weap_working[team]=0;
        }
      }
      break;
    case 'R':
      if (range[team] < MAX_RANGE) {
        used = min(max_amt, ran_req[range[team]+1]-ran_working[team]);
        ran_working[team] += used;
        if (ran_working[team] >= ran_req[range[team]+1]) {
          range[team]++;
          if (team==ENEMY) {
            new_research();
            field = en_research;
          }
          ran_working[team]=0;
        }
      }
      break;
    case 'V':
      if (vel[team] < MAX_VELOCITY) {
        used = min(max_amt, vel_req[vel[team]+1]-vel_working[team]);
        vel_working[team] += used;
        if (vel_working[team] >= vel_req[vel[team]+1]) {
          vel[team]++;
          vel_working[team]=0;
        }
      }
      break;
    default: 
      display(fmt("error!!! in research field %c\n", field));
  } 
  return(used);
}

/* Maybe automagically redraw research here? */
void research(int team, char field, int amt) {
  int old_amt;

  do {
    old_amt = amt;
    amt -= research_limited(team, field, amt);
    if (team==ENEMY) {
      new_research();
      field = en_research;
    }
    // How stupid - invested beyond the limit 
    if (old_amt == amt)
      amt = 0;
  }
  while (amt > 0);
}

void new_research() {
  if (weapons[ENEMY] < MAX_WEAPONS) {
    if (weapons[PLAYER] - weapons[ENEMY] + 5 > rnd(10)) {
      en_research = 'W';
      return;
    }
  }
  
  if (vel[ENEMY] < MAX_VELOCITY) {
    if (range[ENEMY] / vel[ENEMY] > rnd(3)) {
      en_research = 'V';
      return;
    }
  }
  
  en_research = 'R';
}
