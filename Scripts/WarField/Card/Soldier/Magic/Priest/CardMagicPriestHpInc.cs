using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //增加牧师0.3/0.6的回血速度（4/1）                                              1.2/0.6    1.8
    public class CardSupportPriestHpInc : CardEffection<CardSupportPriestHpInc>
    {
        public CardSupportPriestHpInc()
        {
            _category = CD.CardCategory.MAGIC;
            _levelCnt = new[] { 0, 0, 4, 1}; //CardDefines.CardLevel.MAX
        }

        //激活牧师之后才能被初始化
        public override bool CanBeInit()
        {
            return SoldierCtrl.Instance.IsSoldierAvailable(WE.RaceType.Human, SD.TroopType.Magic, (int)HumanDefines.MagicType.PRIEST);
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 0.3f * Time.fixedDeltaTime;
            else if (level == CD.CardLevel.RARE)
                value = 0.6f * Time.fixedDeltaTime;
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.STATE,
                WarFieldElements.RaceType.Human, SD.TroopType.Magic, (int)HumanDefines.MagicType.PRIEST,
                (SoldierDefines.StateSoldierEffectType.HPINC, value, GD.CalDeltaType.ADD), false);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve hp inc failed");
                return false;
            }

            return true;
        }
    }
}
