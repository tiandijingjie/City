using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;

    public class CardAddPriest : CardEffection<CardAddPriest>
    {
        public CardAddPriest()
        {
            _category = CD.CardCategory.MAGIC;
            _levelCnt = new[] { 0, 1, 0, 0}; //CardDefines.CardLevel.MAX
        }

        public override bool CanBeInit()
        {
            return true;
        }

        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            return SoldierCtrl.Instance.EnableNewSoldierType(WE.RaceType.Human, SD.TroopType.Magic, (int)HumanDefines.MagicType.PRIEST);
        }
    }
}
