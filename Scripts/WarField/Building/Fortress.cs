using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    public class Fortress : WarBuilding
    {
#region public parameters

#endregion

#region private parameters
        [SerializeField] protected FortressConf _fortresskConf; //just the _bdConf
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

#endregion

#region public functions

        public bool InitBuilding(BuildingConf conf, byte mapId)
        {
            _bdConf = new FortressConf(conf);
            base.InitBuilding(mapId);
            _fortresskConf = _bdConf as FortressConf;
            return true;
        }
#endregion

#region private functions

#endregion
    }
}

