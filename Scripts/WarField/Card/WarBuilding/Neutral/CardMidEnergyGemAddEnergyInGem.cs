using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using WRD = WarResDefine;
    using GD = GlobalDefines;

    // 增加中级能量宝石蕴含的能量2（2/1）   6
    public class CardMidEnergyGemAddEnergyInGem : CardEffection<CardMidEnergyGemAddEnergyInGem>
    {
        public CardMidEnergyGemAddEnergyInGem()
        {
            _category = CD.CardCategory.RESOURCE;
            _levelCnt = new[] { 0, 0, 2, 1 };
        }

        public override bool CanBeInit()
        {
            return true;
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 2;
            else if (level == CD.CardLevel.RARE)
                value = 2;
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = WarResCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WRD.ResTypes.OCULARSTONE, "energyInMidStone", value, GD.CalDeltaType.ADD);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }
            return true;
        }
    }
}
