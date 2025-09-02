/* Commands.c: Getting user commands */
#include <stdio.h>
#include <stdlib.h>

#include "defs.h"
#include "machine.h"
#include "structs.h"
#include "vars.h"
#include "protos.h"

void blast_planet() {
  int tf_num, planet_num;
  tplanet *pplanet;
  int factors,starnum;
  char iline[81];
  int ind = 1, amount;

  display(conf.PenAttack);
  displayAt(INPVWTL,">>>>>>>> Blast Planet <<<<<<<<");
  display(conf.PenNormal);
  displayAt(1+INPVWTL+1,"Firing TF:");
  clear_left();

  tf_num = get_char() - 'A' + 1;
  display(fmt("%c",tf_num));

  if ((tf_num < 1) || (tf_num > MAX_FLEETS)) {
    error(" !Illegal tf");
    return;
  }

  if (tf[PLAYER][tf_num].dest == 0) {
    error(" !Nonexistent tf");
    return;
  }

  if (tf[PLAYER][tf_num].eta != 0) {
    error(" !Tf is not in normal space   ");
    return;
  }

  if (tf[PLAYER][tf_num].blasting) {
    error(" !Tf is already blasting     ");
    return;
  }

  if ((tf[PLAYER][tf_num].b == 0) && (tf[PLAYER][tf_num].c == 0)) {
    error(" !Tf has no warships         ");
    return;
  }

  starnum = tf[PLAYER][tf_num].dest;
  pplanet = stars[starnum].first_planet;

  if (pplanet == NULL) {
    error(" !No planets at star %c       ", starnum + 'A' - 1);
    return;
  }

  displayAt(INPVWTL+2,"Target colony ");

  if (pplanet->next== NIL) /* Only planet at star */
    display(fmt("%2d",pplanet->number));
  else {
    display(":");
    planet_num = get_char() - '0';
    display(fmt("%d",planet_num));

    for (;(pplanet->number != planet_num) && (pplanet->next);
	    pplanet = pplanet->next);

    if (pplanet->number != planet_num) {
      error(" !No such planet at this star ");
      return;
    }
  }

  if (pplanet->team == ENEMY) {
    error(" !Conquer it first!");
    return;
  }

  if ((pplanet->team == PLAYER) && (!pplanet->conquered)) {
    error(" !That is a human colony!!    ");
    return;
  }

  factors = weapons[PLAYER]
          * (   tf[PLAYER][tf_num].c * C_GUNS 
              + tf[PLAYER][tf_num].b * B_GUNS
            );

  displayAt(INPVWTL+3,fmt("Units (max %3d) :", factors/4));
  point(18,21);
  get_line(iline);
  get_token(iline,&amount);

  if (amount <= 0)
    factors = 0;
  else
    if (amount > 0)
      factors = min(factors, amount * 4);

  if (factors > 0) {
      tf[PLAYER][tf_num].blasting = TRUE;
      displayAt(1, 22, fmt("Blasting %3d units", factors / 4));

      blast(pplanet, factors);

      pplanet->psee_capacity = pplanet->capacity;
  }

  clear_report_view();
  print_star(starnum);
}

void inputplayer() {
  char key;
  BOOL fin;

  fin=FALSE;
  do {
    displayAt(INPVWTL,  "---------- Movement ----------");
    displayAt(INPVWTL+1,"?                             ");
    point(1+INPVWTL+1);
    key = get_char();
    view_clear(INPVWTL+2,31,6);

    switch (key) {
      case 'M':
        printmap();
        break;
      case 'B':
        blast_planet();
        break;
      case 'G':
      case ' ':
        fin=TRUE;
        break;
      case 'H':
        help(1);
        break;
      case 'L':
        land();
        break;
      case 'D':
        send_tf();
        break;
      case 'S':
        starsum();
        break;
      case 'N':
        make_tf();
        break;
      case 'J':
        join_tf();
        break;
      case 'C':
        print_col();
        break;
      case 'R':
        ressum();
        break;
      case 'Q':
        fin = TRUE;
        quit();
        break;
      case '?':
        break;
        case 'T':
        tfsum();
        break;
      case 'E':
        /* Info about enemy - for debugging purposes only! */
        #ifdef DEBUG
          point(REPVWTL);
          clear_field();
          displayAt(REPVWTL,"Enemy status:");
          displayAt(REPVWTL+1,fmt("V: %d, R: %d, W: %d, Next: %c", vel[ENEMY], range[ENEMY], weapons[ENEMY], en_research));
          displayAt(REPVWTL+2,"C:");
          for (key = 1; key <= MAX_NUM_STARS; key++)
          if (col_stars[key][ENEMY])
            display(fmt("%c", key + 'A' - 1));
          displayAt(REPVWTL+3,"F:");
          for (key = 1; key <= MAX_NUM_STARS; key++)
            if (tf_stars[key][ENEMY])
              display(fmt("%c", key + 'A' - 1));
          display(" ");
          for (key = 1; key <= MAX_FLEETS; key++)
            if ((tf[ENEMY][key].dest != 0) && (tf[ENEMY][key].eta > 0))
              display(fmt("%c%d", dispStar(tf[ENEMY][key].dest), tf[ENEMY][key].eta));
          break;
        #endif
      default:
        error("  !illegal command");
    }  /*switch */
  }
  while (!fin);
}

void land() {
  char tfc,planc;
  int x,y,room_left,tfnum, transports,planet_num;
  int starnum;
  char iline[81];
  struct stplanet *pplanet;

  displayAt(1+INPVWTL+1,"Land Taskforce: ");
  tfc = get_char();
  clear_left();
  tfnum = tfc-'A'+1;
  displayAt(16+INPVWTL+1,fmt("%c",tfc));

  if ((tfnum<1) || (tfnum>MAX_FLEETS)) {
    error("  !illegal tf");
    return;
  }

  if ((tf[PLAYER][tfnum].dest == 0)) {
    error("  !nonexistent tf");
    return;
  }

  if ((tf[PLAYER][tfnum].eta != 0)) {
    error("  !tf is not in normal space  ");
    return;
  }

  starnum= tf[PLAYER][tfnum].dest;
  pplanet= stars[starnum].first_planet;

  clear_report_view();
  print_star(starnum);

  if (pplanet == NULL) {
    error("  !no planets at this star    ");
    return;
  }

  if (tf_stars[starnum][ENEMY] > 0) {
    error("  !enemy ships present");
    return;
  }

  /* This should be put in a procedure soon */
  displayAt(9+INPVWTL+2,"planet: ");
  if (pplanet->next == NULL) {
    planet_num = pplanet->number;
    display(fmt("%d", planet_num));
  }
  else {
    planc = get_char();
    planet_num = planc-'0';
    display(fmt("%d",planet_num));

    for (;(pplanet) && (pplanet->number != planet_num); pplanet = pplanet->next);

    if (!pplanet) {
      error(" !Not a habitable planet ");
      return;
    }
  }

  if (    pplanet->team == ENEMY 
       || (pplanet->team == PLAYER && pplanet->conquered)
     ) {
    error("  !Enemy infested planet  !!  ");
    return;
  }

  /* get the number of transports*/
  room_left = pplanet->capacity - pplanet->inhabitants;

  displayAt(5+INPVWTL+3,"transports: ");
  get_line(iline);
  get_token(iline,&transports);

  if (transports == 0)
    transports = tf[PLAYER][tfnum].t;

  if (transports < 1) {
    error("  !illegal transports");
    return;
  }

  if (transports > tf[PLAYER][tfnum].t) {
    error("  !only %2d transports in tf", tf[PLAYER][tfnum].t);
    return;
  }

  if (transports > room_left) {
    error("  !only room for %2d transports", room_left);
    return;
  }

  pplanet->team = PLAYER;
  if (pplanet->inhabitants == 0)
    col_stars[starnum][PLAYER]++;
  pplanet->inhabitants=pplanet->inhabitants+transports;
  pplanet->iu=pplanet->iu + transports;
  tf[PLAYER][tfnum].t=tf[PLAYER][tfnum].t-transports;

  x=tf[PLAYER][tfnum].x;
  y=tf[PLAYER][tfnum].y;

  if (board[x][y].status == ' ') {
    board[x][y].status = '@';
    update_board(x,y,left);
  }

  zero_tf(PLAYER,tfnum);
  clear_report_view();
  print_star(starnum);
}

void quit() {
  clear_screen();
  display("Quit game....[verify]\n");

  if (get_char() != 'Y')
    printmap();
  else {
    game_over = TRUE;
  }
}

void send_tf() {
  char tf_move;
  int tf_num;

  displayAt(1+INPVWTL+1,"Destination tf: ");

  tf_move = get_char();
  display(fmt("%c",tf_move));
  clear_left();
  point(INPVWTL+1);
  tf_num = tf_move-'A'+1;

  if (tf_num < 1 || tf_num>MAX_FLEETS) {
    error(" !illegal tf");
    return;
  }

  if (tf[PLAYER][tf_num].dest == 0) {
    error(" !nonexistent tf");
    return;
  }

  if (    tf[PLAYER][tf_num].eta != 0 
       && (    tf[PLAYER][tf_num].eta != tf[PLAYER][tf_num].origeta 
            || tf[PLAYER][tf_num].withdrew
          )
     ) {
    error(" !Tf is not in normal space  ");
    return;
  }

  if (tf[PLAYER][tf_num].blasting) {
    error(" !Tf is blasting a planet");
    return;
  }

  tf[PLAYER][tf_num].withdrew = FALSE;
  set_des(tf_num);
}

void inv_player(int x, int y, struct stplanet *planet) {
  BOOL printtf;
  char iline[81], key;
  int i, cost, amount, ind = 1, new_tf, balance, trash1, trash2;

  new_tf = get_tf(PLAYER,planet->pstar);
  tf_stars[planet->pstar][PLAYER]++;
  printtf = FALSE;

  balance=planet->iu;

  clear_left();
  do {
    clear_report_view();
    print_star(planet->pstar);
    displayAt(INPVWTL+1,fmt("%c%d>%3d?                       ", dispStar(planet->pstar), planet->number, balance));
    point(7+INPVWTL+1);
    get_line(iline);
    view_clear(INPVWTL+2,31,6);
    do {
      cost = 0;

      key = get_token(iline,&amount);
      switch (key) {
        case 'A':
	        if (planet->inhabitants == 0) {
	          error("  !abandoned planet");
	          break;
	        }

	        if (planet->conquered) {
	          error(" !No amb  on conquered colony ");
	          break;
	        }

	        cost = amount*AMB_COST;
	        if (cost < amount) amount = cost = 0;
	        if (cost <= balance)
	          planet->amb=planet->amb+amount;
	        break;

        case 'B':
          cost = amount*B_COST;
          if (cost < amount) 
            amount = cost = 0;
          if (cost <= balance) {
            tf[PLAYER][new_tf].b=tf[PLAYER][new_tf].b + amount;
            printtf = TRUE;
          }
          break;
        case 'C':
          cost = amount*C_COST;
          if (cost < amount) 
            amount = cost = 0;
          if (cost <= balance) {
            tf[PLAYER][new_tf].c=tf[PLAYER][new_tf].c + amount;
            printtf = TRUE;
          }
          break;
        case 'H':
          help(4);
          break;
        case 'M':
          if (planet->inhabitants == 0) {
            error("  !abandoned planet");
            break;
          }
          if (planet->conquered) {
            error(" !No Mb  on conquered colony  ");
            break;
          }

          cost=amount * MB_COST;
          if (cost < amount) 
            amount = cost = 0;
          if (cost <= balance)
            planet->mb = planet->mb+amount;
          break;
        case 'S':
          cost = amount*S_COST;
          if (cost < amount) 
            amount = cost = 0;
          if (cost <= balance) {
            tf[PLAYER][new_tf].s=tf[PLAYER][new_tf].s + amount;
            printtf = TRUE;
          }
          break;
        case 'T':
          if (planet->conquered) {
            error( "!No transports on conqered col");
            break;
          }

          cost = amount;
          if (cost <= balance) {
            if (cost > planet->inhabitants) {
              error(" ! Not enough people for ( trans");
              cost=0;
              break;
            }

            tf[PLAYER][new_tf].t = tf[PLAYER][new_tf].t+amount;

            planet->inhabitants = planet->inhabitants-amount;

            trash1 = planet->iu - amount;
            trash2 = planet->inhabitants * IU_RATIO;
            planet->iu = min(trash1, trash2);

            printtf = TRUE;

            if (planet->inhabitants == 0) {
              col_stars[planet->pstar][PLAYER]--;
              if (col_stars[planet->pstar][PLAYER] == 0) {
                board[x][y].status = ' ';
                update_board(x,y,left);
              }

              planet->team=NONE;
              planet->amb=0;
              planet->mb=0;
              planet->iu=0;
            }
          }
          break;
        case 'I':
          if ((amount+planet->iu) > (planet->inhabitants*IU_RATIO)) {
            error(" !Can't support that many iu's");
            break;
          }

          cost = I_COST*amount;
          if (cost < amount) 
            amount = cost = 0;
          if (cost <= balance)
            planet->iu=planet->iu+amount;
          break;
        case 'R':
        case 'V':
        case 'W':
          cost = amount;
          if (cost <= balance) {
            point(INPVWTL+4);
            research(PLAYER,key,amount);
          }
          print_res(key);
          break;
        case '*': /* Invest as much as needed in one item */
          key = get_token(iline, &amount);
          switch (key) {
            case 'B':
              amount = max(1, balance/B_COST);
              cost = amount * B_COST;
              if (cost <= balance) {
                tf[PLAYER][new_tf].b += amount;
                printtf = TRUE;
              }
              break;
            case 'C':
              amount = max(1, balance/C_COST);
              cost = amount * C_COST;
              if (cost <= balance) {
                tf[PLAYER][new_tf].c += amount;
                printtf = TRUE;
              }
              break;
            case 'S':
              amount = max(1, balance/S_COST);
              cost = amount * S_COST;
              if (cost <= balance) {
                tf[PLAYER][new_tf].s += amount;
                printtf = TRUE;
              }
              break;
            case 'A':
              if (planet->inhabitants == 0) {
                error("  !abandoned planet");
                break;
              }
              if (planet->conquered) {
                error(" !No AMb  on conquered colony  ");
                break;
              }
              amount = max(1,balance/AMB_COST);
              cost = amount*AMB_COST;
              if (cost <= balance)
                planet->amb = planet->amb+amount;
              break;
            case 'M':
              if (planet->inhabitants == 0) {
                error("  !abandoned planet");
                break;
              }
              if (planet->conquered) {
                error(" !No Mb  on conquered colony  ");
                break;
              }
              amount = max(1,balance/MB_COST);
              cost = amount*MB_COST;
              if (cost <= balance)
                planet->mb = planet->mb+amount;
              break;
            case 'T':
              if (planet->conquered) {
                error( "!No transports on conqered col");
                break;
              }

              amount = cost = min(planet->inhabitants, balance);

              tf[PLAYER][new_tf].t += amount;
              planet->inhabitants = planet->inhabitants-amount;

              trash1 = planet->iu - amount;
              trash2 = planet->inhabitants * IU_RATIO;
              planet->iu = min(trash1, trash2);

              printtf = TRUE;

              if (planet->inhabitants == 0) {
                col_stars[planet->pstar][PLAYER]--;
                if (col_stars[planet->pstar][PLAYER] == 0) {
                  board[x][y].status = ' ';
                  update_board(x,y,left);
                }

                planet->team=NONE;
                planet->amb=0;
                planet->mb=0;
                planet->iu=0;
              }
              break;
            case 'I':
              if (planet->inhabitants*IU_RATIO-planet->iu <= 0) {
                error("!Can't support that many IU's.");
                break;
              }
              amount = min(balance/I_COST,planet->inhabitants*IU_RATIO-planet->iu);
              amount = max(1,amount);
              cost = I_COST*amount;
              if (cost <= balance)
                planet->iu=planet->iu+amount;
              break;
            case 'R':
            case 'W':
            case 'V':
              cost = research_limited(PLAYER, key, balance);
              print_res(key);
              break;
            case ' ':
              break;
            default:
              error("Invalid investment field %c", key);
              break;
          }
          break;
        case '+': /* Invest as much as possible in attack */
          amount = balance/B_COST;
          if (amount > 0) {
            cost = amount*B_COST;
            tf[PLAYER][new_tf].b += amount;
            printtf = TRUE;
          }
          amount = (balance-cost)/C_COST;
          if (amount > 0) {
            cost += amount*C_COST;
            tf[PLAYER][new_tf].c += amount;
            printtf = TRUE;
          }
          break;
        case '-': /* Invest as much as possible in defence */
          if (planet->inhabitants == 0) {
            error("  !abandoned planet");
            break;
          }
          if (planet->conquered) {
            error(" !No defense on conquered colony  ");
            break;
          }
          amount = balance/AMB_COST;
          if (amount > 0) {
            cost = amount*AMB_COST;
            planet->amb = planet->amb+amount;
          }
          amount = (balance-cost)/MB_COST;
          if (amount > 0) {
            cost += amount*MB_COST;
            planet->mb = planet->mb+amount;
          }
          break;
        case ' ':
          break;
        case '>':
          displayAt(INPVWTL+1,">?     ");

          point(2+INPVWTL+1);
          key = get_char();
          switch (key) {
            case 'M':
              printmap();
              break;
            case 'S':
              starsum();
              break;
            case 'C':
              print_col();
              break;
            case 'R':
              ressum();
              break;
            default:
              error(" !Only M,S,C,R allowed      ");
          }
          break;
        default:
          error(" !Illegal field %c",key);
      }
      if (cost > balance) {
	      error(" !can't affort %3d%c", amount, key);
      }
      else
	      balance = balance - cost;
    }
    while (key != ' ');
  }
  while (balance > 0);

  clear_report_view();
  print_star(planet->pstar);

//Join taskforces to conserve letters.
  for (i=1; i<=MAX_FLEETS; i++) {
    if (    tf[PLAYER][i].dest == planet->pstar
         && tf[PLAYER][i].eta  == 0
         && i != new_tf
       )
      joinsilent(PLAYER, &tf[PLAYER][new_tf], &tf[PLAYER][i]);
  }

  zero_tf(PLAYER,new_tf);
  on_board(x,y);
}
