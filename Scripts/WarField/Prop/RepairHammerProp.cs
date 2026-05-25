using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace WarField
{
    using BFD = BuffDefines;
    using SD = SoldierDefines;
    using GD = GlobalDefines;

    //修理锤: 立刻修复一座受损建筑25%的生命值
    public class RepairHammerProp : Prop, SingleEffectIndicatorCb
    {
        private RepairHammerConf _thisConf;
        private Collider2D[] _clickCheckRet = new Collider2D[5];
        private Vector2 _checkSize = new Vector2(1, 1); //检查的区域大小
        private LayerMask _checkLayerMask;
        private WarBuilding _onBd; // 鼠标位置所在的建筑

        public RepairHammerProp()
        {
            _thisConf = new RepairHammerConf();
            _conf = _thisConf;
            _checkLayerMask = LayerMask.GetMask("FriendlybuildingClick");
            _onBd = null;
        }

        public override void ActiveProp()
        {
            EffectIndicator indicator = EffectCtrl.Instance.GetEffectIndicator(EffectDefines.SkillIndicatorType.SINGLE);
            ((SingleEffectIndicator)indicator).SetSingleEffectIndicator("Textures/EffectTex/Cursor/RepairCursor");
            indicator.ActiveEffectIndicator(this);
        }

        public override bool UseProp()
        {
            _onBd.BeRepaired(_thisConf.p_repairPercent, GD.CalDeltaType.MUL, true);

            return base.UseProp();
        }

        public void GiveUpEffect()
        {
            base.GiveupProp();
        }

        public void TriggerEffect(Vector2 position)
        {
            var bd = IsPosOnTarget(position);
            if(_onBd != null)
                _onBd.ResetBdColor();
            _onBd = bd;

            if(_onBd == null)
                base.GiveupProp();
            else
                UseProp();
        }

        public void CheckPosition(Vector2 position)
        {
            var bd = IsPosOnTarget(position);
            if (bd != null)
            {
                if (bd != _onBd)
                {
                    if(_onBd != null)
                        _onBd.ResetBdColor();
                    _onBd = bd;
                    _onBd.SetBdColor(new Color(0.5f, 1.0f, 0.5f));
                }
            }
            else if(_onBd != null)
            {
                _onBd.ResetBdColor();
                _onBd = null;
            }
        }

        //检查是否位于建筑
        //position: world position
        private WarBuilding IsPosOnTarget(Vector2 position)
        {
            int hitCount = Physics2D.OverlapBoxNonAlloc(position, _checkSize, 0f, _clickCheckRet, _checkLayerMask);
            if(hitCount <= 0)
                return null;
            float minZ = 9999;
            int index = -1;
            for (int i = 0; i < hitCount; i++)
            {
                if (_clickCheckRet[i].transform.position.z < minZ)
                {
                    minZ =  _clickCheckRet[i].transform.position.z;
                    index = i;
                }
            }
            if(index < 0)
                return null;

            return _clickCheckRet[index].transform.parent.GetComponent<WarBuilding>(); //_clickCheckRet这里是在FriendlybuildingClick layer的子节点
        }
    }
}
