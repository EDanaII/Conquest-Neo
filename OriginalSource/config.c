/* Config.c - configuration file handler */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <ctype.h>

#include "defs.h"
#include "machine.h"
#include "structs.h"
#include "vars.h"
#include "protos.h"

/* What things are to be found in the config file? */
#define CONFIG_NOP
#define CONFIG_ERROR     -2
#define CONFIG_END       -1

struct config conf
= { 0, MAX_BOARD_SIZE, MAX_BOARD_SIZE, MAX_NUM_STARS, MAX_NUM_TURNS, 
    FALSE, FALSE, INIT_UNITS, INIT_MONEY, CAPACITY,
    VICTORY, OVERWHELM, PENUNEXPL, PENCOLONY,
    PENATTACK, PENNORMAL, "",
  };

char *config_handles[] = {
  "ordering",
  "difficulty",
  "width",
  "height",
  "stars",
  "turns",
  "safeexplore",
  "invest",
  "victory",
  "overwhelm",
  "capacity",
  "penunexpl",
  "pencolony",
  "penattack",
  "pennormal",
  "transports",
  "money",
  NULL
};

int get_config_handle(char *text) {
  int i;

  if (!text)
    return(CONFIG_END);

  for (i = 0; config_handles[i]; i++) {
    if (!strncmp(config_handles[i], text, strlen(config_handles[i])))
      break;
  }

  if (config_handles[i])
    return(i);

  return(CONFIG_ERROR);
}

UINT get_config_value(char *text) {
  if (!text)
    return(0);

  return(atol(text));
}

void read_config(char *filename) { 
  FILE *cf = fopen(filename,"r");
  int i=0,j=0,k=0;
  char c;
  char w[WIDTKN];
  char words[NUMTKN][WIDTKN];

//If the file was opened.
  if (cf != NULL) {
//  For each character in the file.
    while((c = fgetc(cf)) != EOF) {
//    Accept the character if it is AlhphaNum and not a blank or equal sign.
      if ( (isalnum(c) || c == '.' || c == '\\' || c == '[')
           && (c != ' ' || c != '=' || c != '\n')
         )
        w[i++] = c;
//    Otherwise, terminate the word and copy it to word list.
      else {
        w[i] = '\0';
        if (i>0) {
          i=0;
          strcpy(words[j++],w);
       }
      }
    }

//  Process the word list.
    process_config( words, j );

    fclose(cf);
  }

}

BOOL process_config(char token[NUMTKN][WIDTKN], int size) {
  int i;

  for (i=0; i<size; i++) {
    printf( "%10s = ", token[i] );
    switch (get_config_handle(token[i])) {
      case CONFIG_END: // Normal termination of configfile
        return(TRUE);
      case CONFIG_ERROR: // Abnormal termination, e.g. by bad keyword
        return(FALSE);
      case 0: // Stars are ordered
        conf.stars_ordered = TRUE;
        break;
      case 1: // Difficulty
        conf.difficulty = get_config_value(token[++i]);
        break;
      case 2:
        conf.board_width = get_config_value(token[++i]);
        if (conf.board_width > MAX_BOARD_SIZE)
          conf.board_width = MAX_BOARD_SIZE;
        break;
      case 3:
        conf.board_height = get_config_value(token[++i]);
        if (conf.board_height > MAX_BOARD_SIZE)
          conf.board_height = MAX_BOARD_SIZE;
        break;
      case 4: // Total number of stars in the Universe, max is 26.
        conf.num_stars = get_config_value(token[++i]);
        if (conf.num_stars > MAX_NUM_STARS)
          conf.num_stars = MAX_NUM_STARS;
        break;
      case 5: // Total time allowed to play game, default 100
        conf.num_turns = get_config_value(token[++i]);
        break;
      case 6: // Explore stars without risk
        conf.SafeExplore = TRUE;
        break;
      case 7: // initial game investment 
        strcpy(conf.invest,token[++i]);
        break;
      case 8: // Victorious if player/enemy > opponent * this amount.
        conf.victory = atof(token[++i]);
        break;
      case 9: // Overwhelm opponent if player/enemy > opponent * this amount
        conf.overwhelm = atof(token[++i]);
        break;
      case 10: // Find planet with this capacity or greater at start of game.
        conf.capacity = get_config_value(token[++i]);
        break;
      case 11: // Color stars not yet explored with this pen.
        strcpy(conf.PenUnexpl,(char *)get_pen(token[++i]));
        break;
      case 12: // Color Stars belonging to player with this pen.
        strcpy(conf.PenColony,(char *)get_pen(token[++i]));
        break;
      case 13: // Color Stars in conflict with this pen.
        strcpy(conf.PenAttack,(char *)get_pen(token[++i]));
        break;
      case 14: // Color all others with this one.
        strcpy(conf.PenNormal,(char *)get_pen(token[++i]));
        break;
      case 15: // Initial number of starting transports.
        conf.transports = get_config_value(token[++i]);
        break;
      case 16: // Initial starting money.
        conf.money = get_config_value(token[++i]);
        break;
      default:
        fprintf(stderr, "Unimplemented configuration #%d.\n",i);
    }
    printf( "%s\n", token[i] );
  }
  return TRUE;
}
