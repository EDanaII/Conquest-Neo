//Text Controls
#ifndef LINUX
#define SCRCLEAR "\33[2J"  //Clear Screen
#define TXTRESET "\33[0m"  //Reset to default
#define TXTBOLD  "\33[1m"  //Bold Text
#define TXTFAINT "\33[2m"  //Faint Text
#define TXTITAL  "\33[3m"  //Italics
#define TXTUNDLN "\33[4m"  //Underlined Text
#define TXTBLINK "\33[5m"  //Blinking Text - Amiga, no.
#define TXTRBLNK "\33[6m"  //Rapid Blink - Amiga, no.
#define TXTREVRS "\33[7m"  //Reverse Video
#define TXTCONCL "\33[8m"  //Concealed Text
#define TXTFGBLK "\33[30m" //Black Text
#define TXTFGRED "\33[31m" //Amiga, Black
#define TXTFGGRN "\33[32m" //Amiga, white
#define TXTFGYEL "\33[33m" //Amiga, Green
#define TXTFGBLU "\33[34m" //Amiga, Blue
#define TXTFGMAG "\33[35m" //Amiga, Red
#define TXTFGCYN "\33[36m" //Amiga, Yellow
#define TXTFGWHT "\33[37m" //Amiga, Magenta
#define TXTBGBLK "\33[40m"
#define TXTBGRED "\33[41m"
#define TXTBGGRN "\33[42m"
#define TXTBGYEL "\33[43m"
#define TXTBGBLU "\33[44m"
#define TXTBGMAG "\33[45m"
#define TXTBGCYN "\33[46m"
#define TXTBGWHT "\33[47m"
#define TXTSBSCR "\33[48m"
#define TXTSPSCR "\33[49m"
#else
#define SCRCLEAR ""  //Clear Screen
#define TXTRESET ""  //Reset to default
#define TXTBOLD  ""  //Bold Text
#define TXTFAINT ""  //Faint Text
#define TXTITAL  ""  //Italics
#define TXTUNDLN ""  //Underlined Text
#define TXTBLINK ""  //Blinking Text - Amiga, no.
#define TXTRBLNK ""  //Rapid Blink - Amiga, no.
#define TXTREVRS ""  //Reverse Video
#define TXTCONCL ""  //Concealed Text
#define TXTFGBLK "" //Black Text
#define TXTFGRED "" //Amiga, Black
#define TXTFGGRN "" //Amiga, white
#define TXTFGYEL "" //Amiga, Green
#define TXTFGBLU "" //Amiga, Blue
#define TXTFGMAG "" //Amiga, Red
#define TXTFGCYN "" //Amiga, Yellow
#define TXTFGWHT "" //Amiga, Magenta
#define TXTBGBLK ""
#define TXTBGRED ""
#define TXTBGGRN ""
#define TXTBGYEL ""
#define TXTBGBLU ""
#define TXTBGMAG ""
#define TXTBGCYN ""
#define TXTBGWHT ""
#define TXTSBSCR ""
#define TXTSPSCR ""
#define  min(x,y)((x < y) ? x : y)
#define  max(x,y)((x > y) ? x : y)
#endif

#ifdef AMIGA
#define  min(x,y)((x < y) ? x : y)
#define  max(x,y)((x > y) ? x : y)
#define TXTFGPN0 "\33[30m"
#define TXTFGPN1 "\33[31m"
#define TXTFGPN2 "\33[32m"
#define TXTFGPN3 "\33[33m"
#define TXTFGPN4 "\33[34m"
#define TXTFGPN5 "\33[35m"
#define TXTFGPN6 "\33[36m"
#define TXTFGPN7 "\33[37m"
#define TXTBGPN0 "\33[40m"
#define TXTBGPN1 "\33[41m"
#define TXTBGPN2 "\33[42m"
#define TXTBGPN3 "\33[43m"
#define TXTBGPN4 "\33[44m"
#define TXTBGPN5 "\33[45m"
#define TXTBGPN6 "\33[46m"
#define TXTBGPN7 "\33[47m"

#ifdef AROS
#define PENUNEXPL "\33[32m" //TXTFGPN2
#define PENATTACK "\33[31m" //TXTBGPN2
#define PENCOLONY "\33[33m" //TXTFGPN3
#define PENNORMAL "\33[31m" //TXTFGPN1
#define PENBRIGHT "\33[32m" //TXTFGPN2
#else
#define PENUNEXPL TXTFGPN2
#define PENATTACK TXTBGPN7
#define PENCOLONY TXTFGPN3
#define PENNORMAL TXTRESET
#define PENBRIGHT TXTFGPN2
#endif
#endif


#ifdef LINUX
#define PENUNEXPL "\e[1m\e[37;40m"
#define PENATTACK "\e[1m\e[31;40m"
#define PENCOLONY "\e[1m\e[32;40m"
#define PENNORMAL TXTRESET
#define PENBRIGHT TXTFGWHT
#endif

#ifdef WIN32
  #define PENUNEXPL TXTFGWHT
  #define PENATTACK TXTFGRED
  #define PENCOLONY TXTFGGRN
  #define PENNORMAL TXTRESET
  #define PENBRIGHT TXTFGWHT
#endif
