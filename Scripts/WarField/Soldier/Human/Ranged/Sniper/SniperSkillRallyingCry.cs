using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Sniper;

namespace WarField
{
    using SKD = SkillDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using WE = WarFieldElements;

    //凝聚人心：周围近战士兵，远程士兵的数量会对攻击速度和攻击力加成
    public class SniperSkillRallyingCry : Skill
    {
#region public parameters

#endregion

#region private parameters

        private SniperIndividualData.SkillRallyingCry _oriAttribute;
        private int _meleeCnt, _rangedCnt;
        private object _damageIndexer, _atkSpeedIndexer;
#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)SniperIndividualData.IndividualDataType.RALLYINGCRY; }
        }
#endregion

#region Unity callbacks
        private new void Awake()
        {
            base.Awake();
            _triggerType = new[]
            {
                SKD.SkillTriggerType.PARTERENTERSKILLRANGEDTRIGGER, SKD.SkillTriggerType.PARTERLEAVESKILLRANGEDTRIGGER,
            };
        }
#endregion

#region public functions

#endregion

#region private functions
        protected override bool OnActiveSkill()
        {
            if (_oriAttribute == null)
            {
                _oriAttribute = new SniperIndividualData.SkillRallyingCry((_soldier.gs_oriIndividualData as SniperIndividualData)
                    .gs_individualItems[(int)SniperIndividualData.IndividualDataType.RALLYINGCRY]);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as SniperIndividualData)
                    .gs_individualItems[(int)SniperIndividualData.IndividualDataType.RALLYINGCRY]);
            }
            _name = _oriAttribute.GetDescription().p_name;
            _meleeCnt = _rangedCnt = 0;
            _damageIndexer = _atkSpeedIndexer = null;
            //skill collider will be disabled during the soldier's deinit
            _soldier.AddStateChange(SD.StateSoldierEffectType.SKILLCOLLIDER, _oriAttribute.p_range, GD.CalDeltaType.EQUAL, out float ori);
            return true;
        }

        protected override void OnSkillParterEnter(GameObject parter, WarFieldElements.WarEleType type)
        {
            if (type == WE.WarEleType.SOLDIER)
            {
                FriendlySoldier sd = parter.GetComponent<FriendlySoldier>();
                if (sd.gs_troopType == SD.TroopType.Melee)
                {
                    _meleeCnt++;
                    if (_damageIndexer == null)
                        _damageIndexer = _soldier.AddStateChange(SD.StateSoldierEffectType.DAMAGE, _oriAttribute.p_meleeDamgeInc, GD.CalDeltaType.ADD,
                            out float value);
                    else
                        _soldier.ModifyStateChange(SD.StateSoldierEffectType.DAMAGE, _damageIndexer, false, _oriAttribute.p_meleeDamgeInc,
                            GD.CalDeltaType.ADD, false);
                }
                else if (sd.gs_troopType == SD.TroopType.Ranged)
                {
                    _rangedCnt++;
                    if (_atkSpeedIndexer == null)
                        _atkSpeedIndexer = _soldier.AddStateChange(SD.StateSoldierEffectType.ATTACKSPEED, 1 + _oriAttribute.p_rangedAtkSpeedUp,
                            GD.CalDeltaType.MUL, out float value);
                    else
                        _soldier.ModifyStateChange(SD.StateSoldierEffectType.ATTACKSPEED, _atkSpeedIndexer, false, _oriAttribute.p_rangedAtkSpeedUp,
                            GD.CalDeltaType.ADD, false);
                }
            }
        }

        protected override void OnSkillParterLeave(GameObject parter, WarFieldElements.WarEleType type)
        {
            if (type == WE.WarEleType.SOLDIER)
            {
                FriendlySoldier sd = parter.GetComponent<FriendlySoldier>();
                if (sd.gs_troopType == SD.TroopType.Melee)
                {
                    if (_meleeCnt == 0) //should not happen, some error happen, ignor
                        return;
                    _meleeCnt--;
                    _soldier.ModifyStateChange(SD.StateSoldierEffectType.DAMAGE, _damageIndexer, false, _oriAttribute.p_meleeDamgeInc,
                        GD.CalDeltaType.SUB, false);
                }
                else if (sd.gs_troopType == SD.TroopType.Ranged)
                {
                    if (_rangedCnt == 0)//should not happen, some error happen, ignor
                        return;
                    _rangedCnt--;
                    _soldier.ModifyStateChange(SD.StateSoldierEffectType.ATTACKSPEED, _atkSpeedIndexer, false, _oriAttribute.p_rangedAtkSpeedUp,
                        GD.CalDeltaType.SUB, false);
                }
            }
        }

#endregion
    }
}


