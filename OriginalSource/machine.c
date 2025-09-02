#ifdef AMIGA
  #include <workbench/workbench.h>
  #include <workbench/icon.h>
  #include <proto/icon.h>
  #include <proto/dos.h>
  #include <proto/dos.h>
  #include <libraries/dosextens.h>
  #include <proto/intuition.h>
  #include <proto/exec.h>
#endif

#ifdef LINUX
#include <stdlib.h>
#endif

#ifdef WIN32
  #include <windows.h>
  #include <wincon.h>
#endif

#include <stdio.h>
#include <string.h>
#include <ctype.h>
#include <math.h>

#include "defs.h"
#include "machine.h"
#include "structs.h"
#include "vars.h"
#include "protos.h"

#ifdef AMIGA
  void clear_screen( ) {
    switch (terminal_type) {
      case vis400:
        display("\33[2J");
        break;
      case vt52:
        display("\33[H\33[J");
        break;
      case adm3:
        putchar('\32');
        break;
    }
    point(MAPVWTL);
  }

  void configure(int argc, char *argv[]) {
    struct Library* IconBase = NULL;
 	  struct DiskObject* icon  = NULL;
 	  STRPTR found;
    char words[NUMTKN][WIDTKN];
    int i = 0, j = 0;

    IconBase = (struct Library*)OpenLibrary("icon.library", NULL);
    if (IconBase) {
      icon = (struct DiskObject*)GetDiskObject("Conquest");
  		if (icon) {
        while(config_handles[i]!=NULL) {
  				found = 
            FindToolType(
            icon->do_ToolTypes, 
            config_handles[i]);
  				if(found != NULL) {
            strcpy(words[j++],config_handles[i]);
            if (strlen(found)>0) strcpy(words[j++],found);
          }
          i++;
        }
        process_config( words, j );
   		  FreeDiskObject(icon);
      }
    }

  }

  // Print Routines...
  void display( char *prmString ) {
    printf( prmString );
  }

  char get_char() {
    char buffer;
    BPTR in = Input();

    SetMode (in, 1); /* set to RAW mode */
    Read(in, &buffer, 1);
    SetMode (in, 0); /* set to CON mode */

    if (buffer == '\r')
      buffer = '\n';

    return (toupper(buffer));
  }

  void read_command(int argc, char *argv[]) {
    int i=0;
    for (; i<argc; i++)
      printf( "%s\n", argv[i] );
  }

  void point(int col, int row) {
    switch ( terminal_type ) {
      case adm3:
        printf("\33=%c%c", row+31, col+31);
        break;
      case vt52:
        printf("\33[%d;%dH", row, col);
        break;
      case vis400:
        printf("\33[%d;%dH", row, col);
        break;
      case concept:
        printf("\33a%c%c", row+31, col+31);
        break;
      default:
        putchar('\n');
    }
    x_cursor = col;
    y_cursor = row;
    if ( (x_cursor < 20) && (y_cursor != 18) ) 
      left_line[y_cursor] = TRUE;
  }

  void welcome() {
    printf("\n* Welcome to CONQUEST! *\n\n");
    printf("Amiable version 1.3\n");
    printf("Hit return to continue\n");
  }
#endif


#ifdef LINUX
  // Print Routines...
  void display( char *prmString ) {
    printf( prmString );
  }

  void clear_screen() {
    system( "clear" );
    point(MAPVWTL);
  }

  void configure(int argc, char *argv[]) {

  }

  void point(int col, int row) {
    printf("\33[%d;%dH", row, col);

    x_cursor = col;
    y_cursor = row;
    if ( (x_cursor < 20) && (y_cursor != 18) )
      left_line[y_cursor] = TRUE;
  }

  char get_char() {
	    int result;

	    result = getchar();
	    if (result == 0x0d)
	      result = '\n';

	    return(toupper(result));
  }

  void welcome() {
    printf("\n* Welcome to CONQUEST! *\n\n");
    printf("Lamix version 1.3\n");
    printf("Hit return to continue\n");
  }
#endif


#ifdef WIN32
  extern FILE *stdIn;
  extern FILE *stdOut;

  void clear_screen() {
    system( "cls" );
    point(MAPVWTL);
  }

  void configure(int argc, char *argv[]) {
    if (argc>1)
      read_command(argc, argv);
    else
      read_config(".conquestrc");
  }

  // Print Routines... 
  void display( char *prmString ) {
    WORD tmpColor = 0x0000;
    if (strcmp(prmString,TXTFGRED)==0) tmpColor = FOREGROUND_INTENSITY | FOREGROUND_RED;
    if (strcmp(prmString,TXTFGYEL)==0) tmpColor = FOREGROUND_INTENSITY | FOREGROUND_RED | FOREGROUND_GREEN;
    if (strcmp(prmString,TXTFGGRN)==0) tmpColor = FOREGROUND_INTENSITY | FOREGROUND_GREEN;
    if (strcmp(prmString,TXTFGCYN)==0) tmpColor = FOREGROUND_INTENSITY | FOREGROUND_GREEN | FOREGROUND_BLUE;
    if (strcmp(prmString,TXTFGBLU)==0) tmpColor = FOREGROUND_INTENSITY | FOREGROUND_BLUE;
    if (strcmp(prmString,TXTFGMAG)==0) tmpColor = FOREGROUND_INTENSITY | FOREGROUND_BLUE | FOREGROUND_RED;
    if (strcmp(prmString,TXTFGWHT)==0) tmpColor = FOREGROUND_INTENSITY | FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE;

    if (strcmp(prmString,TXTBGRED)==0) tmpColor = BACKGROUND_INTENSITY | BACKGROUND_RED;
    if (strcmp(prmString,TXTBGYEL)==0) tmpColor = BACKGROUND_INTENSITY | BACKGROUND_RED | BACKGROUND_GREEN;
    if (strcmp(prmString,TXTBGGRN)==0) tmpColor = BACKGROUND_INTENSITY | BACKGROUND_GREEN;
    if (strcmp(prmString,TXTBGCYN)==0) tmpColor = BACKGROUND_INTENSITY | BACKGROUND_GREEN | BACKGROUND_BLUE;
    if (strcmp(prmString,TXTBGBLU)==0) tmpColor = BACKGROUND_INTENSITY | BACKGROUND_BLUE;
    if (strcmp(prmString,TXTBGMAG)==0) tmpColor = BACKGROUND_INTENSITY | BACKGROUND_BLUE | BACKGROUND_RED;
    if (strcmp(prmString,TXTBGWHT)==0) tmpColor = BACKGROUND_INTENSITY | BACKGROUND_RED | BACKGROUND_GREEN | BACKGROUND_BLUE;

    if (strcmp(prmString,TXTRESET)  ==0) tmpColor = FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE;

    if (tmpColor)
      SetConsoleTextAttribute( stdOut, tmpColor );
    else
      printf(prmString);
  }

  char get_char() {
      int result;

      // Wait for a key press
      while (!_kbhit());

      // Read the character
      result = _getch();

      // Handle Enter key specifically
      if (result == 0x0d)
          result = '\n';

      // Convert to uppercase
      return toupper(result);
  }
  void read_command(int argc, char *argv[]) {
    int i=1,j=0,k=0;
    char w[WIDTKN],words[NUMTKN][WIDTKN], c;

  //For each argument in the command line.
    for (; i<argc; i++) {
  //  process each character until EoS;
      while((c = *argv[i]++) != '\0') {
  //    Accept the character if it is AlhphaNum and not a blank or equal sign.
        if ((isalnum(c) || c == '.' || c == '\\' || c == '[') && (c != ' ' || c != '=') )
          w[j++] = c;
  //    Otherwise, terminate the word and copy it to word list.
        else {
          w[j] = '\0';
          j = 0;
          strcpy(words[k++],w);
        }
      }
  //  When EOS, terminate the word and copy it to word list.
      w[j] = '\0';
      j = 0;
      strcpy(words[k++],w);
    }

  //Process the word list.
    process_config( words, k );
  }

  void point(int col, int row) {
    COORD coordDest;
    coordDest.X = col-1;
    coordDest.Y = row-1;
    SetConsoleCursorPosition( stdOut, coordDest);
    x_cursor = col;
    y_cursor = row;
    if ( (x_cursor < 20) && (y_cursor != 18) )
      left_line[y_cursor] = TRUE;
  }

  void welcome() {
    stdIn  = GetStdHandle(STD_INPUT_HANDLE );
    stdOut = GetStdHandle(STD_OUTPUT_HANDLE);

    printf("\n* Welcome to CONQUEST! *\n\n");
    printf("IBM Pissie version 1.3\n");
    printf("Hit return to continue\n");
  }
#endif
