using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //缩短技能5.5%/15%冷却时间（4/1）                                             0.797/0.85  0.678
    public class CardSupportPriestSkillTimeStep : CardEffection<CardSupportPriestSkillTimeStep>
    {
        public CardSupportPriestSkillTimeStep()
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
                value = 1 / (1 - 0.055f); //5.5%
            else if (level == CD.CardLevel.RARE)
                value = 1 / (1 - 0.15f); //15%
            else
            {
                GameLogger.LogError($"{this.GetType().Name} not support level {level}");
                return false;
            }

            bool improveRet = SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, SD.SoldierImproveTarget.STATE,
                WarFieldElements.RaceType.Human, SD.TroopType.Magic, (int)HumanDefines.MagicType.PRIEST,
                (SoldierDefines.StateSoldierEffectType.SKILLTIMESTEP, value, GD.CalDeltaType.MUL), false);

            if (improveRet == false)
            {
                GameLogger.LogError($"{this.GetType().Name} improve skill time step failed");
                return false;
            }

            return true;
        }
    }
}
