using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using WRD = WarResDefine;
    using GD = GlobalDefines;

    //增加50%高级能量宝石蕴含的能量 （2/1）   237%
    public class CardHighEnergyGemAddEnergyInGem : CardEffection<CardHighEnergyGemAddEnergyInGem>
    {
        public CardHighEnergyGemAddEnergyInGem()
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
                value = 1 + 0.5f;
            else if (level == CD.CardLevel.RARE)
                value = 1 + 0.5f;
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = WarResCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WRD.ResTypes.OCULARSTONE, "energyInHighStone", value, GD.CalDeltaType.MUL);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve failed");
                return false;
            }
            return true;
        }
    }
}
