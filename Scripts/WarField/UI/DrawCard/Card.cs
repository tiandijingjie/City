using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WarField
{
    using CD = CardDefines;

    //显示在UI上的具体card
    public class Card : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] private Image _lockBtImg; //锁定按钮image
        [SerializeField] private Image _soldOutImg; //已出售的image

        private CD.CardCategory _cardCategory = CD.CardCategory.MIN;
        private CardDetail _cardDetail; //在没有锁定的情况下,_cardDetail每次在show的时候会重新获取
        private bool _isActive;
        private bool _isLocked; //锁定状态, 不更新_cardDetail

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

#endregion

#region public functions

        public bool InitCard(CD.CardCategory cardCategory)
        {
            if(_cardCategory != CD.CardCategory.MIN)
                return false;
            _cardCategory = cardCategory;
            _isActive = true;

            return true;
        }

        public void ShowCard()
        {
            if(_cardDetail != null)
                return;
            _cardDetail = CardCtrl.Instance.TakeCardForPickup(_cardCategory);
            if (_cardDetail == null) //获取不到新的card,隐藏自己
            {
                gameObject.SetActive(false);
                _isActive = false;
                return;
            }

            if (_isActive == false)
            {
                gameObject.SetActive(true);
                _isActive = true;
            }
        }

        public void HideCard()
        {
            if(_isLocked == false)
                _cardDetail = null;
        }

        public void LockCard()
        {

        }
#endregion

#region private functions

#endregion
    }
}

