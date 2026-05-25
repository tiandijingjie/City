using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //增加近战士兵5.5%/20%的移动速度（4张/1张）                        1.23/1.2    1.49
    public class CardMeleeMoveSpeed : CardEffection<CardMeleeMoveSpeed>
    {
        public CardMeleeMoveSpeed()
        {
            _category = CD.CardCategory.MELEE;
            _levelCnt = new[] { 0, 0, 4, 1}; //CardDefines.CardLevel.MAX
        }

        //激活任意一种近战高级兵可以被初始化
        public override bool CanBeInit()
        {
            var highLevelSd = SoldierCtrl.Instance.GetHighLevelTypeSoldiers(WE.RaceType.Human, SD.TroopType.Melee);
            foreach (var sd in highLevelSd)
            {
                if(SoldierCtrl.Instance.IsSoldierAvailable(WE.RaceType.Human, SD.TroopType.Melee, sd.p_soldierType) == true)
                    return true;
            }
            return false;
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 1 + 0.055f; //5.5%
            else if (level == CD.CardLevel.RARE)
                value = 1 + 0.2f; //20%
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            List<SoldierConf> confs = SoldierCtrl.Instance.GetSoldiers(WE.RaceType.Human, SD.TroopType.Melee);
            for (int i = 0; i < confs.Count; i++)
            {
                bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SoldierDefines.SoldierImproveTarget.STATE,
                    WarFieldElements.RaceType.Human, SoldierDefines.TroopType.Melee, confs[i].p_soldierType,
                    (SoldierDefines.StateSoldierEffectType.MOVESPEED, value, GD.CalDeltaType.MUL), false);
                if (improveRet == false)
                {
                    GameLogger.LogError($"{this.GetType().Name} improve soldier {confs[i].p_soldierType} failed");
                }
            }

            return true;
        }
    }
}

