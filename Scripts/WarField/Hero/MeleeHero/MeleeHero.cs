using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using MeleeHero;
using HeroGeneral;

namespace WarField
{
    //近战英雄
    public class MeleeHero : Hero
    {
#region public parameters

#endregion

#region private parameters

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

#endregion

#region public functions
        public bool InitHero(byte mapId, int specialSkillType, HeroGenericIndividualData.IndividualDataType[] talents)
        {
            var data = SoldierCtrl.Instance.GetHeroIndividualData(_sdType);
            if (data != null)
            {
                if (_sdConfBeInit == false)
                {
                    _oriIndividualData = new MeleeHeroIndividualData((MeleeHeroIndividualData)data);
                    _curIndividualData = new MeleeHeroIndividualData((MeleeHeroIndividualData)_oriIndividualData);
                }
                else
                {
                    _oriIndividualData.ReInitIndividualData((MeleeHeroIndividualData)data);
                    _curIndividualData.ReInitIndividualData((MeleeHeroIndividualData)_oriIndividualData);
                }
            }
            else
            {
                GameLogger.LogError("Can not get hero individual data");
            }
            InitSpecialSkill((MeleeHeroIndividualData.IndividualDataType)specialSkillType);
            return base.InitHero(mapId, talents);
        }
#endregion

#region private functions

        //独有的一个技能
        private void InitSpecialSkill(MeleeHeroIndividualData.IndividualDataType specialSkillType)
        {
            //没有添加独立技能
            if(specialSkillType == MeleeHeroIndividualData.IndividualDataType.MIN)
                return;

            var skillTransform = _transform.Find("Skill");
            if (skillTransform == null)
            {
                GameLogger.LogError("没有找到 Skill 子物体！");
                return;
            }

            Skill skill = null;
            switch (specialSkillType)
            {
                case MeleeHeroIndividualData.IndividualDataType.FLAMESLASH:
                    skill = skillTransform.gameObject.AddComponent<MeleeHeroFlameSlashSkill>();
                    break;
                case MeleeHeroIndividualData.IndividualDataType.WHIRLWINDSLASH:
                    skill = skillTransform.gameObject.AddComponent<MeleeHeroWhirlwindSlashSkill>();
                    break;
                case MeleeHeroIndividualData.IndividualDataType.CRISISUNLEASHED:
                    skill = skillTransform.gameObject.AddComponent<MeleeHeroCrisisUnleashedSkill>();
                    break;
                default:
                    GameLogger.LogError($"Fail to add special skill to hero {specialSkillType}");
                    break;
            }

            if (skill != null)
            {
                skill.InitSkillObject();
                skill.ActiveSkill();
            }
        }

#endregion
    }
}

