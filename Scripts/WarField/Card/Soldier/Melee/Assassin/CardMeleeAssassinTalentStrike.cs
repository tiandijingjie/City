using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Assassin;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //增加刺客破隐一击20%/50%的伤害（4/1）                          2.07/1.5     3.11
    public class CardMeleeAssassinTalentStrike : CardEffection<CardMeleeAssassinTalentStrike>
    {
        public CardMeleeAssassinTalentStrike()
        {
            _category = CD.CardCategory.MELEE;
            _levelCnt = new[] { 0, 0, 4, 1}; //CardDefines.CardLevel.MAX
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
                value = 1 + 0.2f;  //20%
            else if(level == CD.CardLevel.RARE)
                value = 1 + 0.5f; //50%
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.TALENT,
                WE.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.MeleeType.ASSASSIN,
                (AssassinIndividualData.IndividualDataType.TALENT,
                    (object)(AssassinIndividualData.Talent.ParameterType.STRIKETIMES, value, GD.CalDeltaType.MUL)), false);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}

