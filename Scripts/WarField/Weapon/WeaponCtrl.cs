using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace WarField
{
    using WD = WeaponDefines;
    using WE = WarFieldElements;
    using SD = SoldierDefines;

    public class WeaponCtrl : MonoBehaviour
    {
#region public parameters

        public static WeaponCtrl Instance;

#endregion

#region private parameters

        private class WeaponPool
        {
            public long p_id; //race<<48 | troop << 32 | type << 16 | user define
            public GameObject p_prefab; //weapon prefab
            public List<Projectile> p_pool; //weapon free not in use
            public System.Object _lock;
            private WE.RaceType _race;

            public WeaponPool(long id, GameObject prefab, WE.RaceType race)
            {
                p_id = id;
                p_prefab = prefab;
                p_pool = new List<Projectile>();
                _lock = new System.Object();
                _race = race;
            }

            public Projectile GetProjectile(Transform parentTransform)
            {
                Projectile bt = null;
                lock (_lock)
                {
                    if (p_pool.Count > 0)
                    {
                        bt = p_pool[p_pool.Count - 1];
                        p_pool.RemoveAt(p_pool.Count - 1);
                    }
                }

                if (ReferenceEquals(bt, null))
                {
                    bt = Instantiate(p_prefab, parentTransform).GetComponent<Projectile>();
                    bt.gs_weaponId = p_id;
                    bt.gs_race = _race;
                }

                return bt;
            }

            public void ReleaseProjectile(Projectile bt)
            {
                if (bt != null) //already make sure the id is the same
                {
                    lock (_lock)
                    {
                        if (p_pool.Contains(bt) == false)
                            p_pool.Add(bt);
                    }
                }
            }
        }

        private struct RaceWeaponPool
        {
            public WE.RaceType p_race;
            private Dictionary<long, WeaponPool> _projectileRecordDict;

            public RaceWeaponPool(WE.RaceType race)
            {
                if (Utils.IsEnumInRange(race, WE.RaceType.MIN, WE.RaceType.MAX) == true)
                {
                    p_race = race;
                    _projectileRecordDict = new Dictionary<long, WeaponPool>();
                }
                else
                {
                    p_race = WE.RaceType.MIN;
                    _projectileRecordDict = null;
                }
            }

            public Projectile GetProjectile(long weaponId, GameObject prefab, Transform parentTransform)
            {
                _projectileRecordDict.TryGetValue(weaponId, out WeaponPool wp);
                if (wp == null)
                {
                    wp = new WeaponPool(weaponId, prefab, p_race);
                    _projectileRecordDict.Add(weaponId, wp);
                }

                return wp.GetProjectile(parentTransform);
            }

            public void ReleaseProjectile(Projectile bt)
            {
                long weaponId = bt.gs_weaponId;
                _projectileRecordDict.TryGetValue(weaponId, out WeaponPool wp);
                if (wp != null)
                    wp.ReleaseProjectile(bt);
            }
        }

        private RaceWeaponPool[] _raceWeaponArray; //[race]
        private AsyncDataPool<Projectile> _projectileInUse;

        private Transform _transform;
        private object _addLock, _rmLock;
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
            _raceWeaponArray = new RaceWeaponPool[(int)WE.RaceType.MAX];
            _projectileInUse = new AsyncDataPool<Projectile>();
            _transform = transform;
            _addLock = new object();
            _rmLock = new object();
            _beInited = false;
        }

        private void OnDestroy()
        {
            _projectileInUse?.Dispose();
        }

#endregion

#region public functions

        public bool InitWeaponCtrl()
        {
            for (int i = 1; i < (int)WE.RaceType.MAX; i++)
            {
                _raceWeaponArray[i] = new RaceWeaponPool((WE.RaceType)i);
            }

            _beInited = true;

            return true;
        }

        public Projectile GetProjectile(WE.RaceType race, long weaponId, GameObject prefab)
        {
            if (prefab == null)
                return null;

            var pj = _raceWeaponArray[(int)race].GetProjectile(weaponId, prefab, _transform);
            if (pj == null)
                return null;
            _projectileInUse.AddItemAsync(pj);

            return pj;
        }

        public void ReleaseProjectile(Projectile bt)
        {
            WE.RaceType race = bt.gs_race;
            if (Utils.IsEnumInRange(race, WE.RaceType.MIN, WE.RaceType.MAX) == false)
            {
                GameLogger.LogError($"Projectile {gameObject.name} race is not valid {race.ToString()}");
                return;
            }

            if (bt == null)
                return;
            _raceWeaponArray[(int)race].ReleaseProjectile(bt);
            _projectileInUse.RemoveItemAsync(bt);
        }

        //for soldier:   type1:troop  type2:sdType other<100
        //for building:  type1:bdMode type2:bdType 0>other>100
        public long GetWeaponID(WE.RaceType race, long type1, long type2, long other, WE.WarEleType eleType)
        {
            if (eleType == WE.WarEleType.SOLDIER && (other >= 100 || other < 0))
                GameLogger.LogError($"Error to get GetWeaponID, Soldier other value bigger then 100, {race} {type1} {type2} {other}");
            if (eleType == WE.WarEleType.BUILDING && other <= 100)
                GameLogger.LogError($"Error to get GetWeaponID, Building other value bigger then 100, {race} {type1} {type2} {other}");

            return (((long)race << 48) | ((long)type1 << 32) | ((long)type2 << 16) | (long)other);
        }

#endregion

#region private functions

#endregion
    }
}

