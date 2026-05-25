using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    // 增加刺客的攻击12%/78%攻击速度（4/1）                           1.57/1.78    2.8
    public class CardMeleeAssassinAttackSpeed : CardEffection<CardMeleeAssassinAttackSpeed>
    {
        public CardMeleeAssassinAttackSpeed()
        {
            _category = CD.CardCategory.MELEE;
            _levelCnt = new[] { 0, 0, 4, 1 };
        }

        //激活刺客之后才能被初始化
        public override bool CanBeInit()
        {
            return SoldierCtrl.Instance.IsSoldierAvailable(WE.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.MeleeType.ASSASSIN);
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 1 + 0.12f;  //12%
            else if(level == CD.CardLevel.RARE)
                value = 1 + 0.78f; //78%
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.STATE,
                WE.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.MeleeType.ASSASSIN,
                (SoldierDefines.StateSoldierEffectType.ATTACKSPEED, value, GD.CalDeltaType.MUL), false);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve assassin failed");
                return false;
            }

            return true;
        }

    }
}


