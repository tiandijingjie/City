using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using ShieldSoldier;

namespace WarField
{
    using SKD = SkillDefines;
    using BFD = BuffDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using WE = WarFieldElements;

    //守护领域：降低周围敌人的攻击速度和攻击力，不可叠加
    public class ShieldSoldierSanctarySkill : Skill
    {
#region public parameters

#endregion

#region private parameters
        private struct StateItem
        {
            public object p_atkSpeedIndex;
            public object p_damageIndex;
        }

        private ShieldSoldierIndividualData.SkillSanctuary _oriAttribute;
        private Dictionary<Soldier, StateItem> _stateChangeList;
        private (SD.StateSoldierEffectType, float, GD.CalDeltaType, float, string, BFD.BuffStrategy, object) _damgeObj, _atkSpdObj;

#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)ShieldSoldierIndividualData.IndividualDataType.SANCTUARY; }
        }
#endregion

#region Unity callbacks
        private new void Awake()
        {
            base.Awake();
            _triggerType = new[] { SKD.SkillTriggerType.RIVALENTERSKILLRANGEDTRIGGER,  SKD.SkillTriggerType.RIVALLEAVESKILLRANGEDTRIGGER};
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override bool OnActiveSkill()
        {
            if (_oriAttribute == null)
            {
                _oriAttribute = new ShieldSoldierIndividualData.SkillSanctuary((_soldier.gs_oriIndividualData as ShieldSoldierIndividualData)
                    .gs_individualItems[(int)ShieldSoldierIndividualData.IndividualDataType.SANCTUARY]);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as ShieldSoldierIndividualData)
                    .gs_individualItems[(int)ShieldSoldierIndividualData.IndividualDataType.SANCTUARY]);
            }
            _name = _oriAttribute.GetDescription().p_name;
            //skill collider will be disabled during the soldier's deinit
            _soldier.AddStateChange(SD.StateSoldierEffectType.SKILLCOLLIDER, _oriAttribute.p_range, GD.CalDeltaType.EQUAL, out float ori);
            _stateChangeList = new Dictionary<Soldier, StateItem>();

            _damgeObj = (SD.StateSoldierEffectType.DAMAGE, _oriAttribute.p_damageDown,
                GD.CalDeltaType.MUL, -1f, "ShieldSoldierSanctarySkill", BFD.BuffStrategy.APPEND, (object)this);
            _atkSpdObj = (SD.StateSoldierEffectType.ATTACKSPEED, _oriAttribute.p_attackSpeedDown,
                GD.CalDeltaType.MUL, -1f, "ShieldSoldierSanctarySkill", BFD.BuffStrategy.APPEND, (object)this);
            return true;
        }

        protected override void OnSkillRivalEnter(GameObject rival, WarFieldElements.WarEleType type)
        {
            Soldier sd = rival.GetComponent<Soldier>();
            object atkSpeedItem = null;
            sd.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _atkSpdObj, ref atkSpeedItem);

            object damageItem = null;
            sd.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _damgeObj, ref damageItem);

            StateItem item = new StateItem();
            item.p_atkSpeedIndex = atkSpeedItem;
            item.p_damageIndex = damageItem;
            _stateChangeList.TryAdd(sd, item);
        }

        protected override void OnSkillRivalLeave(GameObject rival, WarFieldElements.WarEleType type)
        {
            if (type == WE.WarEleType.SOLDIER)
            {
                Soldier sd = rival.GetComponent<Soldier>();
                StateItem item;
                if (_stateChangeList.TryGetValue(sd, out item) == true)
                {
                    var value = (item.p_atkSpeedIndex, (object)this);
                    sd.StopPartOfBuff(BFD.SoldierBuffType.STATE, in value);
                    var value1 = (item.p_damageIndex, (object)this);
                    sd.StopPartOfBuff(BFD.SoldierBuffType.STATE, in value1);
                    _stateChangeList.Remove(sd);
                }
            }
        }

        protected override void OnDeactiveSkill()
        {
            foreach (var kvp in _stateChangeList)
            {
                Soldier soldier = kvp.Key;
                StateItem stateItem = kvp.Value;
                var value = (stateItem.p_atkSpeedIndex, (object)this);
                soldier.StopPartOfBuff(BFD.SoldierBuffType.STATE, in value);
                var value1 = (stateItem.p_damageIndex, (object)this);
                soldier.StopPartOfBuff(BFD.SoldierBuffType.STATE, in value1);
            }
        }
#endregion
    }
}

