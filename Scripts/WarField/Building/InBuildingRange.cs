using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;

    public class InBuildingRange : MonoBehaviour
    {
         #region public parameters

        #endregion

        #region private parameters
        private WarBuilding _selfBd;  //the soldier this detect belong to
        private CircleCollider2D _collider;
        private WE.FactionType _faction = WE.FactionType.MIN;
        #endregion

        #region private parameters' get set

        #endregion

        #region Unity callbacks

        private void Awake()
        {
            _selfBd = transform.parent.GetComponent<WarBuilding>();
            _collider = GetComponent<CircleCollider2D>();
        }
        
        private void OnTriggerEnter2D(Collider2D collision)
        {
            string tag = collision.gameObject.tag;
            WE.WarEleType colType = WarFieldUtil.GetWarEleType(tag);
            if (colType == WE.WarEleType.SOLDIER || colType == WE.WarEleType.BUILDING)
            {
                WE.FactionType colFaction = WarFieldUtil.GetFactionByTag(tag);
                if (colFaction == WE.FactionType.FRIENDLY || colFaction == WE.FactionType.ENEMY)
                {
                    if (_faction == WE.FactionType.MIN)
                        _faction = _selfBd.gs_bdConf.gs_faction;
                    if(_faction != colFaction) 
                        _selfBd.RivalInRange(collision.gameObject, colType, colFaction);
                    else
                        _selfBd.ParterInRange(collision.gameObject, colType);
                }
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            string tag = collision.gameObject.tag;
            WE.WarEleType colType = WarFieldUtil.GetWarEleType(tag);
            if (colType == WE.WarEleType.SOLDIER || colType == WE.WarEleType.BUILDING)
            {
                WE.FactionType colFaction = WarFieldUtil.GetFactionByTag(tag);
                if (colFaction == WE.FactionType.FRIENDLY || colFaction == WE.FactionType.ENEMY)
                {
                    if (_faction == WE.FactionType.MIN)
                        _faction = _selfBd.gs_bdConf.gs_faction;
                    if(_faction != colFaction) 
                        _selfBd.RivalOutRange(collision.gameObject, colType, colFaction);
                    else
                        _selfBd.ParterOutRange(collision.gameObject, colType);
                }
            }
        }
        #endregion

        #region public functions
        //should be only the Neutral building call this API
        public void SetFaction(WE.FactionType faction)
        {
            _faction = faction;
        }

        #endregion

        #region private functions

        #endregion
    }
}