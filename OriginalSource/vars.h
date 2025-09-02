extern struct config conf;
extern char *config_handles[];

extern int tf_stars[MAX_NUM_STARS+1][2], col_stars[MAX_NUM_STARS+1][2];

extern char en_research;

extern struct ssector board[MAX_BOARD_SIZE+1][MAX_BOARD_SIZE+1];

extern struct ststar stars[MAX_NUM_STARS+1];

extern struct sttf tf[2][27];

extern double growth_rate[2];

extern int vel[2], range[2], weapons[2], weap_working[2], 
           ran_working[2], vel_working[2], weap_req[11], 
           ran_req[MAX_BOARD_SIZE+1], vel_req[MAX_VELOCITY+1], 
           production_year, enemy_arrivals[MAX_NUM_STARS+1], 
           en_departures[MAX_NUM_STARS+1], 
           player_arrivals[MAX_NUM_STARS+1], bottom_field;

extern UINT turn; 

extern BOOL game_over;

extern termtype terminal_type;

extern int x_cursor, y_cursor, saved_game, left_line[25], debug;
