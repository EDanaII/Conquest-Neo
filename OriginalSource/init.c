/* Init.c: Initializations routines. */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

#include "defs.h"
#include "machine.h"
#include "structs.h"
#include "vars.h"
#include "protos.h"

void setup_term() {
//  char *termname;
//  struct sgttyb ttyinfo;

//system( "setenv home `type parameters`" );
//printf( "And our survey sayeth... %s: ", getenv("home") );

/* Some kind of TERM set -- ain't needed...
  if ((termname = getenv("TERM")) != NULL) {
    printf("Terminal type is %s.\n", termname);
    if (termname == "xterm") terminal_type = xterm;
    if (termname == "vt100") terminal_type = vt100;
    if (termname == "vt52") terminal_type = vt52;
    if (termname == "adm3") terminal_type = adm3;
    if (termname == "vis400") terminal_type = vis400;
    if (termname == "hpterm") terminal_type = hpterm;
    if (termname == "vi") terminal_type = vi; // WHY???
    if (termname == "concept") terminal_type = concept;
    if (termname == "hardcopy") terminal_type = hardcopy;
    if (terminal_type == unknown) {
      printf("Unknown terminal type %s - using vt100.\n", termname);
      terminal_type = vt100;
    }
  }
  else */
    terminal_type = vt100; // For systems without $TERM

//  ioctl(fileno(stdin), TIOCGETP, &ttyinfo);
//  ttyinfo.sg_flags |= RAW;
//  ioctl(fileno(stdin), TIOCSETP, &ttyinfo);
//  setbuf(stdin, NULL);
}

void startup(int argc, char *argv[]) {
  printf("\n *** CONQUEST *** \n");

  configure(argc, argv);
  setup_term();
  welcome();

  get_char();
  printf(SCRCLEAR);

  srand((UINT)time(0));

  initconst();

  initmach();

  init_player();

  // Put player on board 
  clear_report_view();
  print_star(tf[PLAYER][1].dest);

  // And fight if in same place 
  if (tf[ENEMY][1].dest == tf[PLAYER][1].dest) {
    clear_left();
    battle();
  }
}

void assign_planets(tstar *ustar, int starnum) {
  int i1, nplanets;
  tplanet *pplanet;

  nplanets=rnd(4)-2;
  if (nplanets < 0)
    nplanets = 1;

  if (nplanets!=0) {
    if ((pplanet = (tplanet *)calloc(1, sizeof(tplanet))) == NULL) {
      printf("Out of memory. Sorry.\n");
      exit(1);
    }
    ustar->first_planet=pplanet;
    for (i1=1 ; i1<=nplanets; i1++) {
      pplanet->number = rnd(2) + (2*i1) - 2;
      if (rnd(4)>2) 
        pplanet->capacity = 10 * (rnd(4) + 2); 
      else 
        pplanet->capacity = 5 * rnd(5);
      pplanet->psee_capacity = pplanet->capacity;
      pplanet->esee_def = 1;
      pplanet->esee_team = NONE;
      pplanet->pstar = starnum;
      pplanet->team = NONE;
      if (i1 < nplanets) {
	      if ((pplanet->next = (tplanet *)calloc(1, sizeof(tplanet))) == NULL) {
	        printf("Out of memory. Sorry.\n");
	        exit(1);
	      }
	      pplanet=pplanet->next;
      }
    }
  }
}

void initconst() {
  int x,y;
  UINT i1;

  // init stars 
  for (i1 = 1; i1<=NUM_STARS; i1++) {
    do {
      x=rnd(MAX_BOARD_SIZE);
      y=rnd(MAX_BOARD_SIZE);
    }
    while (board[x][y].star != '.');
    stars[i1].x=x;
    stars[i1].y=y;

    board[x][y].star = 'A'+i1-1;
    board[x][y].status = '?';
    assign_planets(&stars[i1],i1);
  }

  // initialize task forces 
  tf[ENEMY][1].x = 1;
  tf[ENEMY][1].y = 1;

  for (i1 = ENEMY ; i1<=PLAYER; i1++ )
    tf[i1][1].t = conf.transports;

  // Small cheats for the enemy 
  switch (rnd(3)) {
    case 1: weapons[ENEMY] = rnd(4) + 2;
      break;
    case 2: vel[ENEMY] = rnd(3);
      break;
    case 3: growth_rate[ENEMY] = (double)(rnd(4) + 3) / 10.0;
      break;
  }

}

void init_player() {
  char str, key;
  int balance,cost,amt;
  char iline[81];
  UINT star_number;

  // Startup 
  printmap();
  displayAt(INPVWTL,"******* Initialization *******");

  if (conf.capacity) {
    star_number = pick_planet(conf.capacity);
  }
  else {
    do {
      displayAt(INPVWTL+1,"start at star?\n     ");
      str = get_char();
      point(INPVWTL+1);
      star_number= str-'A'+1;
      display(fmt("%c",star_number));
    }
    while ((star_number < 1) || (star_number > NUM_STARS));
  }

  tf[PLAYER][1].x=stars[star_number].x;
  tf[PLAYER][1].y=stars[star_number].y;
  tf_stars[star_number][PLAYER]=1;
  tf[PLAYER][1].dest = star_number;

  displayAt(INPVWTL+2,"choose your initial fleet.");
  displayAt(INPVWTL+3,fmt("you have %d transports", conf.transports));
  displayAt(INPVWTL+4,fmt(" && %d units to spend", conf.money));
  displayAt(INPVWTL+5,"on ships or research.");

  print_res('V');
  print_res('R');
  print_res('W');

  balance = conf.money;
  do {
    point(INPVWTL+2);
    print_tf(1);
    displayAt(INPVWTL+1,fmt("%3d?                          ", balance));
    point(5+INPVWTL+1);
    if (strlen(conf.invest)==0)
      get_line(iline);
    else {
      strcpy(iline,conf.invest);
      strcpy(conf.invest,"");
    }
    do {
      cost = 0;
      key = get_token(iline,&amt);
      switch (key) {
        case 'C':
          cost = amt*C_COST;
          // Overflow 
          if (cost < amt) 
            break; 
          if (cost <= balance)
            tf[PLAYER][1].c=tf[PLAYER][1].c+amt;
          break;
        case 'S':
          cost = amt*S_COST;
          // Overflow 
          if (cost < amt) 
            break; 
          if (cost <= balance)
            tf[PLAYER][1].s=tf[PLAYER][1].s+amt;
          break;
        case 'B':
          cost = amt*B_COST;
          // Overflow 
          if (cost < amt) 
            break; 
          if (cost <= balance)
            tf[PLAYER][1].b=tf[PLAYER][1].b+amt;
          break;
        case 'H':
          help(0);
          break;
        case 'W':
        case 'V':
        case 'R':
          cost = amt;
          if (cost <= balance) {
            research(PLAYER,key,amt);
            print_res(key);
          }
          break;
        case ' ':
          break;
        case '>':
          displayAt(INPVWTL+1,">?      ");
          point(2+INPVWTL+1);
          key = get_char();
          switch ( key ) {
            case 'M':
              printmap();
              break;
            case 'R':
              ressum();
              break;
            default:
              error(" !Only M,R during initialize");
          } // end of 2nd switch 
          break;
        default:
          error( " !Illegal field %c",key);
      } // end of 1st switch 
      if (cost <= balance)
        balance = balance - cost;
      else {
        error("  !can't afford %d%c",amt, key);
      }
    }
    while (key != ' ');
  }
  while (balance >0);
  stars[star_number].visit[PLAYER]=TRUE;
  board[stars[star_number].x][stars[star_number].y].tf = 'a';
  board[stars[star_number].x][stars[star_number].y].status=' ';
  on_board(stars[star_number].x,stars[star_number].y);
  point(33,20);
}

void initmach() {
  int res_amt, maxx, start_star, count;
  double slist[MAX_NUM_STARS+1];
  UINT starnum; 

  en_research = 'V';
  tf[ENEMY][1].c = 1;
  tf[ENEMY][1].s = 2;
  res_amt = 2;
  research(ENEMY, en_research, res_amt);

  // Found out the best place to start (most stars reachable) 
  maxx = 0;
  start_star = 0;
  for (starnum = 1; starnum <= NUM_STARS; starnum++) {
    count = get_stars(starnum, slist);
    count += rnd(5);
    if (count > maxx) {
      maxx = count;
      start_star = starnum;
    }
  }

  tf[ENEMY][1].dest = start_star;
  tf[ENEMY][1].x = stars[start_star].x;
  tf[ENEMY][1].y = stars[start_star].y;
  stars[start_star].visit[ENEMY] = TRUE;
  tf_stars[start_star][ENEMY] = 1;
}

int pick_planet(int prmCapacity) {
  int star_number = 0;
  BOOL planet_found = FALSE;
  struct stplanet *pplanet;

  int count = 0;
  do {
    star_number = rnd(NUM_STARS);
    if (prmCapacity > 0) {
      for ( pplanet = stars[star_number].first_planet;
            pplanet;
            pplanet = pplanet->next
          ) {
        if (pplanet->capacity >= prmCapacity)
          planet_found = TRUE;
      }
    }
    else
      planet_found = TRUE;

	if (count++ > NUM_STARS) {
		prmCapacity -= 10;
		count = 0;
	}
  }
  while( ! planet_found );

  return star_number;
}
