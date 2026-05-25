using System.Collections.Generic;
using UnityEngine;

namespace WarField
{
    using SKD = SkillDefines;
    using WE = WarFieldElements;

    public class Talent : MonoBehaviour
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] protected string _name;
        //a talent may has several type, e.g. only take effect during attack but it need to wait some time before take effect again
        [SerializeField] protected SKD.SkillTriggerType[] _triggerType;
        [SerializeField] private bool _beActived = false;

        protected Soldier _soldier; //the tanlent from
        protected float _timeStep; //用于加速/减速计时

        //the base do all of the init and active judgement
        protected bool _beInited = false;

#endregion

#region private parameters' get set

        public string gs_name
        {
            get { return _name; }
        }

#endregion

#region Unity callbacks

        protected virtual void Awake()
        {
            _soldier = transform.parent.GetComponent<Soldier>();
            //talent register self to soldier during Awake, but other skills need to do register during active
        }

#endregion

#region public functions

        public bool InitTalentObject()
        {
            _beInited = true;
            _timeStep = 1;
            return true;
        }

        public void SetTimeStep(float timeStep)
        {
            _timeStep = timeStep;
        }

        public virtual void DeInitTalent()
        {
            DeactiveTalent();
            _beInited = false;
        }

        public bool ActiveTalent()
        {
            if (_beActived == true)
                return false;
            _beActived = true;
            return OnActiveTalent();
        }

        public void DeactiveTalent()
        {
            if (_beActived == false)
                return;
            _beActived = false;
            OnDeactiveTalent();
        }

        public void TalentUpdate()
        {
            if (_beActived == false)
                return;

            OnTalentUpdate();
        }

        //before be attack
        //damage: the hit from the rival
        //isByPass: want to bypass the skill
        //一般血量降到一个阈值才能触发的talent,不能bypass
        public float TalentBeAttackPre(float damage, object rival, WE.WarEleType rivalType, bool isByPass)
        {
            if (_beActived == false)
                return damage;

            return OnTalentBeAttackPre(damage, rival, rivalType, isByPass);
        }

        //after be attack
        //damage: the hp the self actually dropped
        //isDead: is self dead
        //isByPass: want to bypass the skill
        //一般血量降到一个阈值才能触发的talent,不能bypass
        public void TalentBeAttackPost(float damage, bool isDead, object rival, WE.WarEleType rivalType, bool isByPass)
        {
            if (_beActived == false)
                return;
            OnTalentBeAttackPost(damage, isDead, rival, rivalType, isByPass);
            return;
        }

        //before do attack
        //damage: the hit try to send to rival
        //type: the target type (soldier or building)
        //return: do normal attack or not
        public bool TalentDoAttackPre(float hit, object rivalScript, WE.WarEleType rivalType, out float damage)
        {
            damage = hit;
            if (_beActived == false)
                return true;
            return OnTalentDoAttackPre(hit, rivalScript, rivalType, out damage);
        }

        //after do attack
        //damage: the hp the rival actually dropped
        //isDead: is rival dead
        //type: the target type (soldier or building)
        public void TalentDoAttackPost(float hit, bool isDead, object rivalScript, WE.WarEleType rivalType)
        {
            if (_beActived == false)
                return;
            OnTalentDoAttackPost(hit, isDead, rivalScript, rivalType);
        }

        //rivalSdList: 受到攻击的Soldiers
        //rivalBdList: 受到攻击的buildings
        public void TalentDoAttackPost(float hit, int dieCount, List<Soldier> rivalSdList, List<WarBuilding> rivalBdList, MonoBehaviour target,
            WE.WarEleType type)
        {
            if (_beActived == false)
                return;
            OnTalentDoAttackPost(hit, dieCount, rivalSdList, rivalBdList, target, type);
        }

        //when solider die , the Talent may only effect other solders
        //rivalList parterList: the rival or parter in the Talent collider range
        public void TalentDie()
        {
            if (_beActived == false)
                return;
            OnTalentDie();
        }

        //parter (solider or building) enter Talent range
        public void TalentParterEnter(GameObject parter, WE.WarEleType type)
        {
            if (_beActived == false)
                return;
            OnTalentParterEnter(parter, type);
        }

        //parter (solider or building) leave Talent range
        public void TalentParterLeave(GameObject parter, WE.WarEleType type)
        {
            if (_beActived == false)
                return;
            OnTalentParterLeave(parter, type);
        }

        //rival (solider or building) enter Talent range
        public void TalentRivalEnter(GameObject rival, WE.WarEleType type)
        {
            if (_beActived == false)
                return;
            OnTalentRivalEnter(rival, type);
        }

        //rival (solider or building) leave Talent range
        public void TalentRivalLeave(GameObject rival, WE.WarEleType type)
        {
            if (_beActived == false)
                return;
            OnTalentRivalLeave(rival, type);
        }

        public void TalentFinish()
        {
            //not judge _beActive, make sure end the Talent
            OnTalentFinish();
        }

        //enter reborn status(only hero support)
        //return new reborn time
        public float TalentRebornTrigger(float rebornTime)
        {
            if (_beActived == false)
                return rebornTime;

            return OnTalentRebornTrigger(rebornTime);
        }
#endregion

#region private functions

        //talent not has animation, should nobody call this api
        protected virtual void OnStartTalent()
        {
        }

        protected virtual bool OnActiveTalent()
        {
            return true;
        }

        protected virtual void OnDeactiveTalent()
        {
        }

        protected virtual void OnTalentUpdate()
        {
        }

        //before be attack
        //damage: the hit from the rival
        //isByPass: want to bypass the skill
        protected virtual float OnTalentBeAttackPre(float damage, object rival, WE.WarEleType rivalType, bool isByPass)
        {
            return damage;
        }

        //after be attack
        //damage: the hp the self actually dropped
        //isDead: is self dead
        //isByPass: want to bypass the skill
        protected virtual void OnTalentBeAttackPost(float damage, bool isDead, object rival, WE.WarEleType rivalType, bool isByPass)
        {
        }

        //before do attack
        //damage: the hit try to send to rival
        //type: the target type (soldier or building)
        //return: continue do normal attack or not
        protected virtual bool OnTalentDoAttackPre(float hit, object rivalScript, WE.WarEleType rivalType, out float damage)
        {
            damage = hit;
            return true;
        }

        //after do attack
        //damage: the hp the rival actually dropped
        //isDead: is rival dead
        //type: the target type (soldier or building)
        protected virtual void OnTalentDoAttackPost(float hit, bool isDead, object rivalScript, WE.WarEleType rivalType)
        {
        }

        //dieCount: 范围性攻击死亡个数
        //rivalSdList: 受到攻击的Soldiers
        //rivalBdList: 受到攻击的buildings
        //target: shell的主目标
        protected virtual void OnTalentDoAttackPost(float hit, int dieCount, List<Soldier> rivalSdList, List<WarBuilding> rivalBdList,
            MonoBehaviour target, WE.WarEleType type)
        {
        }

        //when solider die , the Talent may only effect other solders
        //rivalList parterList: the rival or parter in the Talent collider range
        protected virtual void OnTalentDie()
        {
        }

        //parter (solider or building) enter Talent range
        protected virtual void OnTalentParterEnter(GameObject parter, WE.WarEleType type)
        {
        }

        //parter (solider or building) leave Talent range
        protected virtual void OnTalentParterLeave(GameObject parter, WE.WarEleType type)
        {
        }

        //rival (solider or building) enter Talent range
        protected virtual void OnTalentRivalEnter(GameObject rival, WE.WarEleType type)
        {
        }

        //rival (solider or building) leave Talent range
        protected virtual void OnTalentRivalLeave(GameObject rival, WE.WarEleType type)
        {
        }

        protected virtual void OnTalentFinish()
        {
        }

        protected virtual float OnTalentRebornTrigger(float rebornTime)
        {
            return rebornTime;
        }

        //Talent not has animation
        //protected virtual void OnTalentAnimTakeEffect(string value) { }
        //protected virtual void OnTalentAnimInterrupted(SD.SoldierAnimType interruptAnim) { }

#endregion
    }
}

