using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WE = WarFieldElements;

    //闪电塔
    public class ArcTower : DefenceBuilding
    {
#region public parameters

#endregion

#region private parameters

        private int _attackMaxCnt;
        private float _damageDown; //闪电的每次攻击之后的攻击衰减

        private GameObject[] _aoeTargets;  //工具目标
#endregion

#region private parameters' get set

#endregion

#region Unity callbacks

#endregion

#region public functions

        public override bool InitBuilding(BuildingConf conf, byte mapId)
        {
            base.InitBuilding(conf, mapId);
            _canAttackBuilding = false;
            _attackMaxCnt = (int)_defenceConf.gs_specConfs["targetNum"];
            _damageDown = 1 - _defenceConf.gs_specConfs["damageDown"];
            if(_attackMaxCnt > 0)
                _aoeTargets = new GameObject[_attackMaxCnt];
            return true;
        }

#endregion

#region private functions

        protected override void Attack()
        {
            float damage = _defenceConf.gs_damage;
            //attck main rival firstly
            ((Soldier)_rivalScript).BeAttacked(gameObject, this, WE.WarEleType.BUILDING, damage, true, true, out float hitValue);

            //攻击其余目标
            if (_attackMaxCnt > 1)
            {
                damage *= _damageDown;

                var list = _rivalInRange[(int)WE.WarEleType.SOLDIER];
                var state = (needCnt: _attackMaxCnt - 1, rival: _rival, buffer: _aoeTargets);
                //Lamda
                int actualChooseCnt = list.ForList(static (loopList, st) =>
                {
                    int rCnt = loopList.Count;
                    if (rCnt <= 1) //==1时候只包含了rival
                        return 0;
                    if (rCnt <= st.needCnt) //include the rival, but not has enough rival in range, attack them all
                    {
                        int added = 0;
                        for (int i = 0; i < rCnt; i++)
                        {
                            if(loopList[i] != st.rival && added < st.buffer.Length)
                            {
                                st.buffer[added] = loopList[i];
                                added++;
                            }
                        }
                        return added;
                    }
                    else //has enough rivals
                    {
                        // 栈上分配，只存 int 索引，极其快速且 0 GC
                        Span<int> chooseIndexes = stackalloc int[st.needCnt];
                        int chosen = 0;
                        int maxAttempts = rCnt * 2; //最大尝试次数
                        int attempts = 0;
                        while (chosen < st.needCnt && attempts < maxAttempts)
                        {
                            int seed = Utils.GetRandomInt() % rCnt;
                            bool isConflict = (loopList[seed] == st.rival);
                            if (isConflict == false)
                            {
                                for (int i = 0; i < chosen; i++) //检查是否与之前的index冲突
                                {
                                    if (chooseIndexes[i] == seed)
                                    {
                                        isConflict = true;
                                        break;
                                    }
                                }
                            }
                            if (isConflict)
                                attempts++;
                            else
                            {
                                chooseIndexes[chosen] = seed;
                                chosen++;
                                attempts = 0;
                            }
                        }
                        int exportCount = Math.Min(chosen, st.buffer.Length);
                        for (int i = 0; i < exportCount; i++)
                        {
                            st.buffer[i] = loopList[chooseIndexes[i]];
                        }
                        return exportCount;
                    }
                }, state);  //Lamda

                for (int i = 0; i < actualChooseCnt; i++)
                {
                    var target = _aoeTargets[i];
                    if (target != null)
                    {
                        target.GetComponent<Soldier>().BeAttacked(gameObject, this, WE.WarEleType.BUILDING, damage, true, true, out hitValue);
                        damage *= _damageDown;
                    }
                    _aoeTargets[i] = null;
                }
            }
        }

        protected override void OnConfUpgradeNotification(string changeName, float oriValue)
        {
            switch (changeName)
            {
                case "targetNum":
                    _attackMaxCnt = (int)_defenceConf.gs_specConfs["targetNum"];
                    _aoeTargets = new GameObject[_attackMaxCnt];
                    break;
                default:
                    break;
            }
        }

#endregion
    }
}
