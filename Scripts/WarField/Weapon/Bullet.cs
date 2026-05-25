using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;
    using WD = WeaponDefines;

    public class Bullet : Projectile
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

        #endregion

        #region private functions
        protected override void ReachTarget()
        {
            if(_toType == WE.WarEleType.SOLDIER)
            {
                Soldier toSd = (Soldier)_toScript;
                if(ReferenceEquals(toSd, null) == false)
                {
                    bool isDie = toSd.BeAttacked(_fromObj, _fromScript, _fromType, _damage, true, true, out float hitValue);
                    if(_fromType == WE.WarEleType.SOLDIER && _triggerSkill == true)
                        ((Soldier)_fromScript).BullectHit(hitValue, isDie, _toType, _toScript);
                    else if(_fromType == WE.WarEleType.BUILDING)
                        ((DefenceBuilding)_fromScript).BullectHit(hitValue, isDie, _toType, _toScript);
                    if(isDie == true) //target die
                    {
                        if(_fromType == WE.WarEleType.SOLDIER) //bullet shoot from soldier
                            ((Soldier)_fromScript).TargetRemove(_toType, _toObj);
                        else if(_fromType == WE.WarEleType.BUILDING)
                            ((DefenceBuilding)_fromScript).TargetRemove(_toType, _toObj);
                    }
                }
            }
            else if(_toType == WE.WarEleType.BUILDING)
            {
                WarBuilding toBD = (WarBuilding)_toScript;
                if(ReferenceEquals(toBD, null) == false)
                {
                    WarBuilding toBd = (WarBuilding)_toScript;
                    bool isDie = toBd.BeAttacked(_fromObj, _fromScript, _fromType, _damage, out float hitValue);
                    if(_fromType == WE.WarEleType.SOLDIER && _triggerSkill == true)
                        ((Soldier)_fromScript).BullectHit(hitValue, isDie, _toType, _toScript);
                    else if(_fromType == WE.WarEleType.BUILDING)
                        ((DefenceBuilding)_fromScript).BullectHit(hitValue, isDie, _toType, _toScript);
                    if(isDie == true) //target die
                    {
                        if(_fromType == WE.WarEleType.SOLDIER) //bullet shoot from soldier
                            ((Soldier)_fromScript).TargetRemove(_toType, _toObj);
                        else if(_fromType == WE.WarEleType.BUILDING)
                            ((DefenceBuilding)_fromScript).TargetRemove(_toType, _toObj);
                    }
                }
            }

            //deinit self
            base.ReachTarget();
        }
        #endregion
    }
}

