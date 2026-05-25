using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using HeroGeneral;

namespace WarField
{
    using SKD = SkillDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //友军攻速增强 增加全体与英雄同类型士兵的攻击速度 攻速增加:0.5(每秒多攻击0.5次)
    public class HeroAlliedAttackSpeedBoostTalent : Talent, ISoldierProductionNotify
    {
#region public parameters

#endregion

#region private parameters
        private HeroGenericIndividualData.TalentAlliedAttackSpeedBoost _curTalent = null;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks
        protected override void Awake()
        {
            base.Awake();
            // _triggerType = new[] { };
            // for (int i = 0; i < _triggerType.Length; i++)
            //     _soldier.RegisterTalent(this, _triggerType[i]);  //talent need to do register during awake by itself
        }
#endregion

#region public functions
        protected override bool OnActiveTalent()
        {
            if (_curTalent == null)
                _curTalent = (HeroGenericIndividualData.TalentAlliedAttackSpeedBoost)(((Hero)_soldier).gs_genericData as HeroGenericIndividualData)
                    .gs_individualItems[(int)HeroGenericIndividualData.IndividualDataType.ALLIEDATTACKSPEEDBOOST];
            _name = _curTalent.GetDescription().p_name;
            SoldierCtrl.Instance.RegisterSoliderProduceNotify(this, WE.FactionType.FRIENDLY, ((Hero)_soldier).gs_troopType);

            return true;
        }

        public void SoldierProduceIntf(WE.FactionType faction, SoldierDefines.TroopType troop, Soldier soldier, Vector2 pos)
        {
            if(faction == WE.FactionType.FRIENDLY)
                soldier.AddStateChange(SD.StateSoldierEffectType.ATTACKSPEED, _curTalent.p_attackSpeedAdd, GD.CalDeltaType.ADD, out float oriValue);
        }
#endregion

#region private functions

#endregion
    }
}
