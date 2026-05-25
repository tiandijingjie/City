using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WarField;
using MeleeHero;
using RangedHero;
using MagicHero;

namespace WarUpgrade
{
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using WBD = WarBuildingDefines;

    //默认的一些需要解锁的士兵,英雄,建筑
    //不需要用户真的去升级
    public class DefaultUpgrade
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

        public bool ImplementUpgrade()
        {
            //unlock melee hero
            if(SoldierCtrl.Instance.UnlockNewSoldierType(WE.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.HeroType.MELEEHERO, true) == false)
                return false;

            //unlock assassin
            if(SoldierCtrl.Instance.UnlockNewSoldierType(WE.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.MeleeType.ASSASSIN, false) == false)
                return false;

            //unlock swordsman
            if(SoldierCtrl.Instance.UnlockNewSoldierType(WE.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.MeleeType.SWORDSMAN, false) == false)
                return false;

            //unlock sharpshooter
            if(SoldierCtrl.Instance.UnlockNewSoldierType(WE.RaceType.Human, SD.TroopType.Ranged, (int)HumanDefines.RangedType.SHARPSHOOTER, false)
                    == false)
                return false;

            //unlock sniper
            if(SoldierCtrl.Instance.UnlockNewSoldierType(WE.RaceType.Human, SD.TroopType.Ranged, (int)HumanDefines.RangedType.SNIPER, false) == false)
                return false;

            //unlock prist
            if(SoldierCtrl.Instance.UnlockNewSoldierType(WE.RaceType.Human, SD.TroopType.Magic, (int)HumanDefines.MagicType.PRIEST, false) == false)
                return false;

            //unlock melee hero flame slash skill
            object detail = (MeleeHeroIndividualData.SkillFlameSlash.ParameterType.LEVEL, 0.0f, GD.CalDeltaType.MIN);
            if(SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, SD.SoldierImproveTarget.SKILL, WE.RaceType.Human, SD.TroopType
                .Melee, (int)HumanDefines.HeroType.MELEEHERO, (MeleeHeroIndividualData.IndividualDataType.FLAMESLASH, detail), true) == false)
                return false;

            //unlock ranged hero frost arrow skill
            detail = (RangedHeroIndividualData.SkillFrostArrow.ParameterType.LEVEL, 0.0f, GD.CalDeltaType.MIN);
            if(SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, SD.SoldierImproveTarget.SKILL, WE.RaceType.Human, SD.TroopType
                   .Ranged, (int)HumanDefines.HeroType.RANGEDHERO, (RangedHeroIndividualData.IndividualDataType.FROSTARROW, detail), true) == false)
                return false;

            //unlock magic hero meteor strike skill
            detail = (MagicHeroIndividualData.SkillMeteorStrike.ParameterType.LEVEL, 0.0f, GD.CalDeltaType.MIN);
            if(SoldierCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMUPGRADE, SD.SoldierImproveTarget.SKILL, WE.RaceType.Human, SD.TroopType
                   .Magic, (int)HumanDefines.HeroType.MAGICHERO, (MagicHeroIndividualData.IndividualDataType.METEORSTRIKE, detail), true) == false)
                return false;

            //unlock fortress
            if(WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.FORTRESS, (int)
                   HumanDefines.FortressType.MAINFORTRESS, "isUlocked", 0, GD.CalDeltaType.MIN) == false)
                return false;

            //unlock barracks
            if(WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.BARRACK, (int)
                   HumanDefines.BarrackType.MELEE, "isUlocked", 0, GD.CalDeltaType.MIN) == false)
                return false;
            if(WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.BARRACK, (int)
                   HumanDefines.BarrackType.RANGED, "isUlocked", 0, GD.CalDeltaType.MIN) == false)
                return false;
            if(WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.BARRACK, (int)
                   HumanDefines.BarrackType.MAGIC, "isUlocked", 0, GD.CalDeltaType.MIN) == false)
                return false;

            //uplock defence towers
            if(WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.DEFENCE, (int)
                   HumanDefines.DefenceType.BASICTOWER, "isUlocked", 0, GD.CalDeltaType.MIN) == false)
                return false;
            if(WarBuildingCtrl.Instance.ApplyImprovement(WE.ImproveSrc.FROMCARD, WE.RaceType.Human, WBD.BuildingMode.DEFENCE, (int)
                   HumanDefines.DefenceType.VOLLEYTOWER, "isUlocked", 0, GD.CalDeltaType.MIN) == false)
                return false;

            return true;
        }
#endregion

#region private functions



#endregion
    }
}
