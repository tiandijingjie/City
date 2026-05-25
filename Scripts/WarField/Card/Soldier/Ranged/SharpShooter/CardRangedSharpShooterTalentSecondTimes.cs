using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SharpShooter;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //降低第二攻击的5%/18%的攻击力衰减（4/1）                            20/18    38
    public class CardRangedSharpSholdierTalentSecondTimes : CardEffection<CardRangedSharpSholdierTalentSecondTimes>
    {
        public CardRangedSharpSholdierTalentSecondTimes()
        {
            _category = CD.CardCategory.RANGED;
            _levelCnt = new[] { 0, 0, 4, 1 };
        }

        //激活神射手之后才能被初始化
        public override bool CanBeInit()
        {
            return SoldierCtrl.Instance.IsSoldierAvailable(WE.RaceType.Human, SD.TroopType.Ranged, (int)HumanDefines.RangedType.SHARPSHOOTER);
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 0.05f * 100; //5%
            else if(level == CD.CardLevel.RARE)
                value = 0.18f * 100; //18%
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.TALENT,
                WE.RaceType.Human, SD.TroopType.Ranged, (int)HumanDefines.RangedType.SHARPSHOOTER,
                (SharpShooterIndividualData.IndividualDataType.TALENT,
                    (object)(SharpShooterIndividualData.Talent.ParameterType.SECONDDAMAGETIMES, value, GD.CalDeltaType.ADD)), false);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}
