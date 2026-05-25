using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //增加盾兵和剑士10%/30%血量（4/1）                                        1.46/1.3   1.9
    public class CardMeleeMaxHp : CardEffection<CardMeleeMaxHp>
    {
        public CardMeleeMaxHp()
        {
            _category = CD.CardCategory.MELEE;
            _levelCnt = new[] { 0, 0, 4, 1};//CardDefines.CardLevel.MAX
        }

        //激活任意一种近战高级兵可以被初始化
        public override bool CanBeInit()
        {
            if (SoldierCtrl.Instance.IsSoldierAvailable(WE.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.MeleeType.SWORDSMAN) == true)
                return true;
            if(SoldierCtrl.Instance.IsSoldierAvailable(WE.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.MeleeType.SHIELDSOLDIER) == true)
                return true;
            return false;
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 1 + 0.1f; //10%
            else if (level == CD.CardLevel.RARE)
                value = 1 + 0.3f; //30%
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.STATE,
                WarFieldElements.RaceType.Human, SoldierDefines.TroopType.Melee, (int)HumanDefines.MeleeType.SWORDSMAN,
                (SoldierDefines.StateSoldierEffectType.HEALTH, value, GD.CalDeltaType.MUL), false);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve soldier swordsman failed");
            }

            improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SoldierDefines.SoldierImproveTarget.STATE,
                WarFieldElements.RaceType.Human, SoldierDefines.TroopType.Melee, (int)HumanDefines.MeleeType.SHIELDSOLDIER,
                (SoldierDefines.StateSoldierEffectType.HEALTH, value, GD.CalDeltaType.MUL), false);
            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve soldier shield soldier failed");
            }

            return true;
        }
    }
}

