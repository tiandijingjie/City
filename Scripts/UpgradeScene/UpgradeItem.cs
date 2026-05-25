using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WarField;

namespace UpgradeScene
{
    using UUD = UpgradeUIDefines;

    public class UpgradeItem : MonoBehaviour
    {
        #region public parameters

        #endregion

        #region private parameters
        [SerializeField] private string _name;

        private UUD.UpgradeTypes _type;
        private Image _image;
        private bool _beInited = false;
        private UpgradeConf _conf = null;
        private List<UpgradeLine>[] _upgradRelations;
        private bool _isUpgraded = false;
        private RectTransform _rectTransform;
        #endregion

        #region private parameters' get set
        public UUD.UpgradeTypes gs_type
        {
            get { return _type; }
        }

        public bool gs_isUpgraded
        {
            get { return _isUpgraded; }
        }

        public string gs_name
        {
            get { return _conf.p_name; }
        }
        #endregion

        #region Unity callbacks
        private void Awake()
        {
            _upgradRelations = new List<UpgradeLine>[2];
            for (int i = 0; i < _upgradRelations.Length; i++)
            {
                _upgradRelations[i] = new List<UpgradeLine> ();
            }
            _image = GetComponent<Image>();
            _rectTransform = GetComponent<RectTransform>();
            SetColor();
        }

        public void OnButton()
        {
            if (_beInited == false)
                return;

            foreach(UpgradeLine line in _upgradRelations[(int)UUD.UpgradeSeq.CHILDUG])
            {
                if (line.gs_canUpgrade == false)
                    return;
            }
            DoUpgrade();
        }
        #endregion

        #region public functions
        //called by UpgardeUI, it find this item then call the function
        public bool Init(UUD.UpgradeTypes type)
        {
            if (_beInited == true)
                return false;

            _type = type;
            _conf = UpgradeUI.Instance.GetUpgradeConf(_type, _name);
            if (_conf != null)
            {
                _image.sprite = Resources.Load<Sprite>(_conf.p_icon);
                _beInited = true;
            }
            return _beInited;
        }

        public void RegisterUpgradeRelationship(UpgradeLine line, UUD.UpgradeSeq seq)
        {
            List<UpgradeLine> list = _upgradRelations[(int)seq];
            if (list.Contains(line) == false)
            {
                list.Add(line);
            }
        }

        //is the pos in this item
        public bool IsPosIn(Vector2 screenPos)
        {
            bool isInside = RectTransformUtility.RectangleContainsScreenPoint(_rectTransform, screenPos, null);
            return isInside;
        }

        public void SetUpgradedByArchive()
        {
            if (_isUpgraded == false)
            {
                _isUpgraded = true;
                SetColor();
                foreach (UpgradeLine line in _upgradRelations[(int)UUD.UpgradeSeq.PARENTUG])
                {
                    line.UpgradeItemUpgraded(this);
                }
            }
        }
        #endregion

        #region private functions
        private void DoUpgrade()
        {
            if (_isUpgraded == false)
            {
                _isUpgraded = true;
                SetColor();
                foreach (UpgradeLine line in _upgradRelations[(int)UUD.UpgradeSeq.PARENTUG])
                {
                    line.UpgradeItemUpgraded(this);
                }
            //    Archive.Instance.SaveUpgradeItemToArchive(this);
            }
        }

        private void SetColor()
        {
            if(_isUpgraded == false)
                _image.color = Color.gray;
            else
                _image.color = Color.white;
        }
        #endregion
    }
}

