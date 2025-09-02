#define TRUE    1 
#define FALSE   0
#define NIL     0 

#define MAX_BOARD_SIZE 15
#define BOARD_X_SIZE   MAX_BOARD_SIZE
#define BOARD_Y_SIZE   MAX_BOARD_SIZE
#define MAX_VELOCITY   12
#define MAX_RANGE      MAX_BOARD_SIZE
#define MAX_WEAPONS    10

#define VICTORY   1.5
#define OVERWHELM 8.0
#define CAPACITY  0

#define INIT_UNITS     35
#define INIT_MONEY     30

#define I_COST          3
#define MB_COST         8
#define C_COST         16
#define S_COST          6
#define AMB_COST       35
#define B_COST         70
#define C_GUNS          7
#define B_GUNS         40
#define S_DEF           2
#define T_DEF           1
#define MAX_NUM_STARS  26
#define MAX_NUM_TURNS 100 
#define NUM_STARS      conf.num_stars
#define MAX_FLEETS     26
#define INIT_VEL        1
#define INIT_RANGE      5
#define INIT_WEAPONS    3
#define IU_RATIO        2
#define BLANK_LINE       "                              "
#define T_E_PROB        10.0
#define T_E_VAR          5
#define S_E_PROB        70.0
#define S_E_VAR         10
#define C_E_PROB        90
#define C_E_VAR         10
#define B_E_PROB        97.0
#define B_E_VAR         3

#define OTHER_PLAYER(team) (1-(team))

#define NUMTKN 32 // Total number of Config Tokens
#define WIDTKN 32 // Total width of each config token.
#define ABC    "ABCDEFGHIJKLMNOPQRSTUVWXYZ"

//Report View
#define MAPVWTL  1,1
#define REPVWTL 50,1
#define INPVWTL  1,18
#define EVNVWTL 32,18
#define RESVWTL 55,18
#define CURPOS  x_cursor,y_cursor
#define REPVWNL x_cursor=50,y_cursor+1
#define INPVWNL x_cursor=1,y_cursor+1
#define EVNVWNL x_cursor=32,y_cursor+1
#define RESVWNL x_cursor=32,y_cursor+1

// --- logging tee: duplicate console output to transcript.txt ---
#ifndef CQ_TEE_LOG
#define CQ_TEE_LOG 1
#endif

#if CQ_TEE_LOG
#include <stdarg.h>
#include <stdio.h>

static FILE* cq_log = NULL;

static void cq_log_open(void) {
    if (!cq_log) {
        // open in the executable's directory instead of the CWD:
        // simplest: try CWD; if that fails, fall back to "C:\\temp\\transcript.txt"
        cq_log = fopen("transcript.txt", "w");
        if (!cq_log) cq_log = fopen("C:\\temp\\transcript.txt", "w");
    }
}

static void cq_log_close(void) {
    if (cq_log) { fclose(cq_log); cq_log = NULL; }
}

// Core write helpers
static int cq_vprint(FILE* stream, const char* fmt, va_list ap) {
    int n = vfprintf(stream, fmt, ap);
    fflush(stream);
    if (cq_log) { va_list ap2; va_start(ap2, fmt); vfprintf(cq_log, fmt, ap2); va_end(ap2); fflush(cq_log); }
    return n;
}
static int cq_print(const char* fmt, ...) {
    va_list ap; va_start(ap, fmt);
    int n = cq_vprint(stdout, fmt, ap);
    va_end(ap); return n;
}
static int cq_fprint(FILE* stream, const char* fmt, ...) {
    va_list ap; va_start(ap, fmt);
    int n = cq_vprint(stream, fmt, ap);
    va_end(ap); return n;
}
static int cq_puts(const char* s) {
    int n = fputs(s, stdout); fputc('\n', stdout); fflush(stdout);
    if (cq_log) { fputs(s, cq_log); fputc('\n', cq_log); fflush(cq_log); }
    return n;
}
static int cq_fputs(const char* s, FILE* stream) {
    int n = fputs(s, stream); fflush(stream);
    if (cq_log) { fputs(s, cq_log); fflush(cq_log); }
    return n;
}
static int cq_putchar(int ch) {
    int n = fputc(ch, stdout); fflush(stdout);
    if (cq_log) { fputc(ch, cq_log); fflush(cq_log); }
    return n;
}
static int cq_putc(int ch, FILE* stream) {
    int n = fputc(ch, stream); fflush(stream);
    if (cq_log) { fputc(ch, cq_log); fflush(cq_log); }
    return n;
}

// Macros to intercept common output calls everywhere:
#define printf(...)            cq_print(__VA_ARGS__)
#define vprintf(fmt, ap)       cq_vprint(stdout, (fmt), (ap))
#define fprintf(stream, ...)   cq_fprint((stream), __VA_ARGS__)
#define vfprintf(stream,fmt,ap) cq_vprint((stream), (fmt), (ap))
#define puts(s)                cq_puts((s))
#define fputs(s, stream)       cq_fputs((s), (stream))
#define putchar(ch)            cq_putchar((ch))
#define putc(ch, stream)       cq_putc((ch), (stream))
#endif
