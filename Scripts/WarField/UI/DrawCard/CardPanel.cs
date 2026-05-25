using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;

    //在panel中显示一个类别(CardDefines.CardCategory)的所有card
    public class CardPanel : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters
        private CD.CardCategory _cardCategory;
        private GameObject _cardPfb;
        private bool _beInited = false;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

#endregion

#region public functions

        public bool InitCardPanel(CD.CardCategory cardCategory)
        {
            if(_beInited == true)
                return false;

            _cardCategory = cardCategory;
            _beInited = true;
            return true;
        }

        public void ShowPanel()
        {

        }

        public void HidePanel()
        {

        }
#endregion

#region private functions

#endregion
    }
}

