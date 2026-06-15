using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using HeroGeneral;
using MeleeHero;
using RangedHero;
using MagicHero;

namespace WarField
{
    using FD = FarmerDefines;
    using WE = WarFieldElements;

    public class MainFortress : Fortress
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] private int _th1=15, _th2=75; //不同等级farmer的概率

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
            // Hero hero = SoldierCtrl.Instance.AddHeroAt(_heroType, pos, _mapId, _skillType, _talents);
            // if (hero == null)
            // {
            //     GameLogger.LogError("Fail to load hero");
            //     return;
            // }
        }

        public bool ProduceFarmer()
        {
            float seedY = UnityEngine.Random.Range(-0.6f, -0.1f);
            float seedX = UnityEngine.Random.Range(-0.3f, 0.3f);
            Vector2 pos = new Vector2(_transform.position.x + seedX + 1, _transform.position.y + seedY);
            FD.FarmerLevel level;
            WE.GenderType gender;
            int chance = Utils.GetRandomInt();
            if (chance <= _th1)
                level = FD.FarmerLevel.LOW;
            else if (chance > _th2)
                level = FD.FarmerLevel.HIGH;
            else
                level = FD.FarmerLevel.LOW;

            if (chance < 50)
                gender = WE.GenderType.MAN;
            else
                gender = WE.GenderType.WOMAN;
            return FarmerCtrl.Instance.AddFarmerAt(pos, _mapId, level, gender);
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
