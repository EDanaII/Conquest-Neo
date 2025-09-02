// Prototypes for all the functions //

// Combat.c //
void playerattack(int starnum);
void tf_battle(int starnum);
void withdraw(int starnum, int plnum);
void blast(tplanet *planet, int factors);
void fire_salvo(tteam att_team, int tfnum, struct stplanet *planet, BOOL first_time);
BOOL play_salvo(int starnum);
void battle();

// Commands.c //
void blast_planet(); // -> Combat //
void inputplayer();
void land();
void quit();
void send_tf();
void inv_player(int x, int y, struct stplanet *planet);

// Config.c //
char *next_config_token(FILE *f);
UINT get_config_value(char *text);
int get_config_handle(char *text);
void read_command(int argc, char *argv[]);
void read_config(char *filename);
BOOL process_config(char token[NUMTKN][WIDTKN], int size);

// Display.c //
BOOL display_forces(int ennum, int plnum, double *Enodds, double *Plodds);
void disp_tf(struct sttf *taskf);
void printmap();
void print_col();
void starsum();
void tfsum();
void clear_field();
void clear_left();
void clear_report_view();
//void clear_screen();
void error(char *fmt, ...);
void error_message();
void print_tf(int i);
void print_star(UINT stnum);
void view_clear(int prmX, int prmY, int prmWidth, int prmHeight);

// Enemy.c //
BOOL best_withdraw_plan(int Starnum, double odds);
void enemy_attack(int starnum);
void depart(int starnum);
int eval_bc_col(struct stplanet *planet);
int eval_t_col(struct stplanet *planet, double range);
void inputmach();
void move_bc(struct sttf *task, double slist[]);
void send_transports(double slist[], struct sttf *task);
// Maybe this send4t has a meaning? //
void send_t_tf(struct sttf *task, double slist[], int dest_star);
void send_scouts(double slist[], struct sttf *task);
BOOL underdefended(int starnum);
void wander_bc(struct sttf *task, double slist[]);
void inv_enemy(int x, int y, struct stplanet *planet);

// Init.c //
void assign_planets(tstar *Ustar0, int starnum);
void initconst();
void initmach();
void init_player();
void move(int cols, int rows); 
int  pick_planet(int prmCapacity); 
void startup(int argc, char *argv[]); 

// Input.c //
void get_line(char *iline);
double dist(int star1, int star2);
int get_stars(int s_star, double slist[]); // -> utils? misc? //
char get_token(char *line, int *Value);

// Machine.c //
void clear_screen();
void configure(int argc, char *argv[]);
void display( char *prmString );
char get_char();
void point(int col, int row);
void welcome();

// Main.c //
int main();

// Misc.c //
void help(int which);
void on_board(int x, int y);
void pause();
char *get_pen(const char *prmID);

// Movement.c //
void lose_q(int *Ships, char typ, double percent);
BOOL lose(int *Ships, int typ, double percent); // -> Combat //
void move_ships();
BOOL set_des(int tf_num);

// Research.c //
void ressum();
void print_res(char field);
int research_limited(int team, char field, int max_amt);
void research(int team, char field, int amt);
void new_research();

// Taskforce.c //
void make_tf();
int split_tf(int tf_num);
void join_tf();
int get_tf(tteam tm, int starnum);
void joinsilent(tteam team, struct sttf *parent, struct sttf *child);
int GetTaskForceAtStar(tteam prmTeam, int prmStar);

// Update.c //
void update_board(int x, int y, toption option);
void up_year();
void zero_tf(tteam tm, int tf_num);
void check_game_over();
void revolt(int starnum);
void invest();

// Utils.c //
double fmin(double a, double b);
int rnd(int i);
double round(double x); 
BOOL any_bc(tteam team, int starnum);
double fact(int k);
void swap(int* a, int* b);
int conv_bcd(int nibble, char byte);
void displayAt(int prmX, int prmY, char *prmString);
char* fmt(const char*, ...);
char* center(const char* prmString, int prmWidth);
//int min(int x1, int x2);
//int max(int x1, int x2);


// Some other functions that we need //
//extern double exp();
//extern double log();
//extern double sqrt();
//extern int tolower(int aChar);
//extern int toupper(int aChar);
