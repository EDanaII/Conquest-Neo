/* Input.c: Inputting routines */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <ctype.h>
#include <math.h>

#include "defs.h"
#include "machine.h"
#include "structs.h"
#include "vars.h"
#include "protos.h"

void get_line(char *prmLine) {
  char ch;
  int ind;
  
  ind=0;
  do {
    ch = get_char();
    printf("%c", toupper(ch));

    //backspace
    if (ch == '\b') {
      if (ind > 0) {
      	putchar(' ');
      	putchar('\b');
      	// Erase old char
      	putchar(' ');
      	putchar('\b');
      	ind = ind - 1;
      }
      else
	      move(1,0);
      }
      else
        if (ch != '\n') {
          prmLine[ind] = ch;
          ind = ind + 1;
        }
  }
  while (ind < 25 && ch != '\n');

  prmLine[ind] = 0;
}

double dist(int star1, int star2) {
  register int square;

  square 
  = abs(stars[star1].x-stars[star2].x)*abs(stars[star1].x-stars[star2].x)
  + abs(stars[star1].y-stars[star2].y)*abs(stars[star1].y-stars[star2].y);

  return(sqrt((double)square));
}

int get_stars(int s_star, double slist[]) {
  int count;
  UINT starnum; 
  
  count = 0;
  for (starnum = 1 ; starnum <= NUM_STARS; starnum++) {
    // Fudge a bit to avoid doing long squareroots 
    if (range[0] >= (slist[starnum] = dist(s_star,starnum)))
      count++;
    else
      slist[starnum] = 0;
  }
  return(count);
}

char get_token(char *line, int *Value) {
  int index = 0, value = 0;
  char token = ' '; 

  while (iswspace(line[index])) 
    index++;

  if (line[index]) {
    if (iswdigit(line[index])) {
      while (iswdigit(line[index])) {
        value = 10*value + line[index] - '0';
        index++;
      }
    }
    else 
      value = 1;

    if ( line[index] != 0 ) {
      token = line[index];
      index++;
    }
  }

  while (iswspace(line[index])) 
    index++;

  // Still something left 
  if (line[index]) 
    strcpy(line, &line[index]);
  else
    line[0] = 0;

  *Value = value;
  return(token);
}
