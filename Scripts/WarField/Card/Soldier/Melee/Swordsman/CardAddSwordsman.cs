using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;
    
    public class CardAddSwordsman : CardEffection<CardAddSwordsman>
    {
        public CardAddSwordsman()
        {
            _category = CD.CardCategory.MELEE;
            _levelCnt = new[] { 0, 1, 0, 0}; //CardDefines.CardLevel.MAX
        }
        
        public override bool CanBeInit()
        {
            return true;
        }
        
        protected override bool OnTakeEffect(CardDefines.CardLevel level)
        {
            return SoldierCtrl.Instance.EnableNewSoldierType(WE.RaceType.Human, SD.TroopType.Melee, (int)HumanDefines.MeleeType.SWORDSMAN);
        }
    }
}

