using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Swordsman;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //增加剑士11%/40%的吸血效果（4/1）                                    1.52/1.4        2.13
    public class CardMeleeSwordsmanTalentPercent : CardEffection<CardMeleeSwordsmanTalentPercent>
    {
        public CardMeleeSwordsmanTalentPercent()
        {
            _category = CD.CardCategory.MELEE;
            _levelCnt = new[] { 0, 0, 4, 1}; //CardDefines.CardLevel.MAX
        }

        //激活剑士之后才能被初始化
        public override bool CanBeInit()
        {
            return SoldierCtrl.Instance.IsSoldierAvailable(WE.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.MeleeType.SWORDSMAN);
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 1 + 0.11f;
            else if (level == CD.CardLevel.RARE)
                value = 1 + 0.4f;
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.TALENT, WE.RaceType.Human,
                SD.TroopType.Melee, (int)HumanDefines.MeleeType.SWORDSMAN,
                (SwordsmanIndividualData.IndividualDataType.TALENT,
                    (object)(SwordsmanIndividualData.Talent.ParameterType.BLOODSTEALPERCENT, value, GD.CalDeltaType.MUL)), false);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}
