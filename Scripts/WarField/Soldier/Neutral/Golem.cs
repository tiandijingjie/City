using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;
    using SD = SoldierDefines;

    //石头人：召唤物
    public class Golem : NeutralMelee
    {
#region public parameters

#endregion

#region private parameters

        private float _lifeTimeCycle;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

#endregion

#region public functions
        //liftTime: live time by seconds
        public bool InitSoldier(WE.FactionType faction, float liftTime, byte mapId)
        {
            if (liftTime >= 0)
                _lifeTimeCycle = Utils.CountOfFixUpdate(liftTime);
            else  //并没有存活的时间限制
                _lifeTimeCycle = -1;

            return base.InitSoldier(faction, mapId);
        }

        public override void RunFixTask(float deltaTime)
        {
            if (_lifeTimeCycle >= 0)
            {
                _lifeTimeCycle--;
                if (_lifeTimeCycle <= 0)
                {
                    ChangeStatusTo(SD.SoldierStatus.DIE);
                }
            }

            base.RunFixTask(deltaTime);
        }

#endregion

#region private functions

#endregion
    }
}


