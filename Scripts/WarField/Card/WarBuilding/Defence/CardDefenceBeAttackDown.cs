using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;
    using GD = GlobalDefines;

    //防御建筑受到伤害降低4%/9%（4/1）                           0.15/0.09                         0.686
    public class CardDefenceBeAttackDown : CardEffection<CardDefenceBeAttackDown>
    {
        public CardDefenceBeAttackDown()
        {
            _category = CD.CardCategory.BUILDING;
            _levelCnt = new[] { 0, 0, 4, 1 };
        }

        public override bool CanBeInit()
        {
            return true;
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 1 - 0.04f;  //4%
            else if (level == CD.CardLevel.RARE)
                value = 1 - 0.09f; //9%
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            for (int i = 1; i < (int)HumanDefines.DefenceType.MAX; i++)
            {
                bool improveRet = WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.DEFENCE, i,
                    "beAttackDown", value, GD.CalDeltaType.MUL);
                if (improveRet == false)
                {
                    GameLogger.LogError($"{this.GetType().Name} improve failed");
                    return false;
                }
            }

            return true;
        }
    }
}

