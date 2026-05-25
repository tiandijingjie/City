using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using WRD = WarResDefine;
    using GD = GlobalDefines;

    //增加低级能量宝石蕴含的能量1  （3/1）    4
    public class CardLowEnergyGemAddEnergyInGem : CardEffection<CardLowEnergyGemAddEnergyInGem>
    {
        public CardLowEnergyGemAddEnergyInGem()
        {
            _category = CD.CardCategory.RESOURCE;
            _levelCnt = new[] { 0, 0, 3, 1 };
        }

        public override bool CanBeInit()
        {
            return true;
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 1;
            else if (level == CD.CardLevel.RARE)
                value = 1;
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = WarResCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WRD.ResTypes.OCULARSTONE, "energyInLowStone", value, GD.CalDeltaType.ADD);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }
            return true;
        }
    }
}
