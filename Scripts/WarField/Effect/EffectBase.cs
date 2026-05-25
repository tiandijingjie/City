using System;
using System.Collections;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

namespace WarField
{
    using ED = EffectDefines;

    public class EffectBase : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters
        [SerializeField] protected ParticleSystem _mainParticle = null; //最主要的partical，这个partical结束会回调MainEffectFinishCb
        [SerializeField] protected bool _selfFinish;  //特效是自己结束的不用外部的干预，这种需要监控所有partical system的结束
        [SerializeField] protected bool _effectCanMove = false;
        [SerializeField] protected bool _isParticalEffect; //是不是粒子特效。或者spine特效

        [SerializeField] protected SkeletonAnimation _spineAnim;

        protected ED.EffectType _effectType;
        protected MainEffectFinishCb _finishCb; //_mainParticle结束时的回调
        protected List<ParticleSystem> _particlesNoLoop; //不包含loop的partical system
        protected List<ParticleSystem> _particlesLoop; //loop的partical system

        protected Transform _followTarget; //跟随移动的目标
        protected bool _hasFollowTarget = false;
        protected Vector3 _followOffset; //跟随时与_followTarget的offset

        protected Transform _transform;
        protected bool _isUpdate = false;
        protected bool _isActive = false;

        protected float _mapBaseY; //所处的map的左下角y
        protected Vector3 _curPos;
#endregion

#region private parameters' get set
        public virtual ED.EffectType gs_effectType
        {
            get {return _effectType; }
        }

        public Transform gs_transform
        {
            get { return _transform; }
        }
#endregion

#region Unity callbacks

        protected virtual void Awake()
        {
            if (_isParticalEffect == true)
            {
                var children = gameObject.GetComponentsInChildren<ParticleSystem>(true);
                int cnt = children.Length;
                for (int i = 0; i < cnt; i++)
                {
                    if (children[i].main.loop == false)
                    {
                        if (_particlesNoLoop == null)
                            _particlesNoLoop = new List<ParticleSystem>();
                        _particlesNoLoop.Add(children[i].GetComponent<ParticleSystem>());
                    }
                    else
                    {
                        if (_particlesLoop == null)
                            _particlesLoop = new List<ParticleSystem>();
                        _particlesLoop.Add(children[i].GetComponent<ParticleSystem>());
                    }
                }
            }

            else
            {
                //spine 特效会发送Finish event
                _spineAnim.AnimationState.Event += OnAnimationNotification;
            }
            _transform = GetComponent<Transform>();
        }

#endregion

#region public functions
        public void ActiveEffect(float mapBaseY, object value = null)
        {
            if(_isActive == true)
                return;

            _mapBaseY = mapBaseY;
            _curPos = _transform.position;
            _curPos.z = WarFieldUtil.GetZByY(_curPos.y, _mapBaseY);
            _transform.position = _curPos;

            gameObject.SetActive(true);
            _isActive = true;

            if (_isParticalEffect == true)
            {
                int cnt = 0;
                if (_particlesNoLoop != null)
                {
                    cnt = _particlesNoLoop.Count;
                    for (int i = 0; i < cnt; i++)
                    {
                        _particlesNoLoop[i].Clear();
                        _particlesNoLoop[i].Play();
                    }

                    if (_selfFinish == true && _particlesNoLoop.Count > 0)
                    {
                        if (EffectCtrl.Instance.AddEffectInUpdateList(this) == false)
                            GameLogger.LogError($"Add effect {_effectType} into update list failed !");
                        else
                            _isUpdate = true;
                    }
                }

                if (_particlesLoop != null)
                {
                    cnt = _particlesLoop.Count;
                    for (int i = 0; i < cnt; i++)
                    {
                        _particlesLoop[i].Clear();
                        _particlesLoop[i].Play();
                    }
                }
            }

            OnActiveEffect(value);
        }

        public void DeactiveEffect()
        {
            if(_isActive == false)
                return;

            if (_isUpdate == true)
            {
                EffectCtrl.Instance.RemoveEffectFromUpdateList(this);
                _isUpdate = false;
            }

            if(_selfFinish == true)
                EffectCtrl.Instance.ReleaseEffect(this, _effectType, false);

            _finishCb = null;
            _isActive = false;
            _hasFollowTarget = false;
            _followTarget = null;
            OnDeactiveEffect();
            gameObject.SetActive(false);
        }

        //特效跟随移动目标移动
        public bool EffectFollow(Transform targetTransform, Vector3 followOffset)
        {
            if(_hasFollowTarget == true)
                return false;

            if (_isUpdate == false)
            {
                if (EffectCtrl.Instance.AddEffectInUpdateList(this) == false)
                {
                    GameLogger.LogError($"Add effect {_effectType} into update list failed !");
                    return false;
                }

                _isUpdate = true;
            }

            _hasFollowTarget = true;
            _followTarget = targetTransform;
            _followOffset = followOffset;
            return true;
        }

        //add callback, when _mainParticle finish will call this callback
        public bool AddMainEffectFinishCb(MainEffectFinishCb finishCb)
        {
            if (_isParticalEffect == true) //粒子特效
            {
                if (_mainParticle ==  null || _finishCb != null)
                {
                    GameLogger.LogError($"Add effect finish cb fail !");
                    return false;
                }

                if (_isUpdate == false)
                {
                    if (EffectCtrl.Instance.AddEffectInUpdateList(this) == false)
                    {
                        GameLogger.LogError($"Add effect {_effectType} into update list failed !");
                        return false;
                    }
                    _isUpdate = true;
                }
            }
            else //spine 特效
            {
                if (_spineAnim == null || _finishCb != null)
                {
                    GameLogger.LogError($"Add effect finish cb fail !");
                    return false;
                }
            }

            _finishCb = finishCb;

            return true;
        }

        public void EffectUpdate()
        {
            if (_hasFollowTarget == true)
            {
                if (_followTarget == null) //跟随目标死亡了，effect结束
                {
                    DeactiveEffect();
                    return;
                }
                else
                    _transform.position = _followTarget.position + _followOffset;
            }

            if (_effectCanMove == true) //设置z的遮挡关系
            {
                //检查位置，可能主动移动
                if (_transform.hasChanged == true)
                {
                    float deltaY = Mathf.Abs(_transform.position.y - _curPos.y);
                    _curPos = _transform.position;
                    if (deltaY >= 0.01f)
                    {
                        _curPos.z = WarFieldUtil.GetZByY(_curPos.y, _mapBaseY);
                        _transform.position = _curPos;
                    }

                    _transform.hasChanged = false; // 必须手动清除
                }
            }

            if(_selfFinish == true)
            {
                int cnt = _particlesNoLoop.Count; //只轮询不loop的particle
                bool hasNoLoopParticalAlive = false;
                for (int i = 0; i < cnt; i++)
                {
                    if (_particlesNoLoop[i].IsAlive() == true)
                    {
                        hasNoLoopParticalAlive = true;
                        if (_mainParticle == _particlesNoLoop[i]) //main particle没有结束
                            break;

                        if (_finishCb != null) //_mainParticle 还没有被轮询到
                            continue;

                        //没有回调,不用关心main particle是否结束
                        break;
                    }
                    else if (_mainParticle == _particlesNoLoop[i] && _finishCb != null)
                    {
                        _finishCb.MainEffectFinish(); //此时其他例子可能还没有结束
                        _finishCb = null;
                    }
                }
                if(hasNoLoopParticalAlive == false)
                    DeactiveEffect();
            }
        }

#endregion

#region private functions
        protected virtual void OnActiveEffect(object value = null) {}
        protected virtual void OnDeactiveEffect() {}

        protected void OnAnimationNotification(Spine.TrackEntry trackEntry, Spine.Event e)
        {
            string eventName = e.Data.Name;

            if(_finishCb != null)
                _finishCb.MainEffectFinish(eventName);
            if(_selfFinish == true && eventName == "Finish")
                DeactiveEffect();
        }
#endregion
    }
}

