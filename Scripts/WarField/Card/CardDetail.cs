using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WarField
{
    using CD = CardDefines;

    //具体呈现在DrawCard UI上一张card的信息和状态
    public class CardDetail
    {
#region public parameters

#endregion

#region private parameters

        private CardEffection _effection;
        private CD.CardLevel _level; //这一张卡的等级

#endregion

#region private parameters' get set

        public CardEffection gs_cardEffect
        {
            get { return _effection; }
        }

        public CD.CardLevel gs_cardLevel
        {
            get { return _level; }
        }
#endregion

#region Unity callbacks

        public void BeChosen()
        {
            TakeEffect();
        }

#endregion

#region public functions

        public virtual bool InitCardDetail(CardEffection effection, CD.CardLevel level)
        {
            _effection = effection;
            _level = level;
            return true;
        }

#endregion

#region private functions

        protected virtual void TakeEffect()
        {
            if(_effection.gs_category != CD.CardCategory.PROP)
                _effection.TakeAndActive(_level, true, true); //debug == true
            else
                _effection.TakeAndActive(_level, false, true);
        }

#endregion
    }
}

