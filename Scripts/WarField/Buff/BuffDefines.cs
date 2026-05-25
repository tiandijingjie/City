using System.Collections;
using System.Collections.Generic;

namespace WarField
{
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    public class BuffDefines
    {
        public enum SoldierBuffType
        {
            MIN = 0,
            STUN, //晕眩
            STATE, //影响属性
            REFLECTION,//伤害反弹，先反弹再护盾吸收
            SHIELD, //护盾
            DURATIONDAMAGE, //持续性伤害
            STUCK, //困住
            FREEZE, //冰冻
            SHAPCHANGE, //变形
            ATTACKLOST, //攻击落空
            MAX,
        }

        public enum WarBuildingBuffType
        {
            MIN = 0,
            FREEZE, //冰冻
            //ATTRIBUTE, //change the soldier's attribute when warbuilding spawn it
            MAX,
        }

        public enum BuffTriggerType
        {
            MIN = 0,
            TIMETRIGGER,  //periodical trigger, bwteen every time trigger need take fix time
            ATTACKTRIGGER, //only take effect when do attack
            BEATTACKTRIGGER, //only take effect be attacked
            DIETRIGGER,   //only take effect when die
            MAX,
        }

        //when has the same state/duration buff, strategy to add a new one
        //OVERRIDE and APPEND major differece is whether new buff change the buff value, OVERRIDE change the vlue, but APPEND can not change value
        public enum BuffStrategy
        {
            MIN = 0,
            UNIQUE,   //can only has one buff, if has a old one , return false
            OVERRIDE, //override both time and value
            IGNORE, //ignore the new one, not take effect, return true
            CREATENEW, //not update the old buff(even has a same buff), just crerate a new one
            APPEND, //if already has buff, not change value but append self to owner list
            MAX,
        }

        public enum BuffCallBackEventType
        {
            MIN = 0,
            OVERRIDE, //old buff is override by a new one
            EFFECT, //eveny time take effect
            FINISH, //buff finish
            MAX,
        }

        public enum ShieldBuffType
        {
            MIN = 0,
            NORMAL,
            CHARGEABLE, //充能型
            MAX,
        }

        public enum TriggerBuffType
        {
            MIN = 0,
            PERIOD, //周期性触发
            OVERTIMETRIGGER, //超时触发
            STATUSTRIGGER, //soldier的status触发
            MAX,
        }
    }

    //使用unsafe的数据
    public unsafe delegate void BuffUnsafeCallback(BuffDefines.BuffCallBackEventType type, void* callbackData);
}

