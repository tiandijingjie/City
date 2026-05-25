using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;
    using WBD = WarBuildingDefines;

    public class AmplifierProp : Prop
    {
        private AmplifierConf _thisConf;

        public AmplifierProp()
        {
            _thisConf = new AmplifierConf();
            _conf = _thisConf;
        }

        public override void ActiveProp()
        {
            UIConstructTask.Instance.StartConstructTask(WarBuildingCtrl.Instance.GetBdConf(WE.RaceType.Human, WBD.BuildingMode.PROPBD, (int)
                HumanDefines.PropBdType.AMPLIFIER));
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
