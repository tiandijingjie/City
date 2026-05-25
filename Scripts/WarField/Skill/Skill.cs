using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using SKD = SkillDefines;
    using WE = WarFieldElements;
    using GD = GlobalDefines;
    using SD = SoldierDefines;

    public class Skill : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] protected string _name;
        //a skill may has several type, e.g. only take effect during attack but it need to wait some time before take effect again
        [SerializeField] protected SKD.SkillTriggerType[] _triggerType;
        [SerializeField] private bool _beActived = false;

        protected Soldier _soldier; //the skill from
        protected float _timeStep; //用于加速/减速技能的计时

        //the base do all of the init and active judgement
        private bool _beInited = false;
        private bool _waitAnim = false; //等待动画给出释放技能的通知，释放完技能之后就算动画没有播放完成被打断，也不会进入打断的函数

#endregion

#region private parameters' get set

        //针对独有技能，一般是己方士兵，英雄，敌方boss. 这些独有技能需要override，返回IndividualData中定义的值。有些技能是通用技能，返回0
        public virtual uint gs_skillType
        {
            get { return 0; }
        }

#endregion

#region Unity callbacks

        protected void Awake()
        {
            _soldier = transform.parent.GetComponent<Soldier>();
        }

#endregion

#region public functions

        public bool InitSkillObject()
        {
            //need to register skill self to soldier
            _beInited = true;
            return true;
        }

        public void SetTimeStep(float timeStep)
        {
            _timeStep = timeStep;
        }

        public virtual void DeInitSkill()
        {
            DeactiveSkill();
            _beInited = false;
        }

        public bool ActiveSkill()
        {
            if (_beActived == true)
                return false;
            _timeStep = 1;
            _beActived = true;
            for (int i = 0; i < _triggerType.Length; i++) //do after set _beActived
                _soldier.RegisterSkill(this, _triggerType[i]);
            bool ret = OnActiveSkill();
            if (string.IsNullOrEmpty(_name) == true)
            {
                GameLogger.LogError($"{this.name} not set the skill name");
                return false;
            }

            return ret;
        }

        public void DeactiveSkill()
        {
            if (_beActived == false)
                return;
            for (int i = 0; i < _triggerType.Length; i++) //do before set _beActived
                _soldier.UnregisterSkill(this, _triggerType[i]);
            _beActived = false;
            OnDeactiveSkill();
        }

        public void SkillUpdate()
        {
            if (_beActived == false)
                return;
            if (_waitAnim == true) //wait animation finish
                return;
            OnSkillUpdate();
        }

        //before be attack
        //damage: the hit from the rival
        //isByPass: want to bypass the skill
        //一般血量降到一个阈值才能触发的skill,不能bypass
        public float SkillBeAttackPre(float damage, object rival, WE.WarEleType rivalType, bool isByPass)
        {
            if (_beActived == false)
                return damage;
            return OnSkillBeAttackPre(damage, rival, rivalType, isByPass);
        }

        //after be attack
        //damage: the hp the self actually dropped
        //isDead: is self dead
        //isByPass: want to bypass the skill
        //一般血量降到一个阈值才能触发的skill,不能bypass
        public void SkillBeAttackPost(float damage, bool isDead, MonoBehaviour rivalScript, WE.WarEleType rivalType, bool isByPass)
        {
            if (_beActived == false)
                return;
            OnSkillBeAttackPost(damage, isDead, rivalScript, rivalType, isByPass);
            return;
        }

        //before do attack
        //damage: the hit try to send to rival
        //type: the target type (soldier or building)
        //return: do normal attack or not
        public bool SkillDoAttackPre(float hit, MonoBehaviour rivalScript, WE.WarEleType rivalType, out float damage)
        {
            damage = hit;
            if (_beActived == false)
                return true;
            return OnSkillDoAttackPre(hit, rivalScript, rivalType, out damage);
        }

        //after do attack
        //hit: the hp the rival actually dropped
        //isDead: is rival dead
        //rivalType: the target type (soldier or building)
        public void SkillDoAttackPost(float hit, bool isDead, object rivalScript, WE.WarEleType rivalType)
        {
            if (_beActived == false)
                return;
            OnSkillDoAttackPost(hit, isDead, rivalScript, rivalType);
            return;
        }

        //rivalSdList: 受到攻击的Soldiers
        //rivalBdList: 受到攻击的buildings
        public void SkillDoAttackPost(float hit, int dieCount, List<Soldier> rivalSdList, List<WarBuilding> rivalBdList, MonoBehaviour target,
            WE.WarEleType type)
        {
            if (_beActived == false)
                return;
            OnSkillDoAttackPost(hit, dieCount, rivalSdList, rivalBdList, target, type);
            return;
        }

        //when solider die , the skill mya only effect other solders
        //rivalList parterList: the rival or parter in the skill collider range
        public void SkillDie()
        {
            if (_beActived == false)
                return;
            OnSkillDie();
            return;
        }

        //parter (solider or building) enter skill range
        public void SkillParterEnter(GameObject parter, WE.WarEleType type)
        {
            if (_beActived == false)
                return;
            OnSkillParterEnter(parter, type);
            return;
        }

        //parter (solider or building) leave skill range
        public void SkillParterLeave(GameObject parter, WE.WarEleType type)
        {
            if (_beActived == false)
                return;
            OnSkillParterLeave(parter, type);
        }

        //rival (solider or building) enter skill range
        public void SkillRivalEnter(GameObject rival, WE.WarEleType type)
        {
            if (_beActived == false)
                return;
            OnSkillRivalEnter(rival, type);
            return;
        }

        //rival (solider or building) leave skill range
        public void SkillRivalLeave(GameObject rival, WE.WarEleType type)
        {
            if (_beActived == false)
                return;
            OnSkillRivalLeave(rival, type);
            return;
        }

        public void SkillMapChange(int fromMap, int toMap)
        {
            if (_beActived == false)
                return;
            OnSkillMapChange(fromMap, toMap);
            return;
        }

        public void SkillFinish()
        {
            //not judge _beActive, make sure end the skill
            OnSkillFinish();
        }

        //value: the message from the skill animation event content
        public void SkillAnimTakeEffect(string value)
        {
            //not judge _beActive, make sure receive info from skill animation
            _waitAnim = false;
            OnSkillAnimTakeEffect(value);
        }

        //skill was interrupted by other things, interruptAnim: the animation interrupt the skill 主动技能被打断
        public void SkillAnimInterrupted(SD.SoldierAnimType interruptAnim)
        {
            if (_waitAnim == false)
                return;
            //not judge _beActive, make sure info skill animation interrupted
            OnSkillAnimInterrupted(interruptAnim);
        }

        //activated skill, 触发主动技能
        public void SkillActivatedTrigger(string triggerName)
        {
            if (_beActived == false)
                return;

            if (_name != triggerName)
                return;

            OnSkillActivatedTrigger();
        }

        //enter reborn status(only hero support)
        //return new reborn time
        public float SkillRebornTrigger(float rebornTime)
        {
            if (_beActived == false)
                return rebornTime;

            return OnSkillRebornTrigger(rebornTime);
        }

#endregion

#region private functions

        //in this function ask soldier to play skill animation
        protected virtual void OnStartSkill()
        {
            _waitAnim = true;
            _soldier.PlaySkillAnimation(this);
        }

        protected virtual bool OnActiveSkill()
        {
            return true;
        }

        protected virtual void OnDeactiveSkill()
        {
        }

        protected virtual void OnSkillUpdate()
        {
        }

        //before be attack
        //damage: the hit from the rival
        //isByPass: want to bypass the skill
        protected virtual float OnSkillBeAttackPre(float damage, object rival, WE.WarEleType rivalType, bool isByPass)
        {
            return damage;
        }

        //after be attack
        //damage: the hp the self actually dropped
        //isDead: is self dead
        //isByPass: want to bypass the skill
        protected virtual void OnSkillBeAttackPost(float damage, bool isDead, MonoBehaviour rivalScript, WE.WarEleType rivalType, bool isByPass)
        {
        }

        //before do attack
        //damage: the hit try to send to rival
        //type: the target type (soldier or building)
        //return: continue do normal attack or not
        protected virtual bool OnSkillDoAttackPre(float hit, MonoBehaviour rivalScript, WE.WarEleType rivalType, out float damage)
        {
            damage = hit;
            return true;
        }

        //after do attack
        //damage: the hp the rival actually dropped
        //isDead: is rival dead
        //type: the target type (soldier or building)
        protected virtual void OnSkillDoAttackPost(float hit, bool isDead, object rivalScript, WE.WarEleType rivalType)
        {
        }

        //dieCount: 范围性攻击死亡个数
        //rivalSdList: 受到攻击的Soldiers
        //rivalBdList: 受到攻击的buildings
        //target: 目标
        protected virtual void OnSkillDoAttackPost(float hit, int dieCount, List<Soldier> rivalSdList, List<WarBuilding> rivalBdList,
            MonoBehaviour target, WE.WarEleType type)
        {
        }

        //when solider die , the skill mya only effect other solders
        //rivalList parterList: the rival or parter in the skill collider range
        protected virtual void OnSkillDie()
        {
        }

        //parter (solider or building) enter skill range
        protected virtual void OnSkillParterEnter(GameObject parter, WE.WarEleType type)
        {
        }

        //parter (solider or building) leave skill range
        protected virtual void OnSkillParterLeave(GameObject parter, WE.WarEleType type)
        {
        }

        //rival (solider or building) enter skill range
        protected virtual void OnSkillRivalEnter(GameObject rival, WE.WarEleType type)
        {
        }

        //rival (solider or building) leave skill range
        protected virtual void OnSkillRivalLeave(GameObject rival, WE.WarEleType type)
        {
        }

        //soldier enter a new map
        protected virtual void OnSkillMapChange(int fromMap, int toMap)
        {
        }

        protected virtual void OnSkillFinish()
        {
        }

        protected virtual void OnSkillAnimTakeEffect(string value)
        {
        }

        //skill was interrupted by other things, interruptAnim: the animation interrupt the skill 主动技能被打断
        protected virtual void OnSkillAnimInterrupted(SD.SoldierAnimType interruptAnim)
        {
        }

        protected virtual void OnSkillActivatedTrigger()
        {
        }

        protected virtual float OnSkillRebornTrigger(float rebornTime)
        {
            return rebornTime;
        }

#endregion
    }
}

