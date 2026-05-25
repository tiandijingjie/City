using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;

    public class DrawCard : MonoBehaviour
    {
#region public parameters

        public static DrawCard Instance;
        public readonly int MaxCardNum = 5;

#endregion

#region private parameters

        [SerializeField] private GameObject _cardPfb;

        //private CardTypeInPool[] _typePool; //the types of card that user can get in next 3 draw
        private GameObject[] _cardsInRound;
        private RectTransform _rectTrans;

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
                return;
            }

            Instance = this;

            _rectTrans = GetComponent<RectTransform>();
            //_typePool = new CardTypeInPool[(int)CD.CardTypeOnUI.MAX];
            _cardsInRound = new GameObject[MaxCardNum];
            ResetPool();
            HideDrawCardView();
        }

        public void ExitBt()
        {
            HideDrawCardView();
        }

        public void CardCategoryBt(int category)
        {
            //CreateCards((CD.CardTypeOnUI)category);
        }

#endregion

#region public functions

        public bool InitView()
        {
            return true;
        }

        public void CardBeChosen(int index)
        {
            HideDrawCardView();
        }

        public void ShowDrawCardView()
        {
            _rectTrans.anchoredPosition = Vector2.zero;
        }

        public void HideDrawCardView()
        {
            _rectTrans.anchoredPosition = new Vector2(0, 3000);

            for (int i = 0; i < _cardsInRound.Length; i++)
            {
                if (_cardsInRound[i] != null)
                {
                    Destroy(_cardsInRound[i]);
                    _cardsInRound[i] = null;
                }
            }
        }

#endregion

#region private functions

        // private void CreateCards(CD.CardTypeOnUI cardTypeOnUI)
        // {
        //     string cardName = null;
        //     switch(cardTypeOnUI)
        //     {
        //         case CD.CardTypeOnUI.MELEE:
        //             cardName = CardCtrl.Instance.TakeCard(CD.CardTarget.BARRACK, cardTypeOnUI, (int)CD.BarrackCardCategory.SOLDIER, 1);
        //             break;
        //         case CD.CardTypeOnUI.RANGED:
        //             //cardName = CardCtrl.Instance.TakeCard(CD.CardTarget.BARRACK, cardTypeOnUI, (int)CD.BarrackCardCategory.ATTRIBUTE, 1);
        //             break;
        //         case CD.CardTypeOnUI.MAGIC:
        //             //cardName = CardCtrl.Instance.TakeCard(CD.CardTarget.BARRACK, cardTypeOnUI, (int)CD.BarrackCardCategory.ATTRIBUTE, 1);
        //             break;
        //         case CD.CardTypeOnUI.SUPPORT:
        //             //cardName = CardCtrl.Instance.TakeCard(CD.CardTarget.BARRACK, cardTypeOnUI, (int)CD.BarrackCardCategory.ATTRIBUTE, 1);
        //             break;
        //         case CD.CardTypeOnUI.TOWER:
        //             //cardName = CardCtrl.Instance.TakeCard(CD.CardTarget.TOWER, cardTypeOnUI, (int)CD.BarrackCardCategory.ATTRIBUTE, 1);
        //             break;
        //         case CD.CardTypeOnUI.HERO:
        //             //cardName = CardCtrl.Instance.TakeCard(CD.CardTarget.HERO, cardTypeOnUI, (int)CD.BarrackCardCategory.ATTRIBUTE, 1);
        //             break;
        //         default:
        //             break;
        //     }
        //
        //     //turn class name to class instance then add into gameobject
        //     Type type = Type.GetType(cardName);
        //     if (type != null)
        //     {
        //         GameObject cardObj = Instantiate(_cardPfb, transform);
        //         cardObj.AddComponent(type);
        //         Card card = cardObj.GetComponent<Card>();
        //         card.InitCard(0, this);
        //         _cardsInRound[0] = cardObj;
        //     }
        // }

        //calculate the _typePool
        private void ResetPool()
        {
            // int totalCard = MaxCardNum * 3; //every round draw 5 card, total 3 round
            // for (int i = 0; i < (int)CD.CardTypeOnUI.MAX; i++)
            // {
            //     CD.CardTypeOnUI typeOnUI = (CD.CardTypeOnUI)i;
            //
            //     for (int j = 0; j < (int)CD.CardTarget.MAX; j++)
            //     {
            //
            //     }
            // }
        }

#endregion
    }
}
