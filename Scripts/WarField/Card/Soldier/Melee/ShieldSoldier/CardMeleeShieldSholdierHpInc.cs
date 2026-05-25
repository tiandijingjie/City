using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //增加盾兵1.3/4 回血速度（4/1）                                                 5.2/4      9.2
    public class CardMeleeShieldSholdierHpInc : CardEffection<CardMeleeShieldSholdierHpInc>
    {
        public CardMeleeShieldSholdierHpInc()
        {
            _category = CD.CardCategory.MELEE;
            _levelCnt = new[] { 0, 0, 4, 1 };
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
                value = 1.3f  * Time.fixedDeltaTime;  //1.3
            else if(level == CD.CardLevel.RARE)
                value = 4f * Time.fixedDeltaTime; //4
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SoldierDefines.SoldierImproveTarget.STATE,
                WarFieldElements.RaceType.Human, SoldierDefines.TroopType.Melee, (int)HumanDefines.MeleeType.SHIELDSOLDIER,
                (SoldierDefines.StateSoldierEffectType.HPINC, value, GD.CalDeltaType.ADD), false);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }

            return true;
        }
    }
}
