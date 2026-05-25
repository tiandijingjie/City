using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace WarField
{
    using ED = EffectDefines;

    public class EffectCtrl : MonoBehaviour
    {
#region public parameters

        public static EffectCtrl Instance;
#endregion

#region private parameters
        [SerializeField] private GameObject[] _effectIndicatorPfb;

        //各种特效prefab (e.g. 变形术)
        private Dictionary<ED.EffectType, GameObject> _effectPfbDic;

        private Dictionary<ED.EffectType, AsyncDataPool<EffectBase>> _effectPool; //存放没有使用的effect

        private AsyncDataPool<EffectBase> _effectLoop; //需要进行轮询的effect, 必须使用AsyncDataPool，因为effect在轮询过程中可能把自己删掉

        //effect indicator
        private EffectIndicator[] _effectIndicatorArr;

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
            _effectPfbDic = new Dictionary<ED.EffectType, GameObject>();
            LoadEffectPrefabs();

            //effect pool
            _effectPool = new Dictionary<EffectDefines.EffectType, AsyncDataPool<EffectBase>>();

            foreach (var type in Enum.GetValues(typeof(ED.EffectType)))
            {
                _effectPool.Add((ED.EffectType)type, new AsyncDataPool<EffectBase>());
            }

            _effectLoop = new AsyncDataPool<EffectBase>();

            _effectIndicatorArr = new EffectIndicator[(int)ED.SkillIndicatorType.MAX];
        }

        private void Update()
        {
            foreach (var pool in _effectPool)
            {
                pool.Value.ForEachAndFlush(null);
            }

            _effectLoop.ForEachAndFlush(static effect => effect.EffectUpdate());
        }

        private void OnDestroy()
        {
            foreach (var tmp in _effectPool)
            {
                var pool = tmp.Value;
                pool?.Dispose();
            }
            _effectLoop?.Dispose();
        }

#endregion

#region public functions
        public EffectBase AddEffectAt(Vector2 pos, ED.EffectType type, int mapId, object value = null)
        {
            if(_effectPfbDic.ContainsKey(type) == false)
            {
                GameLogger.LogError($"effect {type} prefab is null");
                return null;
            }

            EffectBase effect = null;
            AsyncDataPool<EffectBase> pool = _effectPool[type];
            int poolCount = pool.Count;
            float mapBaseY = WarMapCtrl.Instance.GetMapByIndex(mapId).gs_passablePart.min.y;
            if (poolCount == 0)
            {
                effect = Instantiate(_effectPfbDic[type], pos, Quaternion.identity, transform).GetComponent<EffectBase>();
                if (effect == null)
                {
                    GameLogger.LogError("Instantiate effect, but with no effect script on gameobject");
                    return null;
                }

                effect.ActiveEffect(mapBaseY, value);
            }
            else
            {
                pool.PopOutSync(out effect);
                if (effect == null)
                {
                    GameLogger.LogError($"Fail to find effect {type} in pool");
                    return null;
                }
                effect.ActiveEffect(mapBaseY, value);
                effect.gs_transform.position = pos; //必须先给位置赋值，才能正确的计算z
            }

            return effect;
        }

        //needDeactive: 如果粒子自己调用ReleaseEffect时为false
        public void ReleaseEffect(EffectBase effect, ED.EffectType type, bool needDeactive = true)
        {
            _effectPool[type].AddItemAsync(effect);
            if(needDeactive == true)
                effect.DeactiveEffect();
        }

        public bool AddEffectInUpdateList(EffectBase effect)
        {
            _effectLoop.AddItemAsync(effect);
            return true;
        }

        public void RemoveEffectFromUpdateList(EffectBase effect)
        {
            _effectLoop.RemoveItemAsync(effect);
        }

        //获取一个skill indicator,后续处理由获取者处理
        public EffectIndicator GetEffectIndicator(ED.SkillIndicatorType type)
        {
            if(_effectIndicatorArr[(int)type] == null)
                _effectIndicatorArr[(int)type] = Instantiate(_effectIndicatorPfb[(int)type], transform).GetComponent<EffectIndicator>();
            return _effectIndicatorArr[(int)type];
        }
#endregion

#region private functions

        private void LoadEffectPrefabs()
        {
            StringBuilder path = new StringBuilder("Prefabs/EffectPf/");
            GameObject[] prefabs = Resources.LoadAll<GameObject>(path.ToString());

            Type scriptType = typeof(EffectBase);
            foreach (var prefab in prefabs)
            {
                Component scriptComponent = prefab.GetComponent(scriptType);
                if (scriptComponent != null)
                {
                    Type actualType = scriptComponent.GetType();
                    PropertyInfo property = actualType.GetProperty("gs_effectType");
                    if (property != null)
                    {
                        object effectType = property.GetValue(scriptComponent);
                        if ((int)effectType == 0)
                        {
                            GameLogger.LogError($"Load effect prefab {prefab.name} type {effectType} {(int)effectType} error");
                            continue;
                        }
                        else
                            GameLogger.LogInfo($"Load effect prefab {prefab.name} {effectType} {(int)effectType}");
                        _effectPfbDic.Add((ED.EffectType)effectType, prefab);
                    }
                    else
                    {
                        GameLogger.LogError($"Load effect prefab {prefab.name} error, can not get the interface gs_effectType");
                    }
                }
            }
        }
#endregion
    }
}

