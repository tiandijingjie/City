using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using BFD = BuffDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;
    using WE =WarFieldElements;
    using WBD = WarBuildingDefines;

    //地雷: 放置地面上,对范围3.5内的敌人造成50点伤害，放置之后需要2s准备时间，延迟3秒爆炸
    public class LandmineProp : Prop
    {
        private LandmineConf _thisConf;

        public LandmineProp()
        {
            _thisConf = new LandmineConf();
            _conf = _thisConf;
        }

        public override void ActiveProp()
        {
            UIConstructTask.Instance.StartConstructTask(WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Human, WBD.BuildingMode.PROPBD, (int)
                HumanDefines.PropBdType.LANDMINE));
            UIConstructTask.Instance.RegisterConstructCallback(Callback);
        }

        private void Callback(WarBuilding bd, bool success, Vector2 position)
        {
            if(success == true)
                UseProp();
            else
                base.GiveupProp();
        }
    }
}
