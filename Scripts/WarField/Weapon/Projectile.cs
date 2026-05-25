using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using WD = WeaponDefines;
    using WE = WarFieldElements;

    public class Projectile : WarEleParent
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] protected WD.ProjectileTypes _projectileType;
        [SerializeField] protected float _theta1, _theta2; //_theta1 == 0, _theta2 == 90 mean the bullet move in straight

        //whether call BullectHit() or ShellHit() to call SkillDoAttackPost()/BuffDoAttackPost()/TalentDoAttackPost()
        [SerializeField] protected bool _triggerSkill;

        protected GameObject _fromObj, _toObj;
        protected MonoBehaviour _fromScript, _toScript;
        protected WE.WarEleType _fromType, _toType;
        protected Vector2 _fromPos, _toPos;
        protected float _damage;
        protected float _interp = 0f; // Interpolation (0~1)
        protected float _step = 0f;  //the percent of _interp increacement in fixupdate (< 1)
        protected float _moveSpeed; //move speed of the bullet, e.g. 2 mean it takes 1/2s from shoot out to get to hit the target
        protected Vector2 _controlPos;

        //for shell
        protected float _damageRange; //打击范围
        protected float _otherDamage; //对于主目标之外的其余目标的伤害

        //for straight trace
        protected bool _straightTrace;
        protected Vector3 _straightStep; //projectile move in every fix update
        protected long _weaponId = 0; //use to align with the id in WeaponRecord
        protected WE.RaceType _race = WE.RaceType.MIN;  //which weapon pool this projectile belong to
#endregion

#region private parameters' get set

        public WD.ProjectileTypes gs_projectileType
        {
            get { return _projectileType; }
        }

        public long gs_weaponId
        {
            get { return _weaponId; }
            set { _weaponId = value; }
        }

        public WE.RaceType gs_race
        {
            get { return _race; }
            set { _race = value; }
        }
#endregion

#region Unity callbacks
        protected override void Awake()
        {
            base.Awake();
            _warEleType = WE.WarEleType.WEAPON;
            if (_theta1 == 0 && _theta2 == 90)
                _straightTrace = true;
            else
                _straightTrace = false;
            _beInited = false;
        }
#endregion

#region public functions

        public override void RunFixTask(float deltaTime)
        {
            if(_beInited == false)
                return;
            if (_interp <= 1)
            {
                if (_straightTrace == false)
                {
                    Vector2 newPosition = Utils.CalculateBezierPos(_interp, _fromPos, _controlPos, _toPos);
                    float angle = Utils.Vector2Degree(newPosition, _transform.position);
                    _transform.localRotation = Quaternion.Euler(0, 0, angle);
                    _transform.position = newPosition;
                }
                else
                {
                    _transform.position += _straightStep;
                }
                _interp += _step;
            }
            else
                ReachTarget();
        }

        //for shell
        //damageRange the shell attack range
        public bool InitProjectile(GameObject from, WE.WarEleType fromType, MonoBehaviour fromScript, Vector2 fromPos, GameObject to,
            WE.WarEleType toType, MonoBehaviour toScript, Vector2 toPos, float moveSpeed, WE.FactionType faction, float damage,
            float damageRange, float otherDamage, byte mapId, bool triggerSkill = true)
        {
            if (_projectileType != WD.ProjectileTypes.SHELL)
                return false;
            _damageRange = damageRange;
            _otherDamage = otherDamage;
            return InitProjectile(from, fromType, fromScript, fromPos, to, toType, toScript, toPos, moveSpeed, faction, damage, mapId, triggerSkill);
        }

        //for bullet / NoTargetBullet
        public bool InitProjectile(GameObject from, WE.WarEleType fromType, MonoBehaviour fromScript, Vector2 fromPos, GameObject to,
            WE.WarEleType toType, MonoBehaviour toScript, Vector2 toPos, float moveSpeed, WE.FactionType faction, float damage,
            byte mapId, bool triggerSkill = true)
        {
            if(_beInited == true)
                return false;

            if (base.InitWarEle(mapId) == false) //weapon的mapid不设置
                return false;

            _fromObj = from;
            _toObj = to;
            _fromScript = fromScript;
            _toScript = toScript;
            _moveSpeed = moveSpeed;
            _toType = toType;
            _fromType = fromType;
            _faction = faction;
            _interp = 0;
            _damage = damage;
            _fromPos = fromPos;
            _toPos = toPos;
            _triggerSkill = triggerSkill;

            if (_straightTrace == false)
            {
                float len = (_toPos - _fromPos).sqrMagnitude;
                if (len < 1) //if end is too close from the start , take it as a straight trace
                    _straightTrace = true;
                else
                    _controlPos = Utils.CalculateBezierControlPos(_fromPos, _toPos, _theta1, _theta2);
            }
            bool ret = OnInit();
            if (ret == false)
                return false;

            return true;
        }
#endregion

#region private functions

        protected virtual bool OnInit()
        {
            if (_straightTrace == true)
            {
                float angle = Utils.Vector2Degree(_toPos, _fromPos);
                _transform.localRotation = Quaternion.Euler(0, 0, angle);
                _straightStep = (_toPos - _fromPos).normalized * (Time.fixedDeltaTime * _moveSpeed);
                _step = _moveSpeed * Time.fixedDeltaTime / (_toPos - _fromPos).magnitude;
            }
            else
            {
                _step = _moveSpeed * Time.fixedDeltaTime / Utils.CalculateBezierCurveLength(_fromPos, _controlPos, _toPos);
            }
            gameObject.SetActive(true);
            return true;
        }

        protected virtual void ReachTarget()
        {
            //deinit self
            DeInit();
            gameObject.SetActive(false);
            WeaponCtrl.Instance.ReleaseProjectile(this);
        }

        protected override void CreateWarId()
        {
            _wfId = $"{_warEleType}";
        }

#endregion
    }
}

