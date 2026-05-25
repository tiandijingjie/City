using System.Collections;
using System.Collections.Generic;

namespace WarField
{
    public class SkillDefines
    {
        public enum SkillTriggerType
        {
            MIN = 0,
            TIMETRIGGER,  //periodical trigger, bwteen every time trigger need take fix time
            ATTACKTRIGGER, //only take effect when do attack
            BEATTACKTRIGGER, //only take effect be attacked
            DIETRIGGER,   //only take effect when die
            PARTERENTERSKILLRANGEDTRIGGER, //parter enter skill range will trigger
            PARTERLEAVESKILLRANGEDTRIGGER, //parter leave skill range will trigger
            RIVALENTERSKILLRANGEDTRIGGER, //rival enter skill range will trigger
            RIVALLEAVESKILLRANGEDTRIGGER, //rival leave skill range will trigger
            ACTIVETRIGGER, //take effect when call skill active, the skill will be trigger only when soldier call active skill
            REBORNTRIGGER, //die then enter reborn status will trigger, only hero has reborn status
            MAPCHANGE, //when soldier change map will trigger, when a skill need to create a searcher will need this trigger
            MAX,
        }

        public static int MAXSOLDIERSKILLLEVEL = 4; //soldier skill max level, used in the individual data
    }
}

