using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WarUpgrade;

namespace WarField
{
    using UD = UpgradeDefine;

    //UpdateCtrl创建之后UpgradeDatabase需要已经初始化
    public class UpgradeCtrl : MonoBehaviour
    {
#region public parameters
        static public UpgradeCtrl Instance = null;
#endregion

#region private parameters

        private readonly Dictionary<UD.UpgradeType, Type> _upgradeType2Class= new Dictionary<UD.UpgradeType, Type>
        {
            { UD.UpgradeType.ADDSOLDIERHPINC, typeof(AddSoldierHpIncUpgrade) },
            { UD.UpgradeType.ADDHEROHPMAX, typeof(AddHeroHpMaxUpgrade) },
            { UD.UpgradeType.ADDSOLDIERMOVEINCAVE, typeof(AddSoldierMoveInCaveUpgrade) },
            { UD.UpgradeType.UNLOCKRANGEDHERO, typeof(UnlockRangedHeroUpgrade) },
            { UD.UpgradeType.UNLOCKWHIRLWINDSLASH, typeof(UnlockWhirlwindSlashUpgrade) },
            { UD.UpgradeType.UNLOCKCANNONEER, typeof(UnlockCannoneerUpgrade) },
            { UD.UpgradeType.UNLOCKARROWRAIN, typeof(UnlockArrowRainUpgrade) },
            { UD.UpgradeType.ADDHEROHPINC, typeof(AddHeroHpIncUpgrade) },
            { UD.UpgradeType.UNLOCKSHIELDSOLDIER, typeof(UnlockShieldSoldierUpgrade) },
            { UD.UpgradeType.UNLOCKSUDDENDEMISE, typeof(UnlockSuddenDemiseUpgrade) },
            { UD.UpgradeType.UNLOCKCRISISUNLEASHED, typeof(UnlockCrisisUnleashedUpgrade) },
            { UD.UpgradeType.UNLOCKMAGICHERO, typeof(UnlockMagicHeroUpgrade) },
            { UD.UpgradeType.UNLOCKSHAMAN, typeof(UnlockShamanUpgrade) },
            { UD.UpgradeType.UNLOCKSTORMFURY, typeof(UnlockStormFuryUpgrade) },
            { UD.UpgradeType.UNLOCKFROZENSEAL, typeof(UnlockFrozenSealUpgrade) },

            { UD.UpgradeType.ADDSTARTGOLD, typeof(AddStartGoldUpgrade) },
            { UD.UpgradeType.ADDGOLDPERSEC, typeof(AddGoldPerSecUpgrade) },
            { UD.UpgradeType.ADDCAVEPRODUCE, typeof(AddCaveProduceUpgrade) },
            { UD.UpgradeType.KILLNEUTRALGETGOLD, typeof(KillNeutralGetGoldUpgrade) },
            //{ UD.UpgradeType.UNLOCKNEUTRALSELL, typeof(UnlockNeutralSellUpgrade) },  //TBD
            //{ UD.UpgradeType.CAMPREFRESHNEUTRAL, typeof(CampRefreshNeutralUpgrade) }, //TBD
            { UD.UpgradeType.KILLGETGOLD, typeof(KillGetGoldUpgrade) },

            //{ UD.UpgradeType.ONEMORECARD, typeof(OneMoreCardUpgrade) },  //TBD
            //{ UD.UpgradeType.STARTDRAW, typeof(StartDrawUpgrade) },  //TBD
            //{ UD.UpgradeType.ADDCARDLOCK, typeof(AddCardLockUpgrade) },  //TBD
            //{ UD.UpgradeType.CARDPRICEDOWN, typeof(CardPriceDownUpgrade) },  //TBD
            //{ UD.UpgradeType.ENLARGEBAG, typeof(EnlargeBagUpgrade) },  //TBD
            //{ UD.UpgradeType.DOWNDRAWNEEDEYE, typeof(DownDrawNeedEyeUpgrade) },  //TBD
            //{ UD.UpgradeType.DOWNREFRESHPRICE, typeof(DownRefreshPriceUpgrade) },  //TBD
            //{ UD.UpgradeType.GETFREEREFRESH, typeof(GetFreeRefreshUpgrade) },  //TBD

            { UD.UpgradeType.ADDMELEEBARRACKSPAWNSPOT, typeof(AddMeleeBarrackSpawnSpotUpgrade) },
            { UD.UpgradeType.ADDRANGEDBARRACKSPAWNSPOT, typeof(AddRangedBarrackSpawnSpotUpgrade) },
            { UD.UpgradeType.ADDMAGICBARRACKSPAWNSPOT, typeof(AddMagicBarrackSpawnSpotUpgrade) },
            { UD.UpgradeType.UNLOCKFROSTTOWER, typeof(UnlockFrostTowerUpgrade) },
            { UD.UpgradeType.AUTOREPAIR, typeof(AutoRepairUpgrade) },
            { UD.UpgradeType.UNLOCKFLAMETOWER, typeof(UnlockFlameTowerUpgrade) },
            { UD.UpgradeType.UNLOCKSIEGETOWER, typeof(UnlockSiegeTowerUpgrade) },
            { UD.UpgradeType.DESTROYGETFEEBACK, typeof(DestroyGetFeeBack) },
            { UD.UpgradeType.ADDTOWERDAMAGEINCAVE, typeof(AddTowerDamageInCaveUpgrade) },
            { UD.UpgradeType.DESTROYGETGOLD, typeof(DestroyGetGoldUpgrade) },
            //{ UD.UpgradeType.SELLGETFEEBACK, typeof(SellGetFeeBackUpgrade) }, //TBD
            { UD.UpgradeType.UNLOCKLASERTOWER, typeof(UnlockLaserTowerUpgrade) },
            { UD.UpgradeType.UNLOCKARCTOWER, typeof(UnlockArcTowerUpgrade) },
        };

        private UpgradeBase[] _upgradeArr;
        private DefaultUpgrade _defaultUpgrade;
        private bool _beInited;
#endregion

#region private parameters' get set

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
            _upgradeArr = new UpgradeBase[(int)UD.UpgradeType.MAX];
            _defaultUpgrade = new DefaultUpgrade();
            _beInited = false;
        }

#endregion

#region public functions

        public bool InitUpgradeCtrl()
        {
            if(_beInited == true)
                return false;

            var arr = UpgradeDatabase.Instance.gs_upgradeItemArr;
            int cnt = arr.Length;
            for (int i = 1; i < cnt; i++)
            {
                if(_upgradeType2Class.TryGetValue(arr[i].p_upgradeType, out var classType) == false)
                    continue;
                _upgradeArr[i] = (UpgradeBase)Activator.CreateInstance(classType);
                _upgradeArr[i].Init(arr[i]); //将upgrade的实现与conf绑定
            }
            _beInited = true;
            return ImplementUpgrades();
        }
#endregion

#region private functions

        private bool ImplementUpgrades()
        {
            //implement default upgrade
            if(_defaultUpgrade.ImplementUpgrade() == false)
            {
                GameLogger.LogError($"Fail to implement default upgrade");
                return false;
            }

            for (int i = 1; i < _upgradeArr.Length; i++)
            {
                if(_upgradeArr[i] == null)
                    continue;
                if (_upgradeArr[i].ImplementUpgrade() == false)
                {
                    GameLogger.LogError($"Fail to implement upgrade {_upgradeArr[i].gs_type}");
                    return false;
                }
            }
            return true;
        }
#endregion
    }
}
