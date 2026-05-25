using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WD = WeaponDefines;
    using WE = WarFieldElements;

    //没有具体目标
    //子弹必须有collider2D
    public class NoTargetBullet : Projectile
    {
#region public parameters

#endregion

#region private parameters

        private Vector3 _dirVec;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if(_beInited == false)
                return;

            string tag = collision.gameObject.tag;
            WE.WarEleType colType = WarFieldUtil.GetWarEleType(tag);
            if (colType == WE.WarEleType.SOLDIER && WarFieldUtil.GetFactionByTag(tag) != _faction)
            {
                Soldier sd = collision.gameObject.GetComponent<Soldier>();
                if (sd != null)
                {
                    float hitValue;
                    bool isDead = sd.BeAttacked(_fromObj, _fromScript, _fromType, _damage, true, true, out hitValue);
                    //not call BullectHit( ),because this bullet is just the skill
                }
            }
            //can not attack building
        }

#endregion

#region public functions

#endregion

#region private functions

        protected override bool OnInit()
        {
            if (_faction == WE.FactionType.FRIENDLY)
                gameObject.layer = LayerMask.NameToLayer("FriendlySoldierAttack");
            else if(_faction == WE.FactionType.ENEMY)
                gameObject.layer = LayerMask.NameToLayer("EnemySoldierAttack");
            return base.OnInit();
        }

#endregion
    }
}

