using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using CD = CardDefines;

    public class CardMinorFirstAidKit : CardEffection<CardMinorFirstAidKit>
    {
        public CardMinorFirstAidKit()
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
            throw new System.NotImplementedException();
        }
    }
}

