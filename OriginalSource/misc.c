/* Misc.c: Miscellaneous routines I didn't know where else to put. */
#include <stdio.h>
#include <string.h>

#include "defs.h"
#include "machine.h"
#include "structs.h"
#include "vars.h"
#include "protos.h"

struct helpst {
  char *cmd, *does;
};

struct helpst help0[] = {
  { "B",    "Bld Battlestar(s)    75"},
  { "C",    "Bld Cruiser(s)       16"},
  { "H",    "Help"                   },
  { "R",    "Range Research"         },
  { "S",    "Bld Scout(s)          6"},
  { "V",    "Velocity Research"      },
  { "W",    "Weapons Research"       },
  { ">M",   "Redraw Map"             },
  { ">R",   "Research summary"       },
  { 0,      0                        }
};

struct helpst help1[] = {
  { "B",    "Blast Planet"           },
  { "C",    "Colony summary"         },
  { "D",    "TaskForce Destination"  },
  { "G",    "Go on (done)"           },
  { "H",    "Help"                   },
  { "J",    "Join TaskForces"        },
  { "L",    "Land transports"        },
  { "M",    "Redraw Map"             },
  { "N",    "New TaskForce"          },
  { "Q",    "Quit"                   },
  { "R",    "Research summary"       },
  { "S",    "Star summary"           },
  { "T",    "TaskForce summary"      },
  { 0,      0                        }
};

struct helpst help2[] = {
  { "C",    "Colonies"               },
  { "G",    "Go on (done)"           },
  { "H",    "Help"                   },
  { "M",    "Map"                    },
  { "O",    "Odds"                   },
  { "R",    "Research summary"       },
  { "S",    "Star summary"           },
  { "T",    "TaskForce summary"      },
  { "W",    "Withdraw"               },
  { 0,      0,                       }
};

struct helpst help3[] = 
{
  {       "B",    "Break off Attack"       },
  {       "C",    "Colony summary"         },
  {       "G",    "Go on (done)"           },
  {       "H",    "Help"                   },
  {       "J",    "Join TFs"               },
  {       "M",    "Redraw Map"             },
  {       "N",    "New TF"                 },
  {       "R",    "Research summary"       },
  {       "S",    "Star summary"           },
  {       "T",    "TaskForce summary"      },
  {       0,      0                        }
};

struct helpst help4[] = {
  {       "A",    "Bld Adv. Missle Base 35"},
  {       "B",    "Bld Battlestar(s)    70"},
  {       "C",    "Bld Cruiser(s)       16"},
  {       "H",    "Help"                   },
  {       "I",    "Invest                3"},
  {       "M",    "Bld Missle Base(s)    8"},
  {       "R",    "Range Research"         },
  {       "S",    "Bld Scout(s)          6"},
  {       "T",    "Bld Transports"         },
  {       "V",    "Vel Research"           },
  {       "W",    "Weapons Research"       },
  {       "*x",   "Bld max. # of x"        },
  {       "+",    "Bld max. offensive"     },
  {       "-",    "Bld max. defensive"     },
  {       ">C",   "Colony summary"         },
  {       ">M",   "Redraw Map"             },
  {       ">R",   "Research summary"       },
  {       ">S",   "Star summary"           },
  {       0,      0                        }
};

void help(int which) {
  struct helpst *h;
  int j;
  
  j = 1;
  point(50, j++);
  if (which == 0) h = help0;
  if (which == 1) h = help1;
  if (which == 2) h = help2;
  if (which == 3) h = help3;
  if (which == 4) h = help4;
  while (h->cmd != 0) {
    display(fmt("%2s - %-25s", h->cmd, h->does));
    point(50, j++);
    h++;
  }
  clear_field();
}

void on_board(int x, int y) {
  int i;
  int starnum;
  board[x][y].tf=' ';
  i=1;
  do {
    if (    tf[PLAYER][i].dest!=0
         && tf[PLAYER][i].x==x
         && tf[PLAYER][i].y==y
       ) {
      if (board[x][y].tf == ' ')
        board[x][y].tf= dispTF(i);
      else {
	      board[x][y].tf='*';
	      i=27;
      }
    }
    i=i+1;
  } 
  while (i <= 26); //??? Necessary
  if ( board[x][y].star != '.' ) {
    board[x][y].status = ' ';
    starnum= board[x][y].star - 'A' +1;
    if ( col_stars[starnum][PLAYER] != 0 )
      board[x][y].status = '@';
  }
  update_board(x,y,both);
}

void pause() {
  display(conf.PenUnexpl);
  displayAt(INPVWTL+7,"Press any key to continue");
  display(conf.PenNormal);
  point(INPVWTL+1);
  get_char();
  view_clear(INPVWTL+2,31,6);
}

char *get_pen(const char *prmID) {
  char *tmpColor = NULL; 
  strcpy(tmpColor,TXTRESET);

  if (strcmp(prmID,"bold")==0)        strcpy(tmpColor,TXTBOLD);
  if (strcmp(prmID,"faint")==0)       strcpy(tmpColor,TXTFAINT);
  if (strcmp(prmID,"italic")==0)      strcpy(tmpColor,TXTITAL);
  if (strcmp(prmID,"underline")==0)   strcpy(tmpColor,TXTUNDLN);
  if (strcmp(prmID,"blink")==0)       strcpy(tmpColor,TXTBLINK);
  if (strcmp(prmID,"rapid")==0)       strcpy(tmpColor,TXTRBLNK);
  if (strcmp(prmID,"reverse")==0)     strcpy(tmpColor,TXTREVRS);
  if (strcmp(prmID,"conceal")==0)     strcpy(tmpColor,TXTCONCL);
  if (strcmp(prmID,"subscript")==0)   strcpy(tmpColor,TXTSBSCR);
  if (strcmp(prmID,"superscript")==0) strcpy(tmpColor,TXTSPSCR);
  if (strcmp(prmID,"black")==0   || strcmp(prmID,"pen0")==0) strcpy(tmpColor,TXTFGBLK);
  if (strcmp(prmID,"red")==0     || strcmp(prmID,"pen1")==0) strcpy(tmpColor,TXTFGRED);
  if (strcmp(prmID,"green")==0   || strcmp(prmID,"pen2")==0) strcpy(tmpColor,TXTFGGRN);
  if (strcmp(prmID,"yellow")==0  || strcmp(prmID,"pen3")==0) strcpy(tmpColor,TXTFGYEL);
  if (strcmp(prmID,"blue")==0    || strcmp(prmID,"pen4")==0) strcpy(tmpColor,TXTFGBLU);
  if (strcmp(prmID,"magenta")==0 || strcmp(prmID,"pen5")==0) strcpy(tmpColor,TXTFGMAG);
  if (strcmp(prmID,"yellow")==0  || strcmp(prmID,"pen6")==0) strcpy(tmpColor,TXTFGCYN);
  if (strcmp(prmID,"white")==0   || strcmp(prmID,"pen7")==0) strcpy(tmpColor,TXTFGWHT);
  if (strcmp(prmID,"bgblack")==0   || strcmp(prmID,"bgpen0")==0) strcpy(tmpColor,TXTBGBLK);
  if (strcmp(prmID,"bgred")==0     || strcmp(prmID,"bgpen1")==0) strcpy(tmpColor,TXTBGRED);
  if (strcmp(prmID,"bggreen")==0   || strcmp(prmID,"bgpen2")==0) strcpy(tmpColor,TXTBGGRN);
  if (strcmp(prmID,"bgyellow")==0  || strcmp(prmID,"bgpen3")==0) strcpy(tmpColor,TXTBGYEL);
  if (strcmp(prmID,"bgblue")==0    || strcmp(prmID,"bgpen4")==0) strcpy(tmpColor,TXTBGBLU);
  if (strcmp(prmID,"bgmagenta")==0 || strcmp(prmID,"bgpen5")==0) strcpy(tmpColor,TXTBGMAG);
  if (strcmp(prmID,"bgcyan")==0    || strcmp(prmID,"bgpen6")==0) strcpy(tmpColor,TXTBGCYN);
  if (strcmp(prmID,"bgwhite")==0   || strcmp(prmID,"bgpen7")==0) strcpy(tmpColor,TXTBGWHT);
  return tmpColor;
}
