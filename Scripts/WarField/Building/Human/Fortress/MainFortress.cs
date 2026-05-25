using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using HeroGeneral;
using MeleeHero;
using RangedHero;
using MagicHero;

namespace WarField
{
    using WBD = WarBuildingDefines;

    public class MainFortress : Fortress
    {
#region public parameters

#endregion

#region private parameters

        private HumanDefines.HeroType _heroType = HumanDefines.HeroType.RANGEDHERO;
        private int _skillType = (int)RangedHeroIndividualData.IndividualDataType.FROSTARROW; //英雄的自定义技能

        private HeroGenericIndividualData.IndividualDataType[] _talents =
        {
            //HeroGenericIndividualData.IndividualDataType.QUICKATTACK,
            //HeroGenericIndividualData.IndividualDataType.HEAVYATTACK,
            HeroGenericIndividualData.IndividualDataType.COLLECTOR
        }; //最多3个

#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

#endregion

#region public functions

        public override void StartWork()
        {
            base.StartWork();

            float seedY = UnityEngine.Random.Range(-0.6f, -0.1f);
            float seedX = UnityEngine.Random.Range(-0.3f, 0.3f);
            Vector2 pos = new Vector2(_transform.position.x + seedX + 10, _transform.position.y + seedY);
            Hero hero = SoldierCtrl.Instance.AddHeroAt(_heroType, pos, _mapId, _skillType, _talents);
            if (hero == null)
            {
                GameLogger.LogError("Fail to load hero");
                return;
            }
        }

#endregion

#region private functions
        //不会真的被删除，进入摧毁状态
        protected override void BdDestroy()
        {
            _isDestroyed = true;
            _canWork = false;
            gameObject.SetActive(false);
        }
#endregion
    }
}
