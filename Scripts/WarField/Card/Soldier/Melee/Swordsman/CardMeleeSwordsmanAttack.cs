using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Swordsman;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //增加剑士12%/45%的攻击速度，8%/35%攻击伤害（4/1）  1.57/1.45     2.28   1.36/1.35  1.84
    public class CardMeleeSwordsmanAttack : CardEffection<CardMeleeSwordsmanAttack>
    {
        public CardMeleeSwordsmanAttack()
        {
            _category = CD.CardCategory.MELEE;
            _levelCnt = new[] { 0, 0, 4, 1}; //CardDefines.CardLevel.MAX
        }

        //激活剑士之后才能被初始化
        public override bool CanBeInit()
        {
            return SoldierCtrl.Instance.IsSoldierAvailable(WE.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.MeleeType.SWORDSMAN);
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            float value = 0;
            if (level == CD.CardLevel.NORMAL)
                value = 1 + 0.12f;
            else if (level == CD.CardLevel.RARE)
                value = 1 + 0.45f;
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.STATE,
                WarFieldElements.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.MeleeType.SWORDSMAN,
                (SoldierDefines.StateSoldierEffectType.ATTACKSPEED, value, GD.CalDeltaType.MUL), false);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve atk speed failed");
                return false;
            }

            if (level == CD.CardLevel.NORMAL)
                value = 1 + 0.08f;
            else if (level == CD.CardLevel.RARE)
                value = 1 + 0.35f;
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.STATE,
                WarFieldElements.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.MeleeType.SWORDSMAN,
                (SoldierDefines.StateSoldierEffectType.DAMAGE, value, GD.CalDeltaType.MUL), false);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve atk damage failed");
                return false;
            }

            return true;
        }
    }
}
