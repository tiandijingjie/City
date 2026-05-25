using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WarField
{
    using CD = CardDefines;

    public class CardCtrl : MonoBehaviour
    {
#region public parameters

        public static CardCtrl Instance;

#endregion

#region private parameters

        //card 都是在主线程运行的，所以不用加锁
        private DataList<CardEffection>[] _allCardEffections; //[CardDefines.CardCategory]  所有CardEffection的实例
        private DataList<CardEffection>[] _unactiveCardEffections; //[CardDefines.CardCategory]  所有未激活的CardEffection的实例
        private DataList<CardDetail>[,] _cardPool; //[CardDefines.CardCategory, CardDefines.CardLevel]
        private int[,] _cardNumInPool; //[CardDefines.CardCategory, CardDefines.CardLevel], _cardPool中每个分量的数量
        private int[,] _cardChance;
        private int[] _levelChance = new int[] { 0, 45, 35, 20 }; //一般情况下各个等级卡的抽取概率,normal卡的数量最多，base的卡很少

        private int _cardNumOfType = 3; //每一个CardCategory抽取的card的数量
        private int _cardLockNum = 1; //能够锁定的卡牌数

        private bool _beInit;

#endregion

#region private parameters' get set

        public int gs_cardNumOfType
        {
            get { return _cardNumOfType; }
        }

        public int gs_cardLockNum
        {
            get { return _cardLockNum; }
        }
#endregion

#region Unity callbacks

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _allCardEffections = new DataList<CardEffection>[(int)CD.CardCategory.MAX];
            _unactiveCardEffections = new DataList<CardEffection>[(int)CD.CardCategory.MAX];
            _cardPool = new DataList<CardDetail>[(int)CD.CardCategory.MAX, (int)CD.CardLevel.MAX];
            _cardNumInPool = new int[(int)CD.CardCategory.MAX, (int)CD.CardLevel.MAX];
            _cardChance = new int[(int)CD.CardCategory.MAX, (int)CD.CardLevel.MAX];
            for (int i = 1; i < (int)CD.CardCategory.MAX; i++)
            {
                _allCardEffections[i] = new DataList<CardEffection>(false, true);
                _unactiveCardEffections[i] = new DataList<CardEffection>(false, true);
                for (int j = 1; j < (int)CD.CardLevel.MAX; j++)
                {
                    _cardPool[i, j] = new DataList<CardDetail>(false, true);
                }
            }

            _beInit = false;
        }

#endregion

#region public functions

        public bool InitCarCtrl()
        {
            InitCardEffectionPool();
            _beInit = true;
            return true;
        }

        //check does it still card in pool of a category
        public bool CanGetCardInCategory(CD.CardCategory category)
        {
            if (Utils.IsEnumInRange(category, CD.CardCategory.MIN, CD.CardCategory.MAX) == false)
                return false;
            int count = 0;
            for (int i = 1; i < (int)CD.CardLevel.MAX; i++)
                count += _cardNumInPool[(int)category, i];

            if (count == 0)
                return false;
            return true;
        }

        //get a card from pool of a category for user to pick up
        public CardDetail TakeCardForPickup(CD.CardCategory category)
        {
            if (Utils.IsEnumInRange(category, CD.CardCategory.MIN, CD.CardCategory.MAX) == false)
                return null;

            int total = 0;
            for (int i = 1; i < (int)CD.CardLevel.MAX; i++)
            {
                total += _cardNumInPool[(int)category, i];
            }

            if (total == 0)
            {
                GameLogger.LogWarning($"Category {category} card pool is empty, should not choose this");
                return null;
            }

            int chance = Utils.GetRandomInt();
            CD.CardLevel level;
            if (chance < _cardChance[(int)category, (int)CD.CardLevel.BASE])
                level = CD.CardLevel.BASE;
            else if (chance < _cardChance[(int)category, (int)CD.CardLevel.BASE] + _cardChance[(int)category, (int)CD.CardLevel.NORMAL])
                level = CD.CardLevel.NORMAL;
            else
                level = CD.CardLevel.RARE;

            for (int i = 0; i < 3; i++) //确保这个level里面还有card
            {
                if (_cardPool[(int)category, (int)level].Count == 0)
                    level++;
                else
                    break;

                if (level == CD.CardLevel.RARE)
                    level = CD.CardLevel.BASE;
            }

            DataList<CardDetail> list = _cardPool[(int)category, (int)level];

            int index = Random.Range(0, list.Count);
            CardDetail ret = list.GetByIndex(index);
            list.RemoveItemAt(index);
            _cardNumInPool[(int)category, (int)level]--;
            //将card从pool取出供给user选择，card被抽取出来之后会改变抽卡概率（_cardChance）
            AdjustChance(category);

            return ret;
        }

        public void AfterPickUp(CD.CardCategory category, List<CardDetail> cardsNotpicked)
        {
            int cnt = cardsNotpicked.Count;

            //add not picked cards into pool again
            for (int i = 0; i < cnt; i++)
            {
                CardDetail cardDetail = cardsNotpicked[i];
                _cardPool[i, (int)cardDetail.gs_cardLevel].AddItem(cardDetail);
                _cardNumInPool[i, (int)cardDetail.gs_cardLevel]++;
            }

            var list = _unactiveCardEffections[(int)category];
            List<CardEffection> initList = null;
            for (int i = 0; i < list.Count; i++)
            {
                if (_unactiveCardEffections[(int)category].GetByIndex(i).CanBeInit() == true)
                {
                    if (initList == null)
                        initList = new List<CardEffection>();
                    initList.Add(_unactiveCardEffections[(int)category].GetByIndex(i));
                }
            }

            if (initList != null)
            {
                foreach (var card in initList)
                {
                    _unactiveCardEffections[(int)category].RemoveItem(card);

                    //init and add into pool
                    card.InitCardEffection();
                    int[] levelCnts = card.gs_levelCnt;
                    for (int k = 1; k < (int)CD.CardLevel.MAX; k++)
                    {
                        int levelCnt = levelCnts[k];
                        for (int l = 0; l < levelCnt; l++)
                        {
                            CardDetail cardDetail = new CardDetail();
                            cardDetail.InitCardDetail(card, (CD.CardLevel)k);
                            _cardPool[(int)category, k].AddItem(cardDetail);
                            _cardNumInPool[(int)category, k]++;
                        }
                    }
                }
            }

            AdjustChance(category);
        }

        //增加可以锁定的卡牌上限
        public void AddCardLockNum()
        {
            _cardLockNum++;
        }
#endregion

#region private functions

        private void InitCardEffectionPool()
        {
            // 获取所有继承 CardEffection 的类型
            IEnumerable<Type> derivedTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(CardEffection)));

            List<CardEffection> list = new List<CardEffection>();
            foreach (Type type in derivedTypes)
            {
                // 通过反射实例化对象
                CardEffection instance = (CardEffection)Activator.CreateInstance(type);
                list.Add(instance);
            }

            foreach (var cardEffect in list)
            {
                CD.CardCategory category = cardEffect.gs_category;
                _allCardEffections[(int)category].AddItem(cardEffect);

                if (cardEffect.CanBeInit() == true)
                {
                    cardEffect.InitCardEffection();
                    int[] levelCnts = cardEffect.gs_levelCnt;
                    for (int k = 1; k < (int)CD.CardLevel.MAX; k++)
                    {
                        int levelCnt = levelCnts[k];
                        for (int l = 0; l < levelCnt; l++)
                        {
                            CardDetail cardDetail = new CardDetail();
                            cardDetail.InitCardDetail(cardEffect, (CD.CardLevel)k);
                            _cardPool[(int)category, k].AddItem(cardDetail);
                            _cardNumInPool[(int)category, k]++;
                        }
                    }
                }
                else
                    _unactiveCardEffections[(int)category].AddItem(cardEffect);
            }

            for (int i = 1; i < (int)CD.CardCategory.MAX; i++)
            {
                AdjustChance((CD.CardCategory)i);
            }
        }

        //change the draw card chance of every level
        //一般情况下抽卡的概率  Base:45%   Normal:35%   Rare:20%，同等级的卡抽取的概率相等
        //当一个等级的卡的数量是0的话，这个等级的chance 8/2开分给其他两个等级
        private void AdjustChance(CD.CardCategory category)
        {
            int[] cntArrr = new int[(int)CD.CardLevel.MAX];
            cntArrr[(int)CD.CardLevel.BASE] = _cardNumInPool[(int)category, (int)CD.CardLevel.BASE];
            cntArrr[(int)CD.CardLevel.NORMAL] = _cardNumInPool[(int)category, (int)CD.CardLevel.NORMAL];
            cntArrr[(int)CD.CardLevel.RARE] = _cardNumInPool[(int)category, (int)CD.CardLevel.RARE];

            int leftChance = 0;
            for (int j = 1; j < (int)CD.CardLevel.MAX; j++)
            {
                if (cntArrr[j] == 0) //没有card了,将这个等级的概率累积下来
                {
                    leftChance += _levelChance[j];
                    _cardChance[(int)category, j] = 0;
                }
                else
                    _cardChance[(int)category, j] = _levelChance[j];
            }

            // 8/2分leftChance
            if (leftChance > 0)
            {
                bool beShared = false;
                for (int j = 1; j < (int)CD.CardLevel.MAX; j++)
                {
                    if (cntArrr[j] == 0)
                        continue;
                    if(leftChance <= 0)
                        break;

                    if (beShared == false)
                    {
                        int value = Mathf.RoundToInt(leftChance * 0.8f);
                        _cardChance[(int)category, j] += value;
                        leftChance -= value;
                        beShared = true;
                    }
                    else
                    {
                        _cardChance[(int)category, j] += leftChance;
                        leftChance = 0;
                    }
                }
            }

            // 只有一个等级的card，其他两个等级都为空的时候，才会进入
            if (leftChance > 0)
            {
                for (int j = 1; j < (int)CD.CardLevel.MAX; j++)
                {
                    if (cntArrr[j] > 0)
                        _cardChance[(int)category, j] += leftChance;
                }
            }
        }

#endregion
    }
}

