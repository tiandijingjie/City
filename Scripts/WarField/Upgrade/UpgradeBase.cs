using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WarUpgrade;

namespace WarUpgrade
{
    using UD = UpgradeDefine;

    //具体实现的一个upgrade的效果
    public abstract class UpgradeBase
    {
#region public parameters

#endregion

#region private parameters

        protected UpgradeNode p_node = null;

#endregion

#region private parameters' get set

        public virtual UD.UpgradeType gs_type
        {
            get { return UD.UpgradeType.MIN; }
        }

#endregion

#region public functions


        public virtual bool Init(UpgradeNode node)
        {
            if (p_node != null)
                return false;
            p_node = node;
            return true;
        }

        public bool ImplementUpgrade()
        {
            if (p_node == null) //should not happen
                return false;
            if(p_node.p_curLevel == 0) //no active yet
                return true;
            return OnImplementUpgrade();
        }

#endregion

#region private functions

        protected abstract bool OnImplementUpgrade();

#endregion
    }
}

