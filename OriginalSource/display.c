/* Display.c: Displaying all kinds of information */
#include <stdio.h>
#include <stdarg.h>
#include <math.h>
#include <string.h>

#include "defs.h"
#include "machine.h"
#include "structs.h"
#include "vars.h"
#include "protos.h"

void clear_report_view() {
  view_clear(REPVWTL,28,17);
  point(REPVWTL);
}

/* Display forces in tf battle. Return TRUE if any battle must happen */
BOOL display_forces(int ennum, int plnum, double *Enodds, double *Plodds) {
  double enodds, plodds;
  int en_forces = 0, pl_forces = 0;

  zero_tf(ENEMY,ennum);
  zero_tf(PLAYER,plnum);

  if (tf[ENEMY][ennum].dest != 0)
    en_forces 
    = (  (tf[ENEMY][ennum].c*C_GUNS + tf[ENEMY][ennum].b*B_GUNS) 
        * weapons[ENEMY]
      );

  if (tf[PLAYER][plnum].dest !=0)
    pl_forces
    = (   (tf[PLAYER][plnum].c * C_GUNS + tf[PLAYER][plnum].b*B_GUNS)
		    * weapons[PLAYER]
      );

  clear_report_view();
  if (tf[ENEMY][ennum].dest != 0)
    print_star(tf[ENEMY][ennum].dest);
  else
    if (tf[PLAYER][plnum].dest != 0)
      print_star(tf[PLAYER][plnum].dest);

  clear_field();

  if (    (en_forces == 0 && pl_forces == 0) 
       || (tf[ENEMY][ennum].dest == 0) 
       || (tf[PLAYER][plnum].dest == 0)
     )
    return(FALSE);

  enodds = ((double)pl_forces)
         / (en_forces + tf[ENEMY][ennum].t*T_DEF + tf[ENEMY][ennum].s*S_DEF);
  enodds = fmin(14.0, enodds);
  enodds = exp(log(0.8) * enodds);

  plodds = ((double)en_forces)
         / (pl_forces + tf[PLAYER][plnum].t*T_DEF  + tf[PLAYER][plnum].s*S_DEF);
  plodds = fmin(14.0, plodds);
  plodds = exp((log(0.8)) * plodds);

  displayAt(INPVWTL+2,fmt("enemy  %5d", en_forces));
  if (en_forces > 0)
    display(fmt("(weap %2d)", weapons[ENEMY]));
  else
    display("         ");
  display(fmt("sur: %4.0f", enodds*100.0));

  displayAt(INPVWTL+3, fmt("player %5d", pl_forces));
  if (pl_forces > 0)
    display(fmt("(weap %2d)", weapons[PLAYER]));
  else
    display("         ");
  display(fmt("sur: %4.0f", plodds*100.0));

  *Enodds = enodds;
  *Plodds = plodds;
  return(TRUE);
}

void disp_tf(struct sttf *taskf) {
  if (taskf->t > 0) printf("%2dt", taskf->t);
  else printf("   ");
  if (taskf->s > 0) printf("%2ds", taskf->s);
  else printf("   ");
  if (taskf->c > 0) printf("%2dc", taskf->c);
  else printf("   ");
  if (taskf->b > 0) printf("%2db", taskf->b);
  else printf("   ");
}

void printmap() {
  int x, y;

  clear_screen();

  for (y = BOARD_Y_SIZE ; y >= 1; y--) {
    if ((y == 1) || (y%5 == 0))
      printf("%2d|", y);
    else
      printf("  |");
	
    for (x = 1; x <= BOARD_X_SIZE; x++)
      update_board(x,y,left);	

    printf("|\n");
  }

  printf("   ");
  for (x = 1 ; x <= BOARD_X_SIZE; x++ )
    printf("---");
  putchar('\n');

  printf("   ");
  for (x = 1 ; x<=MAX_BOARD_SIZE; x++)
    if ((x==1) || (x%5 == 0))
      printf("%2d ", x);
    else
      printf("   ");
  putchar('\n');

  print_res('V');
  print_res('R');
  print_res('W');

  displayAt(EVNVWTL,       "------- Events -------");
  displayAt(EVNVWTL+1, fmt("Turn:              %3d", turn) );
  displayAt(EVNVWTL+2, fmt("Prod yr:             %d", production_year) );

  bottom_field = 0;
  for (x = 19; x<=24; x++)
    left_line[x] = FALSE;
}

void print_col() {
  UINT i;
  tplanet *pplanet;

  view_clear(REPVWTL,28,17);
  displayAt(REPVWTL,"Colonies:");
  point(REPVWTL+1);

  for (i= 1 ; i<=NUM_STARS; i++) {
    pplanet = stars[i].first_planet;
    while (pplanet != NIL) {
      if (pplanet->team == PLAYER) {
	      putchar(dispStar(i));
	      if ((y_cursor > 21 && x_cursor >= 50) || y_cursor > 24) {
	        pause();
	        clear_field();
	        point(REPVWTL);
	      }

	      display( fmt( "%d:%2d                         ", 
                      pplanet->number,
	                    pplanet->psee_capacity
                    )
               );
	      point(x_cursor + 5, y_cursor);
	      x_cursor = x_cursor - 5;

	      display(fmt("(%2d,/%3d)", pplanet->inhabitants, pplanet->iu));
	      if (pplanet->conquered)
	        display("Con");
	      else
	        display("   ");

        if (pplanet->mb != 0)
	        display(fmt("%2dmb", pplanet->mb));
	      else
	        display("    ");

        if (pplanet->amb != 0)
	        display(fmt("%2damb", pplanet->amb));

	      point(x_cursor,y_cursor + 1);
      }
      pplanet=pplanet->next;
    }
  }
  clear_field();
  clear_left();
}

void starsum() {
  char iline[81],
       tmpChar[] = ABC;
  int i,value;
  char strs;

  displayAt(1+INPVWTL+1,"Star summary:");
  clear_left();
  point(INPVWTL+2);
  putchar(':');

  get_line(iline);
  strs = get_token(iline,&value);
  point(REPVWTL);

  if (strs == ' ') {
    strcpy(iline,tmpChar);
    strs = get_token(iline,&value);
  }

  clear_report_view();
  do {
    i = strs-'A'+1;
    print_star(i);
    strs = get_token(iline,&value);
    if ((y_cursor+3+tf_stars[i][PLAYER]+tf_stars[i][ENEMY]) > 19) {
      pause();
      clear_report_view();
    }
  }
  while (strs != ' ');
}

void tfsum() {
  int i, value;
  char tfs;
  char iline[81];

  displayAt(1+INPVWTL+1,"Tf summary :");
  get_line(iline);
  tfs = get_token(iline,&value);
  point(REPVWTL);

  clear_report_view();
  if (tfs==' ')
    for (i = 1 ; i<=MAX_FLEETS; i++)
      print_tf(i);
  else
    do {
      i = tfs - 'A' + 1;
      print_tf(i);
      tfs = get_token(iline,&value);
    }
    while (tfs != ' ');

  clear_field();
  clear_left();
}

void clear_field() {
/*int new_bottom, y;

  new_bottom = y_cursor - 1;
  if (new_bottom < bottom_field) {
    for (y = new_bottom + 1; y<=bottom_field; y++) {
      point(50,y);
      switch (terminal_type) {
       case adm3:
	display(BLANK_LINE);
	break;
       case vis400:
       case vt52:
	display("\33[K");
	break;
      }
    }
  }

  bottom_field = new_bottom;*/
}

void clear_left() {
  view_clear(INPVWTL+2,31,6);
  point(1+INPVWTL+1);
/*int i;

if (terminal_type != hardcopy)
  for (i = 20 ; i<=25; i++)
    if (left_line[i]) {
      switch (terminal_type) {
        case vt52:
          point(1,i);
          display(BLANK_LINE);
          break;
      }
      left_line[i] = FALSE;
    }
*/
}

void arrivalsShow(int* arrivals, const char* label, int y) {
    int any = FALSE;
    int j = 21;

    for (int i = NUM_STARS; i > 0; i--) {
        if (arrivals[i]) {
            if (!any) {
                displayAt(EVNVWTL + y, label);
                point(15 + EVNVWTL + y);
                any = TRUE;
            }
            displayAt(j + EVNVWTL + y, fmt("%c", dispStar(i)));
            arrivals[i] = FALSE;
            j--;
        }
    }

    if ((!any) && (terminal_type != hardcopy)) {
        displayAt(EVNVWTL + y, "                       ");
    }
}

void error(char *fmt, ...) {
  va_list args;

  point(INPVWTL+7);
  va_start(args, fmt);
  vprintf(fmt, args);
}

void print_tf(int i) {
  int x, y;

  if ((i!=0) && (i<27)) {
    if (tf[PLAYER][i].dest != 0) {
      display(fmt("TF%c:", dispTF(i)));

      x=tf[PLAYER][i].x;
      y=tf[PLAYER][i].y;
      if (tf[PLAYER][i].eta==0)
        putchar(dispStar(tf[PLAYER][i].dest));
      else
        putchar(' ');
      display(fmt("(%2d,%2d)               ",x,y));

      point(x_cursor + 12, y_cursor);
      x_cursor = x_cursor - 12;
      disp_tf(&tf[PLAYER][i]);

      if (tf[PLAYER][i].eta != 0) {
        display(conf.PenUnexpl);
        display(fmt(" %c%d", dispStar(tf[PLAYER][i].dest), tf[PLAYER][i].eta));
        display(conf.PenNormal);
      }
      point(x_cursor, y_cursor+1);
    }
  }
}

void print_star(UINT stnum) {
  BOOL see;
  int i, x, y;
  tplanet *p;

  if ((stnum != 0) && (stnum <= NUM_STARS)) {
    /* FOO: A bit too long? */
    if (stars[stnum].visit[PLAYER] == TRUE) {
      see = FALSE;
      display(fmt("--------- Star %c ---------", dispStar(stnum)));
      point(50, y_cursor + 1);

      x = stars[stnum].x;
      y = stars[stnum].y;

      /* Print taskforces */
      if (tf_stars[stnum][PLAYER] != 0) {
	      see = TRUE;
	      for (i=1; i<=MAX_FLEETS; i++) {
	        if (    tf[PLAYER][i].dest == stnum
               && tf[PLAYER][i].eta == 0
               && (    tf[PLAYER][i].b > 0
                    || tf[PLAYER][i].c > 0
                    || tf[PLAYER][i].s > 0
                    || tf[PLAYER][i].t > 0
                  )
             ) {
	          display(fmt("TF%c                           ", dispTF(i)));
	          point(55,y_cursor);
	          disp_tf(&tf[PLAYER][i]);
	          point(50, y_cursor + 1);
	        }
	      }
      }

      see |= (col_stars[stnum][PLAYER] != 0);

      if (see && (tf_stars[stnum][ENEMY] != 0)) {
	      for ( i=1; 
              tf[ENEMY][i].eta != 0 || tf[ENEMY][i].dest != stnum; 
              i++
            )
	        ;

	      display(fmt(" EN:                          "));
	      point(55,y_cursor);
	      disp_tf(&tf[ENEMY][i]);
	      point( 50, y_cursor + 1);
      }

      p = stars[stnum].first_planet;

      if (p == NULL) {
	      display("  no useable planets          ");
	      point(50,y_cursor + 1);
	      return;
      }

      for (;p != NULL; p=p->next, point(x_cursor, y_cursor + 1)) {
	      putchar(' ');
	      if (    (y_cursor > 21 && x_cursor >= 50)
             || y_cursor > 24
           ) {
	        pause();
	        clear_field();
	        point(REPVWTL);
	      }

	      display(fmt("%d:%2d                         ", p->number, p->psee_capacity));
	      point(x_cursor + 5, y_cursor);
	      x_cursor = x_cursor - 5;

	      if (p->psee_capacity == 0) {
	        display(" Decimated");
	        continue;
	      }
	      switch (p->team) {
          case NONE:
	          if (see)
	            printf(" No colony");
	          break;
          case PLAYER:
	          display(fmt("(%2d,/%3d)", p->inhabitants, p->iu));

	          if (p->conquered)
	            display("Con");
	          else
	            display("   ");

	          if (p->mb != 0)
	            display(fmt("%2dmb", p->mb));
	          else
	            display("    ");
	          if (p->amb!=0)
	            display(fmt("%2damb", p->amb));
	          break;
	        case ENEMY:
	          if (see) {
	            display("*EN*");
	            if (p->conquered) {
	              display("Conquered");
	            }
	            else
	              display("   ");

	            if (p->under_attack) {
	              if (p->mb != 0)
		              display(fmt("%2dmb", p->mb));
	              else
		              display("    ");
	              if (p->amb != 0)
      		        display(fmt("%2damb", p->amb));
	          }
	        }
	      }
      }
    }
  }
}

void view_clear(int prmX, int prmY, int prmWidth, int prmHeight) {
  int lastX = x_cursor, lastY = y_cursor;
  int x = 0, y = 0;

  for (y=0; y<prmHeight; y++) {
    point( prmX, prmY+y );
    for (x=0; x<prmWidth; x++) {
      printf( " " );
    }
  }
  x_cursor = lastX; y_cursor = lastY;
}
