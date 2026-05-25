using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WarField
{
    using SD = SoldierDefines;
    using WE = WarFieldElements;

    public class EnemySoldier : Soldier
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] protected SD.TroopType _troopType;

        protected float _backHomeYMul; //回归homeY速度的系数 0~1
#endregion

#region private parameters' get set

        public override SD.TroopType gs_troopType
        {
            get { return _troopType; }
        }

#endregion

#region Unity callbacks

        protected override void Awake()
        {
            base.Awake();
            gameObject.tag = "EnemySoldier";
            _faction = WE.FactionType.ENEMY;
            if (_skills.Count == 1)
                _curSkill[0] = _skills[0]; //for enemy, it can only have one skill
            else if (_skills.Count == 0)
                _curSkill = null;
            else
            {
                GameLogger.LogError($"Enemy soldier {_sdName} can only has one skill");
                return;
            }

            //查找所有的不处于隐身状态的人族士兵
            _rivalSearcher.AddCondition(new SearchCondition
            {
                p_targetEleType = (byte)WE.WarEleType.SOLDIER,
                p_targetSubType = (int)WE.EncodeEntitySubType((byte)WE.RaceType.Human, (byte)0xff, (byte)0xff, (byte)0xff),
                p_includeFlags = 0,
                p_excludeFlags = 1 << (int)SpatialDefines.EntitySpecType.HIDE,
            });
            //查找所有的人族建筑
            _rivalSearcher.AddCondition(new SearchCondition
            {
                p_targetEleType = (byte)WE.WarEleType.BUILDING,
                p_targetSubType = (int)WE.EncodeEntitySubType((byte)WE.RaceType.Human, (byte)0xff, (byte)0xff, (byte)0xff),
                p_includeFlags = 0,
                p_excludeFlags = 1 << (int)SpatialDefines.EntitySpecType.HIDE,
            });
        }

#endregion

#region public functions

        public override bool InitSoldier(byte mapId)
        {
            if (base.InitSoldier(mapId) == false)
                return false;

            if (_curSkill != null)
            {
                _curSkill[0].InitSkillObject();
                _curSkill[0].ActiveSkill();
                _curSkill[0].SetTimeStep(_curState.p_skillTimeStep);
            }

            StepIntoMap(-1, mapId, _transform.position.y);
            _backHomeYMul = (float)Utils.GetRandomInt() / 100.0f;
            //enemy not has talent
            return true;
        }

        public override void StepOutofCave(CaveTran cave, byte fromMapId, byte toMapId, float yPercent)
        {
            float y = cave.GetStepOutYPos(yPercent);
            float x = cave.GetRandomLeftXInRange();
            base.StepOutofCave(cave, fromMapId, toMapId, y);
            SoldierCtrl.Instance.TransferSoldiderToMap(this, new Vector2(x, y), fromMapId, toMapId);
        }

        public override float GetHomeYMul()
        {
            return _backHomeYMul;
        }
#endregion

#region private functions
        protected override void BindToMiniMapObj()
        {
            base.BindToMiniMapObj();

            var img = _miniMapObjTransform.Find("ElePic").GetComponent<Image>(); //建筑、士兵的图标
            img.sprite = UiMiniMapCtrl.Instance.gs_soldierIcon;
            _miniMapObjTransform.sizeDelta = new Vector2(5, 5);
            img.color = Color.red; //敌方方是红色
        }
#endregion
    }
}

