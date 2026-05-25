using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using WarUpgrade;

namespace UpgradeScene
{
    using UUD = UpgradeUIDefines;
    public class UpgradeLine : MonoBehaviour
    {
        #region public parameters

        #endregion

        #region private parameters
        [SerializeField] private RectTransform[] _lines; //0:up 1:horizon 2:down
        [SerializeField] private UpgradeItem _upperItem, _bottomItem;

        private UUD.UpgradeTypes _type;
        private Vector2 _upper = Vector2.zero, _bottom = Vector2.zero;  //these two positions are screen position
        private RectTransform _rectTransform;
        private bool _canUpgrade = false;
        private bool _beInited = false;
        #endregion

        #region private parameters' get set
        public bool gs_canUpgrade
        {
            get { return _canUpgrade; }
        }

        public UUD.UpgradeTypes gs_type
        {
            get { return _type; }
        }
        #endregion

        #region Unity callbacks
        private void Awake()
        {
            AdjustLines();
            _rectTransform = GetComponent<RectTransform>();
        }
        #endregion

        #region public functions
        public bool Init(UUD.UpgradeTypes type)
        {
            if (_beInited == true)
                return false;

            _type = type;
            _upperItem = UpgradeUI.Instance.GetUpgradeItemPosIn(_upper, _type);
            if (_upperItem != null)
                _upperItem.RegisterUpgradeRelationship(this, UUD.UpgradeSeq.PARENTUG);

            _bottomItem = UpgradeUI.Instance.GetUpgradeItemPosIn(_bottom, _type);
            if(_bottomItem != null)
                _bottomItem.RegisterUpgradeRelationship(this, UUD.UpgradeSeq.CHILDUG);

            if (_upperItem != null && _bottomItem != null)
            {
                if (_upperItem.gs_isUpgraded == true)
                    SetColor(Color.white);
                else
                    SetColor(Color.gray);
                _beInited = true;
            }
            return _beInited;
        }

        public void UpgradeItemUpgraded(UpgradeItem item)
        {
            if (item == _upperItem)
            {
                _canUpgrade = true;
                SetColor(Color.white);
            }
        }
        #endregion

        #region private functions
        //set lines length and position
        //must set up line and down line vertical and horizon line's horizon postion manually
        private void AdjustLines()
        {
            for (int i = 0; i < _lines.Length; i++)
            {
                if (_lines[i] == null) //if some line is null, mean this is just a straight line, not need to adjust
                {
                    if (_lines[0] != null && _lines[2] == null)
                    {
                        _upper = new Vector2(_lines[0].position.x, _lines[0].position.y + _lines[0].rect.width / 2);
                        _bottom = new Vector2(_lines[0].position.x, _lines[0].position.y - _lines[0].rect.width / 2);
                    }
                    else if (_lines[0] == null && _lines[2] != null)
                    {
                        _upper = new Vector2(_lines[2].position.x, _lines[2].position.y + _lines[2].rect.width / 2);
                        _bottom = new Vector2(_lines[2].position.x, _lines[2].position.y - _lines[2].rect.width / 2);
                    }
                    else if (_lines[0] != null && _lines[2] != null) //_lines[0].x _lines[2].x should be the same
                    {
                        _upper = new Vector2(_lines[0].position.x, _lines[0].position.y + _lines[0].rect.width / 2);
                        _bottom = new Vector2(_lines[2].position.x, _lines[2].position.y - _lines[2].rect.width / 2);
                        float upperYPos = _lines[0].anchoredPosition.y + _lines[0].rect.width / 2;
                        float buttonYPos = _lines[2].anchoredPosition.y - _lines[2].rect.width / 2;
                        _lines[0].anchoredPosition = new Vector2(_lines[0].anchoredPosition.x, (upperYPos + buttonYPos) / 2);
                        _lines[0].sizeDelta = new Vector2(Mathf.Abs(upperYPos - buttonYPos), _lines[0].rect.height);
                        _lines[2].gameObject.SetActive(false);
                    }
                    else if (_lines[1] != null)
                    {
                        //should not happen
                    }
                    return;
                }
            }

            //up line,  already set it x postion manually and upper point
            //down line, already set it x postion manually and button point
            //horizon line, already set it y postion manually
            float horizonX = (_lines[0].anchoredPosition.x + _lines[2].anchoredPosition.x) / 2;
            _lines[1].sizeDelta = new Vector2(Mathf.Abs(_lines[0].anchoredPosition.x - _lines[2].anchoredPosition.x), _lines[1].rect.height);
            _lines[1].anchoredPosition = new Vector2(horizonX, _lines[1].anchoredPosition.y);

            float upperY = _lines[0].anchoredPosition.y + _lines[0].rect.width / 2;
            _lines[0].anchoredPosition = new Vector2(_lines[0].anchoredPosition.x, (upperY + _lines[1].anchoredPosition.y) / 2);
            _lines[0].sizeDelta = new Vector2(Mathf.Abs(upperY - _lines[1].anchoredPosition.y), _lines[0].rect.height);

            float buttonY = _lines[2].anchoredPosition.y - _lines[2].rect.width / 2;
            _lines[2].anchoredPosition = new Vector2(_lines[2].anchoredPosition.x, (_lines[1].anchoredPosition.y + buttonY) / 2);
            _lines[2].sizeDelta = new Vector2(Mathf.Abs(_lines[1].anchoredPosition.y - buttonY), _lines[2].rect.height);

            _upper = new Vector2(_lines[0].position.x, _lines[0].position.y + _lines[0].rect.width / 2);
            _bottom = new Vector2(_lines[2].position.x, _lines[2].position.y - _lines[2].rect.width / 2);
        }

        private void SetColor(Color color)
        {
            for (int i = 0; i < _lines.Length; i++)
            {
                _lines[i].GetComponent<Image>().color = color;
            }
        }
        #endregion
    }
}

