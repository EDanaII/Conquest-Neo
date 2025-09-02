/* Utils.c: Diverse small utility funcitons that shouldn't lie all over */

#include <stdio.h>
#include <stdarg.h>
#include <stdlib.h>

#include "defs.h"
#include "machine.h"
#include "structs.h"
#include "vars.h"
#include "protos.h"

double fmin(double a, double b) {
  return(a<b?a:b);
}

/* Relative (dis)placement. Doesn't set x_cursor & y_cursor */
void move(int cols, int rows) {
  if (    (terminal_type == vt52)
       || (terminal_type == vis400) ) {
    if (cols > 0)
      printf("\33[%dC", cols);
    else if (cols < 0)
      printf("\33[%dD", cols);
  }
}

/* Print Routines... */
void displayAt(int prmX, int prmY, char *prmString ) {
  point( prmX, prmY );
  display( prmString );
}

/* Random integer in [1..i] */
int rnd(int i) {
  return(rand()%i+1);
}

double round(double x) {
  return(x<0.0?(int)(x-0.5):(int)(x+.5));
}

/* Are there any battlestars or cruisers at this star? */
BOOL any_bc(tteam team, int starnum) {
  BOOL any;
  int tf_number;

  any = FALSE;
  if (tf_stars[starnum][team]>0) {
    for (tf_number = 1;(!any) && (tf_number <= MAX_FLEETS); tf_number++) {
      any = (    tf[team][tf_number].dest==starnum 
              && tf[team][tf_number].eta==0 
              && (tf[team][tf_number].c > 0 || tf[team][tf_number].b > 0)
            );
    }
  }
  return (any);
}

char addToChar(char c, int i) {
    return c + i;
}

int compare(const void* a, const void* b) {
    return (*(int*)a - *(int*)b);
}

char dispStar(int i) {
    return addToChar('@', i);
}

char dispTF(int i) {
    return addToChar('`', i);
}

double fact(int k) {
  int res;

  for (res = 1; k > 1; res *= k, k--);

  return(res);
}

char* leftPad(char* str, int total_length, char pad_char) {
    int len = strlen(str);  // Get current string length
    if (len >= total_length) {
        return str;  // No padding needed
    }

    // Create a new buffer for the padded string
    char* padded_str = malloc(total_length + 1);  // Allocate memory for the padded string
    if (!padded_str) {
        printf("Memory allocation failed!\n");
        exit(1);  // Handle allocation failure
    }

    int padding = total_length - len;  // Calculate the number of padding characters

    // Fill the padding characters
    memset(padded_str, pad_char, padding);

    // Copy the original string after the padding
    strcpy(padded_str + padding, str);

    padded_str[total_length] = '\0';  // Null-terminate the string

    return padded_str;
}

void padRight(char* str, int total_length, char pad_char) {
    int len = strlen(str); 
    if (len < total_length) {
        for (int i = len; i < total_length; i++) {
            str[i] = pad_char;
        }
        str[total_length] = '\0'; 
    }
}

void swap(int *a, int *b) {
  int t;

  t = *a;
  *a = *b;
  *b = t;
}

int unique(int arr[], int size) {
    if (size == 0) return 0;

    qsort(arr, size, sizeof(int), compare);  // Sort the array

    int unique_count = 0;  // Index for unique elements

    for (int i = 1; i < size; i++) {
        if (arr[unique_count] != arr[i]) {
            unique_count++;
            arr[unique_count] = arr[i];  // Move unique elements to the front
        }
    }

    return unique_count + 1;  // Return new size after removing duplicates
}

int conv_bcd(int nibble, char byte) {
  if (nibble == 1) return (byte & 0x0f);
  return((byte >> 4) & 0x0f);
}

char *fmt(const char * prmString, ...) {
  va_list parms;
  static char tmpString[80];

  va_start(parms, prmString);
  vsprintf(tmpString, prmString, parms);
  return tmpString;
}

/*char * center(const char * prmString, int prmWidth) {
  int tmpLength = (int)(abs(prmWidth - strlen(prmString))/2);
  char spaces[] = "                                                                                ";
  return ;  
}
*/
