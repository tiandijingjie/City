using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using HeroGeneral;
using RangedHero;

namespace WarField
{
    using SD = SoldierDefines;
    using WE = WarFieldElements;

    //远程英雄
    public class RangedHero : Hero
    {
#region public parameters

#endregion

#region private parameters
        [SerializeField] protected GameObject _weaponPfb = null;
        [SerializeField] protected SD.RemoteAttackStartPosition _shootPos; //出手射击的位置

        protected Vector3 _shootOffset; //bullet start positon offset according to the transform.position
        [SerializeField] protected uint _weaponId;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks
        protected override void Awake()
        {
            base.Awake();

            _isRemote = true;
            if (_shootPos == SD.RemoteAttackStartPosition.OVERHEAD)
                _shootOffset = _halfBodySize * 2;
            else if (_shootPos == SD.RemoteAttackStartPosition.OVERSHOULDER)
            {
                var value = _halfBodySize * 2;
                _shootOffset = new Vector2(value.x, value.y * 2 / 3); //从2/3高度位置出手
            }

            if (_weaponPfb == null)
                GameLogger.LogError($"{name}, not set weapon prefab");
        }
#endregion

#region public functions

        public bool InitHero(byte mapId, int specialSkillType, HeroGenericIndividualData.IndividualDataType[] talents)
        {
            var data = SoldierCtrl.Instance.GetHeroIndividualData(_sdType);
            if (data != null)
            {
                if (_sdConfBeInit == false)
                {
                    _oriIndividualData = new RangedHeroIndividualData((RangedHeroIndividualData)data);
                    _curIndividualData = new RangedHeroIndividualData((RangedHeroIndividualData)_oriIndividualData);
                }
                else
                {
                    _oriIndividualData.ReInitIndividualData((RangedHeroIndividualData)data);
                    _curIndividualData.ReInitIndividualData((RangedHeroIndividualData)_oriIndividualData);
                }
            }
            else
            {
                GameLogger.LogError("Can not get hero individual data");
            }

            InitSpecialSkill((RangedHeroIndividualData.IndividualDataType)specialSkillType);
            return base.InitHero(mapId, talents);
        }

        public override void RemoteRangedAttack(float damage, MonoBehaviour rivalScript, WE.WarEleType rivalType)
        {
            Vector2 startPos = (Vector2)(_transform.position + _shootOffset);
            Vector2 targetPos = Vector2.zero;
            if (rivalType == WE.WarEleType.SOLDIER)
                targetPos = ((Soldier)rivalScript).gs_bullectTargetPos;
            else if (rivalType == WE.WarEleType.BUILDING)
                targetPos = ((WarBuilding)rivalScript).gs_bullectTargetPos;

            int targetGridIndex = ((WarEleParent)rivalScript).gs_gridIndex;
            WeaponCtrl.Instance.FireBullet(
                _weaponId, _faction, damage,
                _mapId, (int)WE.WarEleType.SOLDIER, gs_gridIndex,
                (int)rivalType, targetGridIndex, true,
                startPos, targetPos, _weaponPfb);
        }

        //根据技能需要射出一些特殊的arrow
        public void ShootSpecialWeaponTo(GameObject weaponPfb, uint weaponId, Vector3 startOffset, GameObject target, WE.WarEleType targetType,
            MonoBehaviour rivalScript, float damage)
        {
            Vector2 startPos = (Vector2)(_transform.position + _shootOffset);
            Vector2 targetPos = Vector2.zero;
            if (targetType == WE.WarEleType.SOLDIER)
                targetPos = ((Soldier)rivalScript).gs_bullectTargetPos;
            else if (targetType == WE.WarEleType.BUILDING)
                targetPos = ((WarBuilding)rivalScript).gs_bullectTargetPos;

            int targetGridIndex = ((WarEleParent)rivalScript).gs_gridIndex;
            WeaponCtrl.Instance.FireBullet(
                weaponId, _faction, damage,
                _mapId, (int)WE.WarEleType.SOLDIER, gs_gridIndex,
                (int)targetType, targetGridIndex, true,
                startPos, targetPos, weaponPfb);
        }
#endregion

#region private functions

        //独有的一个技能
        private void InitSpecialSkill(RangedHeroIndividualData.IndividualDataType specialSkillType)
        {
            //没有添加独立技能
            if (specialSkillType == RangedHeroIndividualData.IndividualDataType.MIN)
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
                case RangedHeroIndividualData.IndividualDataType.FROSTARROW:
                    skill = skillTransform.gameObject.AddComponent<RangedHeroFrostArrowSkill>();
                    break;
                case RangedHeroIndividualData.IndividualDataType.ARROWRAIN:
                    skill = skillTransform.gameObject.AddComponent<RangedHeroArrowRainSkill>();
                    break;
                case RangedHeroIndividualData.IndividualDataType.SUDDENDEMISE:
                    skill = skillTransform.gameObject.AddComponent<RangedHeroSuddenDemiseSkill>();
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

