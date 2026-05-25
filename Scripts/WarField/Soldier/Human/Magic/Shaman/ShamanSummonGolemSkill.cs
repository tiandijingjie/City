using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Shaman;

namespace WarField
{
    using SKD = SkillDefines;
    using GD = GlobalDefines;
    using ED = EffectDefines;
    using WE =WarFieldElements;
    using BFD = BuffDefines;
    using SD = SoldierDefines;

    //- 召唤石人：战斗中召唤石人
    public class ShamanSummonGolemSkill : Skill
    {
#region public parameters

#endregion

#region private parameters

        private ShamanIndividualData.SkillSummonGolem _oriAttribute, _curAttribute;

#endregion

#region private parameters' get set
        public override uint gs_skillType
        {
            get { return (uint)ShamanIndividualData.IndividualDataType.SUMMONGOLEM; }
        }
#endregion

#region Unity callbacks
        private new void Awake()
        {
            base.Awake();
            _triggerType = new[]
            {
                SKD.SkillTriggerType.TIMETRIGGER
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
                _oriAttribute = new ShamanIndividualData.SkillSummonGolem((_soldier.gs_oriIndividualData as ShamanIndividualData)
                    .gs_individualItems[(int)ShamanIndividualData.IndividualDataType.SUMMONGOLEM]);
                _curAttribute = new ShamanIndividualData.SkillSummonGolem(_oriAttribute);
            }
            else
            {
                _oriAttribute.ReInit((_soldier.gs_oriIndividualData as ShamanIndividualData)
                    .gs_individualItems[(int)ShamanIndividualData.IndividualDataType.SUMMONGOLEM]);
                _curAttribute.ReInit(_oriAttribute);
            }
            _name = _oriAttribute.GetDescription().p_name;
            _curAttribute.p_intervalCycle = 0;
            return true;
        }

        protected override void OnSkillUpdate()
        {
            if (_curAttribute.p_intervalCycle > 0)
            {
                _curAttribute.p_intervalCycle -= _timeStep;
            }
            else
            {
                if (_soldier.gs_curStatus == SD.SoldierStatus.ATTACKTATGET)
                {
                    if(_soldier.CanTriggerActiveSkill() == false)
                        return;

                    OnStartSkill();

                    Golem golem = (Golem)SoldierCtrl.Instance.AddSoldierAt(WE.RaceType.Neutral, SD.TroopType.Melee, (int)NeutralDefines.MeleeType
                        .GOLEM, _soldier.gs_transform.position + new Vector3(0.1f, 0.5f, 0), _soldier.gs_mapId);
                    golem.InitSoldier(WE.FactionType.FRIENDLY, _curAttribute.p_duration, _soldier.gs_mapId);
                    if (_curAttribute.p_doubleSummonChance > 0)
                    {
                        int chance = Utils.GetRandomInt();
                        if (chance < _curAttribute.p_doubleSummonChance)
                        {
                            golem = (Golem)SoldierCtrl.Instance.AddSoldierAt(WE.RaceType.Neutral, SD.TroopType.Melee, (int)NeutralDefines.MeleeType
                                .GOLEM, _soldier.gs_transform.position + new Vector3(0.1f, -0.5f, 0), _soldier.gs_mapId);
                            golem.InitSoldier(WE.FactionType.FRIENDLY, _curAttribute.p_duration, _soldier.gs_mapId);
                        }
                    }
                    _curAttribute.p_intervalCycle = _oriAttribute.p_intervalCycle;
                }
            }
        }

#endregion
    }
}
