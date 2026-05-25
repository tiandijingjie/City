using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;
    using GD = GlobalDefines;
    using SD = SoldierDefines;
    using BFD = BuffDefines;

    //扩音器
    public class Amplifier : PropBaseBuilding
    {
#region public parameters

#endregion

#region private parameters

        protected Dictionary<Soldier, object> _parterBuffs;
        //protected DataPool<GameObject> _rivalInRange;

        private (SD.StateSoldierEffectType, float, GD.CalDeltaType, float, string, BFD.BuffStrategy, object) _atkUpObj;
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

        override protected void Awake()
        {
            base.Awake();
            _parterBuffs = new Dictionary<Soldier, object>();
        }
#endregion

#region public functions

        public override bool InitBuilding(BuildingConf conf, byte mapId)
        {
            base.InitBuilding(conf, mapId);
            _atkUpObj = (SD.StateSoldierEffectType.DAMAGE, _propBdConf.gs_specConfs["atkUp"], GD.CalDeltaType.MULPERCENT, -1f, "Amplifier", BFD.BuffStrategy.OVERRIDE, (object)
                this);
            return true;
        }

        public override void ParterInRange(GameObject colTarget, WE.WarEleType type)
        {
            if(type == WE.WarEleType.SOLDIER)
                AddParterIntoList(colTarget);
        }

        public override void ParterOutRange(GameObject colTarget, WE.WarEleType type)
        {
            if(type == WE.WarEleType.SOLDIER)
                DelParterFromList(colTarget);
        }

#endregion

#region private functions

        protected override bool OnTimeUp()
        {
            foreach (var tmp in _parterBuffs)
            {
                Soldier sd = tmp.Key;
                object indexer = tmp.Value;
                var state = (indexer, (object)this);
                sd.StopPartOfBuff(BFD.SoldierBuffType.STATE, in state);
                _parterBuffs.Remove(sd);
            }
            return true;
        }

        protected void AddParterIntoList(GameObject rival)
        {
            Soldier sd = rival.GetComponent<Soldier>();
            if (_parterBuffs.ContainsKey(sd) == false)
            {
                object buffIndexer = null;
                if (sd.BeAffectedByBuff(BFD.SoldierBuffType.STATE, in _atkUpObj, ref buffIndexer) == true)
                    _parterBuffs.Add(sd, buffIndexer);
            }
        }

        protected void DelParterFromList(GameObject rival)
        {
            Soldier sd = rival.GetComponent<Soldier>();
            if (_parterBuffs.TryGetValue(sd, out var indexer) == true)
            {
                var state = (indexer, (object)this);
                sd.StopPartOfBuff(BFD.SoldierBuffType.STATE, in state);
                _parterBuffs.Remove(sd);
            }
        }
#endregion
    }
}
