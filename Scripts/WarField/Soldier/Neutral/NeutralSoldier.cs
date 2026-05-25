using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WarField
{
    using SD = SoldierDefines;
    using WE = WarFieldElements;

    public class NeutralSoldier : Soldier
    {
#region public parameters

#endregion

#region private parameters
        [SerializeField] protected SD.TroopType _troopType;
#endregion

#region private parameters' get set
        public SD.TroopType gs_troopType
        {
            get { return _troopType; }
        }
#endregion

#region Unity callbacks
        protected override void Awake()
        {
            base.Awake();
            _faction = WE.FactionType.NEUTRAL;
            if (_skills.Count == 1)
                _curSkill[0] = _skills[0]; //for neural, it can only have one skill
            else if (_skills.Count == 0)
                _curSkill = null;
            else
            {
                GameLogger.LogError($"Neutral soldier {_sdName} can only has one skill");
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

        public virtual bool InitSoldier(WE.FactionType faction, byte mapId)
        {
            if (faction == WE.FactionType.ENEMY)
            {
                gameObject.layer = LayerMask.NameToLayer("EnemySoldierBody");
                gameObject.tag = "EnemySoldier";
            }
            else if (faction == WE.FactionType.FRIENDLY)
            {
                gameObject.layer = LayerMask.NameToLayer("FriendlySoldierBody");
                gameObject.tag = "FriendlySoldier";
            }
            else
            {
                GameLogger.LogError($"Can not init Neutral soldier by faction {faction}");
                ChangeStatusTo(SD.SoldierStatus.ERROR);
                return false;
            }
            _faction = faction;

            if (base.InitSoldier(mapId) == false)
                return false;
            if (ReferenceEquals(_curSkill, null) == false)
            {
                _curSkill[0].InitSkillObject();
                _curSkill[0].ActiveSkill();
                _curSkill[0].SetTimeStep(_curState.p_skillTimeStep);
            }
            //neural not has talent
            return true;
        }
#endregion

#region private functions
        //绑定小地图图标
        protected override void BindToMiniMapObj()
        {
            base.BindToMiniMapObj();

            var img = _miniMapObjTransform.Find("ElePic").GetComponent<Image>(); //建筑、士兵的图标
            img.sprite = UiMiniMapCtrl.Instance.gs_soldierIcon;
            _miniMapObjTransform.sizeDelta = new Vector2(5, 5);
            img.color = Color.yellow; //中立是黄色
        }
#endregion
    }
}

