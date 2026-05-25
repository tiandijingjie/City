using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using ShieldSoldier;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    // 增加盾兵4.5/16点格挡 （4/1）                                                  18/16      34
    public class CardMeleeShieldSholdierTalentBlockValue : CardEffection<CardMeleeShieldSholdierTalentBlockValue>
    {
        public CardMeleeShieldSholdierTalentBlockValue()
        {
            _category = CD.CardCategory.MELEE;
            _levelCnt = new[] { 0, 0, 4, 1}; //CardDefines.CardLevel.MAX
        }

        //激活盾兵之后才能被初始化
        public override bool CanBeInit()
        {
            return SoldierCtrl.Instance.IsSoldierAvailable(WE.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.MeleeType.SHIELDSOLDIER);
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 4.5f;
            else if (level == CD.CardLevel.RARE)
                value = 16f;
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.TALENT,
                WE.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.MeleeType.SHIELDSOLDIER,
                (ShieldSoldierIndividualData.IndividualDataType.TALENT,
                    (object)(ShieldSoldierIndividualData.Talent.ParameterType.BLOCKVALUE, value, GD.CalDeltaType.ADD)), false);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}
