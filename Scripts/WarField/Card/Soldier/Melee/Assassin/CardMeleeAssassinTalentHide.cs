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

    //缩短刺客隐身时间0.4/1.2s （4/1）                                        1.6/1.2        2.8
    public class CardMeleeAssassinTalentHide : CardEffection<CardMeleeAssassinTalentHide>
    {

        public CardMeleeAssassinTalentHide()
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
                value = 0.4f;
            else if(level == CD.CardLevel.RARE)
                value = 1.2f;
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.TALENT,
                WE.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.MeleeType.ASSASSIN,
                (AssassinIndividualData.IndividualDataType.TALENT,
                    (object)(AssassinIndividualData.Talent.ParameterType.HIDEINTERVAL, value, GD.CalDeltaType.SUB)), false);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }

    }
}

