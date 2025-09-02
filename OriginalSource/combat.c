/* Combat.c: Combat commands and utilities */
#include <stdio.h>
#include <stdlib.h> 
#include <string.h>
#include <math.h>

#include "defs.h" 
#include "machine.h"
#include "structs.h" 
#include "vars.h"
#include "protos.h"

/* Player attack at star. Assumes warships and enemy planet present */
void playerattack(int starnum) {
	BOOL fighting = TRUE;
	struct stplanet *pplanet;

	display(conf.PenAttack);
	displayAt(INPVWTL,fmt(">>>>>> Attack at Star %c <<<<<<", dispStar(starnum))) ;
	display(conf.PenNormal);
	board[stars[starnum].x][stars[starnum].y].status = '!';
	update_board(stars[starnum].x, stars[starnum].y, left);
	while (fighting) {
		clear_report_view();
		print_star(starnum);
		displayAt(INPVWTL+1, "P?                            ");

		point(2+INPVWTL+1);
		switch (get_char()) {
			case 'S':
			starsum();
			break;
			case 'M':
			printmap();
			break;
			case 'H':
			help(3);
			pause();
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
			case 'T':
			tfsum();
			break;
			case 'G':
			case ' ':
			fighting = play_salvo(starnum);
			break;
			case 'B':
			display("Break off attack");
			fighting = FALSE;
			break;
			default:
			clear_left();
			error(" !Illegal command");
			break;
		} /*switch */
	}
	view_clear(INPVWTL+2,31,6);

	for ( pplanet = stars[starnum].first_planet;
			pplanet;
			pplanet = pplanet->next
	) {
		pplanet->under_attack = FALSE;
	}

	displayAt(INPVWTL+7,"Planet attack concluded       ");
	on_board(stars[starnum].x, stars[starnum].y);

	revolt(starnum);

	pause();
}

		/* Battle between tf's. Assumes ships from both sides and >0 warships present*/
void tf_battle(int starnum) {
	int ennum, plnum, new_tf, i, dstar;
	double enodds, plodds, slist[MAX_NUM_STARS + 1];
	BOOL combat, pla_loss, ene_loss, fin, first;
	char ch;

	board[stars[starnum].x][stars[starnum].y].status = '!';
	update_board(stars[starnum].x, stars[starnum].y, left);

	/*
	 ennum = 1;
	 while (    tf[ENEMY][ennum].dest!=starnum
	 || tf[ENEMY][ennum].eta!=0
	 )
	 ennum++;
	 */
	ennum = GetTaskForceAtStar(ENEMY, starnum);

	plnum = 1;
	if (tf_stars[starnum][PLAYER] > 1) {
		new_tf = get_tf(PLAYER, starnum);
		for (i = 1; i <= MAX_FLEETS; i++) {
			if (tf[PLAYER][i].dest == starnum && tf[PLAYER][i].eta == 0 && i
					!= new_tf)
				joinsilent(PLAYER, &tf[PLAYER][new_tf], &tf[PLAYER][i]);
		}
		tf_stars[starnum][PLAYER] = 1;
		plnum = new_tf;
	} else { /* Mysteriously blowing up here: plnum = 215;
	 while (    tf[PLAYER][plnum].dest != starnum
	 || tf[PLAYER][plnum].eta!=0
	 )
	 plnum++;*/
		plnum = GetTaskForceAtStar(PLAYER, starnum);
	}

	combat = display_forces(ennum, plnum, &enodds, &plodds);

	pause();

	first = TRUE;
	while (combat) {
		if (left_line[24]) {
			displayAt(INPVWTL+6,BLANK_LINE);
			left_line[24] = FALSE;
		}

		pla_loss = FALSE;
		ene_loss = FALSE;
		displayAt(INPVWTL+4," Enemy losses:                ");
		displayAt(INPVWTL+5,"Player losses:                ");

		/* Fight until there are losses, but not first time */
		do {
			point(14+INPVWTL+4);
			ene_loss |= lose(&tf[ENEMY][ennum].t,'t',enodds);
			ene_loss |= lose(&tf[ENEMY][ennum].s,'s',enodds);
			ene_loss |= lose(&tf[ENEMY][ennum].c,'c',enodds);
			ene_loss |= lose(&tf[ENEMY][ennum].b,'b',enodds);
			point(14+INPVWTL+5);
			pla_loss |= lose(&tf[PLAYER][plnum].t,'t',plodds);
			pla_loss |= lose(&tf[PLAYER][plnum].s,'s',plodds);
			pla_loss |= lose(&tf[PLAYER][plnum].c,'c',plodds);
			pla_loss |= lose(&tf[PLAYER][plnum].b,'b',plodds);
		}
		while (!first && !ene_loss && !pla_loss);
		first = FALSE;

		if (!ene_loss) {
			displayAt(14+INPVWTL+4,"(none)");
		}
		if (!pla_loss) {
			displayAt(14+INPVWTL+5,"(none)");
		}

		combat = display_forces(ennum,plnum,&enodds,&plodds);

		/* Withdraw the bad guys */
		if (combat) {
			new_tf = get_tf(ENEMY,starnum);
			if ( tf[PLAYER][plnum].c > 0
					|| tf[PLAYER][plnum].b > 0
			) {
				tf[ENEMY][new_tf].t = tf[ENEMY][ennum].t;
				tf[ENEMY][new_tf].s = tf[ENEMY][ennum].s;

				if (best_withdraw_plan(starnum, enodds)) {
					tf[ENEMY][new_tf].c = tf[ENEMY][ennum].c;
					tf[ENEMY][new_tf].b = tf[ENEMY][ennum].b;
				}
			}

			/* If any need to be withdrawn */
			if ( ( tf[ENEMY][new_tf].t
							+ tf[ENEMY][new_tf].s
							+ tf[ENEMY][new_tf].c
							+ tf[ENEMY][new_tf].b
					) > 0
			) {
				/* Find a place to withdraw to */
				get_stars(starnum,slist);
				do {
					dstar = rnd(NUM_STARS);
				}
				while (slist[dstar] <= 0);

				tf[ENEMY][new_tf].dest = dstar;
				tf[ENEMY][new_tf].eta = (int)((slist[dstar]-0.01)/vel[ENEMY])+1;
	      tf[ENEMY][new_tf].xf=stars[starnum].x;
	      tf[ENEMY][new_tf].yf=stars[starnum].y;
      }
      else
        tf[ENEMY][new_tf].dest = 0;

      fin = FALSE;
      do {
        displayAt(INPVWTL+1,"B?                            ");
        point(2+INPVWTL+1);
        ch = get_char();
        switch ( ch ) {
          case 'M':
            printmap();
            break;
          case 'H':
            help(2);
            break;
          case 'S':
            starsum();
            break;
          case 'T':
            tfsum();
            break;
          case 'C':
            print_col();
            break;
          case '?':
            break;
          case 'R':
            ressum();
            break;
          case 'O':
            combat = display_forces(ennum,plnum,&enodds, &plodds);
            break;
          case 'W':
            withdraw(starnum,plnum);
            combat = display_forces(ennum,plnum,&enodds,&plodds);
            break;
          case ' ':
          case 'G':
            fin = TRUE;
            break;
          default:
            display("!illegal command");
        } /*switch (*/
      }
      while (!fin && combat);
      view_clear(INPVWTL+2,31,6);

      zero_tf(ENEMY,new_tf);
      zero_tf(PLAYER,plnum);

      if (tf[ENEMY][new_tf].dest != 0) {
	      displayAt(1, 23,"en withdraws");
	      point(14, 23);
	      disp_tf(&tf[ENEMY][new_tf]);

	      tf[ENEMY][ennum].t = tf[ENEMY][ennum].t - tf[ENEMY][new_tf].t;
	      tf[ENEMY][ennum].s = tf[ENEMY][ennum].s - tf[ENEMY][new_tf].s;
	      tf[ENEMY][ennum].c = tf[ENEMY][ennum].c - tf[ENEMY][new_tf].c;
	      tf[ENEMY][ennum].b = tf[ENEMY][ennum].b - tf[ENEMY][new_tf].b;

	      zero_tf(ENEMY,ennum);

	      combat = display_forces (ennum, plnum, &enodds, &plodds);
      }
    }
  }
			pause();
			zero_tf(ENEMY, ennum);
			zero_tf(PLAYER, plnum);

			revolt(starnum);

			on_board(stars[starnum].x, stars[starnum].y);
		}

void withdraw(int starnum, int plnum) {
	int withnum;
	BOOL error;

	display("Withdraw ");
	clear_left();
	point(10 + INPVWTL+1);
	withnum = split_tf(plnum);

	if ( tf[PLAYER][withnum].dest != 0 ) {
		point(INPVWTL+2);
		error = set_des(withnum);
		if (error) {
			tf[PLAYER][plnum].dest = starnum;
			joinsilent(PLAYER, &tf[PLAYER][plnum], &tf[PLAYER][withnum]);
			tf_stars[starnum][PLAYER] = 1;
		}
		else
		/* FOO: Shouldn't this be a return code? Or is it used otherwhere */
		tf[PLAYER][withnum].withdrew = TRUE;
	}
}

void blast(tplanet *planet, int factors) {
	int killed;

	killed = min(planet->capacity, factors / 4);

	planet->inhabitants = min(planet->inhabitants, planet->capacity) - killed;
	planet->iu = min(planet->iu - killed, planet->inhabitants * IU_RATIO);
	planet->capacity = planet->capacity - killed;

	/* Totally destroyed... */
	if (planet->inhabitants <= 0) {
		planet->inhabitants = 0;
		planet->iu = 0;
		planet->mb = 0;
		planet->amb = 0;

		if (planet->team != NONE) {
			col_stars[planet->pstar][planet->team]--;

			planet->team = NONE;
			planet->esee_team = NONE;
			planet->conquered = FALSE;

			on_board(stars[planet->pstar].x, stars[planet->pstar].y);
		}
	}
}

void fire_salvo(tteam att_team, int tfnum, struct stplanet *planet,
		BOOL first_time) {
	int bases, att_forces, def_forces;
	BOOL a_losses, p_losses;
	double att_odds, def_odds, attack_save, defend_save;
	tteam def_team;
	struct sttf *task = &tf[att_team][tfnum];

	if (left_line[24]) {
		displayAt(1, 24, BLANK_LINE);
		left_line[24] = FALSE;
	}

	if (att_team == ENEMY)
		def_team = PLAYER;
	else
		def_team = ENEMY;

	att_forces = weapons[att_team] * (task->c * C_GUNS + task->b * B_GUNS);
	def_forces = weapons[def_team] * (planet->mb * C_GUNS + planet->amb
			* B_GUNS);

	if (def_forces > 0) {
		att_odds = fmin((((double) def_forces) / att_forces), 14.0);
		attack_save = exp(log(0.8) * (att_odds));
		def_odds = fmin((double) att_forces / def_forces, 14.0);
		defend_save = exp(log(0.8) * (def_odds));

		if (att_team == PLAYER)
			displayAt(INPVWTL+2, fmt("TF%c", dispTF(tfnum)) );
			else
			displayAt(INPVWTL+2, " EN");

			displayAt( 3+INPVWTL+2,
					fmt( ": %4d(weap %2d)sur: %4.0f",
							att_forces,
							weapons[att_team],
							attack_save*100
					)
			);

			displayAt(INPVWTL+3,fmt(" %c%d:%4d (weap %2d)sur: %4.0f", dispStar(planet->pstar),
	  planet->number, def_forces, weapons[def_team], defend_save*100));

    displayAt(INPVWTL+4,"Attacker losses:              ");
    left_line[23]=TRUE;
    displayAt(INPVWTL+5," Planet losses :              ");

    a_losses = FALSE;
    p_losses = FALSE;

    do {
      point(16+INPVWTL+4);
      a_losses |= lose(&task->c, 'c', attack_save);
      a_losses |= lose(&task->b, 'b', attack_save);

      point(16+INPVWTL+5);
      bases = planet->mb;
      p_losses |=lose(&planet->mb, 'm', defend_save);
      if ( planet->mb != bases ) 
        display("b");

      bases = planet->amb;
      p_losses |= lose(&planet->amb,'a',defend_save);
      if ( planet->amb != bases ) 
        display("mb");
    }
    while (!first_time && !p_losses && a_losses);

    if (!a_losses) {
      displayAt(16+INPVWTL+4,"(none)");
    }

    if (!p_losses) {
      displayAt(16+INPVWTL+5,"(none)");
    }
  }

			if ((planet->mb+planet->amb == 0) && (any_bc(att_team, planet->pstar))) {
				displayAt(16+INPVWTL+6,fmt("Planet %d falls!               ", planet->number));

				planet->team = att_team;
				planet->esee_team = att_team;
				planet->conquered = TRUE;
				board[stars[task->dest].x][stars[task->dest].y].status = '#';
				col_stars[task->dest][def_team]--;
				col_stars[task->dest][att_team]++;

				clear_report_view();
				print_star(planet->pstar);

				clear_field();

				on_board(stars[task->dest].x, stars[task->dest].y);
			}
		}

		/* Play attack at star. Assumes warships & enemy planet present */
		/* Returns TRUE if there is still a battle going on */
BOOL play_salvo(int starnum) {
	int planet_num, tf_num;
	BOOL first_time;
	struct stplanet *pplanet;

	display("Attack planet ");

	pplanet = stars[starnum].first_planet;

	/* If more than one enemy planet, we have to ask which one */
	if ((col_stars[starnum][ENEMY] > 1)) {
		display(":");
		planet_num = get_char() - '0';
		display(fmt("%d", planet_num));
		clear_left();

		for (; pplanet->number != planet_num && pplanet->next; pplanet
				= pplanet->next)
			;

		if (pplanet->number != planet_num) {
			error("! That is not a useable planet");
			return (TRUE);
		}
		if (pplanet->team != ENEMY) {
			error(" !Not an enemy colony");
			return (TRUE);
		}
	} else {
		while (pplanet->team != ENEMY)
			pplanet = pplanet->next;

		display(fmt("%d", pplanet->number));
		clear_left();
	}

	/* Find out which tf is attacking */
	displayAt(1, 19, " attacking tf ");

	if (tf_stars[starnum][PLAYER] > 1) {
		display(":");
		tf_num = get_char() - 'A' + 1;
		display(fmt("%c", tf_num));

		if ((tf_num < 1) || (tf_num > MAX_FLEETS)) {
			error(" !Illegal tf");
			return (TRUE);
		}

		if (tf[PLAYER][tf_num].dest == 0) {
			error(" !Nonexistent tf");
			return (TRUE);
		}

		if ((tf[PLAYER][tf_num].dest != starnum) || (tf[PLAYER][tf_num].eta
				!= 0)) {
			error(" !Tf is not at this star      ");
			return (TRUE);
		}

		if ((tf[PLAYER][tf_num].b + tf[PLAYER][tf_num].c) == 0) {
			error(" !Tf has no warships");
			return (TRUE);
		}
	} else {
		/* Only one TF - must be the one with warships
		 tf_num = 1;
		 while (    tf[PLAYER][tf_num].dest != starnum
		 || tf[PLAYER][tf_num].eta != 0
		 )
		 tf_num++;
		 */
		tf_num = GetTaskForceAtStar(PLAYER, starnum);
		putchar(tf_num + 'a' - 1);
	}

	first_time = !pplanet->under_attack;
	if (!pplanet->under_attack) {
		pplanet->under_attack = TRUE;
		clear_report_view();
		print_star(starnum);
	}
	fire_salvo(PLAYER, tf_num, pplanet, first_time);
	zero_tf(PLAYER, tf_num);

	return ((col_stars[starnum][ENEMY] > 0) && any_bc(PLAYER, starnum));
}

void battle() {
	BOOL first;
	UINT starnum;

	first = TRUE;
	for (starnum = 1; starnum <= NUM_STARS; starnum++) {
		if (tf_stars[starnum][ENEMY] > 0 && tf_stars[starnum][PLAYER] > 0
				&& (any_bc(ENEMY, starnum) || any_bc(PLAYER, starnum))) {
			if (first) {
				display(conf.PenAttack);
				displayAt(INPVWTL, ">>>>>> Taskforce Battle <<<<<<");
				display(conf.PenNormal);
				first = FALSE;
			}
			tf_battle(starnum);
		}

		/* By now we know there is only one side left with warships here */
		if (any_bc(ENEMY,starnum) && col_stars[starnum][PLAYER] > 0)
		enemy_attack(starnum);

		if (any_bc(PLAYER, starnum) && col_stars[starnum][ENEMY] > 0)
		playerattack(starnum);
	}
}
