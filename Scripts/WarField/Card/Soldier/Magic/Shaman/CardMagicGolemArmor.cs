using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //增加石头人0.6/3的护甲（4/1）                                                    2.4/3          5.4
    public class CardSupportGolemArmor : CardEffection<CardSupportGolemArmor>
    {
        public CardSupportGolemArmor()
        {
            _category = CD.CardCategory.MAGIC;
            _levelCnt = new[] { 0, 0, 4, 1}; //CardDefines.CardLevel.MAX
        }

        //激活萨满之后才能被初始化
        public override bool CanBeInit()
        {
            return SoldierCtrl.Instance.IsSoldierAvailable(WE.RaceType.Human, SD.TroopType.Magic, (int)HumanDefines.MagicType.SHAMAN);
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 0.6f;
            else if (level == CD.CardLevel.RARE)
                value = 3f;
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.STATE,
                WarFieldElements.RaceType.Neutral, SD.TroopType.Melee, (int)NeutralDefines.MeleeType.GOLEM,
                (SoldierDefines.StateSoldierEffectType.PHYARMOR, value, GD.CalDeltaType.ADD), false);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve armor failed");
                return false;
            }

            return true;
        }
    }
}
