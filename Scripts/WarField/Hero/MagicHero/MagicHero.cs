using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using MagicHero;
using HeroGeneral;

namespace WarField
{
    using SD = SoldierDefines;
    using WE = WarFieldElements;

    //远程英雄
   public class MagicHero : Hero
   {
#region public parameters

#endregion

#region private parameters
       [SerializeField] protected GameObject _weaponPfb = null;
       [SerializeField] protected SD.RemoteAttackStartPosition _shootPos; //出手射击的位置

       protected Vector3 _shootOffset; //bullet start positon offset according to the transform.position
       protected long _weaponId;
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

           if (_weaponPfb != null)
               _weaponId = WeaponCtrl.Instance.GetWeaponID(_race, (long)_troopType, (long)_sdType, 1, WE.WarEleType.SOLDIER);
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
                   _oriIndividualData = new MagicHeroIndividualData((MagicHeroIndividualData)data);
                   _curIndividualData = new MagicHeroIndividualData((MagicHeroIndividualData)_oriIndividualData);
               }
               else
               {
                   _oriIndividualData.ReInitIndividualData((MagicHeroIndividualData)data);
                   _curIndividualData.ReInitIndividualData((MagicHeroIndividualData)_oriIndividualData);
               }
           }
           else
           {
               GameLogger.LogError("Can not get hero individual data");
           }

           InitSpecialSkill((MagicHeroIndividualData.IndividualDataType)specialSkillType);
           return base.InitHero(mapId, talents);
       }

       public override void RemoteRangedAttack(float damage, MonoBehaviour rivalScript, WE.WarEleType rivalType)
       {
           Projectile bt = WeaponCtrl.Instance.GetProjectile(_race, _weaponId, _weaponPfb);
           Vector3 startPos = _transform.position + _shootOffset;
           bt.gs_transform.position = startPos;
           Vector2 targetPos = Vector2.zero;
           if (rivalType == WE.WarEleType.SOLDIER) //shell
               targetPos = ((Soldier)rivalScript).gs_bullectTargetPos;
           else if (rivalType == WE.WarEleType.BUILDING)
               targetPos = ((WarBuilding)rivalScript).gs_bullectTargetPos;

           //default for ProjectileTypes.BULLET only, for SHELL  solider need inplemente it self
           bt.InitProjectile(gameObject, WE.WarEleType.SOLDIER, this, startPos, _rival, rivalType, rivalScript, targetPos, 20f, _faction, damage,
               _mapId);
       }

#endregion

#region private functions
//独有的一个技能
       private void InitSpecialSkill(MagicHeroIndividualData.IndividualDataType specialSkillType)
       {
           //没有添加独立技能
           if (specialSkillType == MagicHeroIndividualData.IndividualDataType.MIN)
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
               case MagicHeroIndividualData.IndividualDataType.METEORSTRIKE:
                   skill = skillTransform.gameObject.AddComponent<MagicHeroMeteorStrikeSkill>();
                   break;
               case MagicHeroIndividualData.IndividualDataType.STORMFURY:
                   skill = skillTransform.gameObject.AddComponent<MagicHeroStormFurySkill>();
                   break;
               case MagicHeroIndividualData.IndividualDataType.FROZENSEAL:
                   skill = skillTransform.gameObject.AddComponent<MagicHeroFrozenSealSkill>();
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

