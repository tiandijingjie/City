using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;
    using SD = SoldierDefines;

    //稻草人,嘲讽技能实在prop使用的时候生效的
    public class TargetDummy : PropBaseBuilding
    {
#region public parameters

#endregion

#region private parameters

        private float _armor; //护甲
        private float _curPhyResistance;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

#endregion

#region public functions

        public override bool InitBuilding(BuildingConf conf, byte mapId)
        {
            base.InitBuilding(conf, mapId);
            _armor = _propBdConf.gs_specConfs["armor"];
            _curPhyResistance = SD.GetPhysicsResistance(_armor); //与士兵同样的计算护甲的算法
            StartCoroutine(ChangeBody());
            return true;
        }

        protected override float IndividualCalculateDamage(float damage)
        {
            return damage * (1 - _curPhyResistance);
        }
#endregion

#region private functions
        //必须改变body, 如果是之前static那种,那直接添加到collider的范围的时候是无法触发collider回调的
        private IEnumerator ChangeBody()
        {
            yield return null;
            yield return null;
            GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        }
#endregion
    }
}
